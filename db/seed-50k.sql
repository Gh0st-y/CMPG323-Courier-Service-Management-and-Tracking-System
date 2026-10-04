-- T08 (optional): 50,000 synthetic packages for the performance test
-- Run AFTER db/schema.sql and db/seed.sql. Idempotent. Takes a little while - that is normal.
-- Package IDs run F20-10001 to F20-60000 so they never clash with seed.sql (F20-0001 to F20-0200).
USE CourierService;
GO
SET NOCOUNT ON;
GO
IF EXISTS (SELECT 1 FROM dbo.Recipients WHERE IdentifierNo LIKE 'PERF-%')
BEGIN
    PRINT '50,000-package seed already present - nothing to do.';
    RETURN;
END

DECLARE @intake  INT = (SELECT UserId FROM dbo.Users WHERE Username = 'intake.demo');
DECLARE @storage INT = (SELECT UserId FROM dbo.Users WHERE Username = 'storage.demo');
DECLARE @collect INT = (SELECT UserId FROM dbo.Users WHERE Username = 'collection.demo');
DECLARE @locCount INT = (SELECT COUNT(*) FROM dbo.StorageLocations WHERE IsActive = 1);

IF @intake IS NULL OR @storage IS NULL OR @collect IS NULL OR @locCount = 0
    THROW 50001, 'Demo users or storage locations are missing. Run db/seed.sql first.', 1;

-- numbers 1..50000
SELECT TOP (50000) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS INT) AS n
INTO #N
FROM sys.all_objects a CROSS JOIN sys.all_objects b;

CREATE TABLE #F (i INT, nm NVARCHAR(50));
INSERT #F VALUES
    (0, 'Thabo'),
    (1, 'Lerato'),
    (2, 'Sipho'),
    (3, 'Naledi'),
    (4, 'Pieter'),
    (5, 'Anika'),
    (6, 'Johan'),
    (7, 'Zanele'),
    (8, 'Michael'),
    (9, 'Fatima'),
    (10, 'Kagiso'),
    (11, 'Emma'),
    (12, 'Ruan'),
    (13, 'Palesa'),
    (14, 'Daniel'),
    (15, 'Ayesha'),
    (16, 'Tshepo'),
    (17, 'Chloe'),
    (18, 'Andile'),
    (19, 'Marius');
CREATE TABLE #L (i INT, nm NVARCHAR(50));
INSERT #L VALUES
    (0, 'Nkosi'),
    (1, 'Botha'),
    (2, 'Molefe'),
    (3, 'Van Wyk'),
    (4, 'Dlamini'),
    (5, 'Naidoo'),
    (6, 'Pretorius'),
    (7, 'Mokoena'),
    (8, 'Khumalo'),
    (9, 'Smit'),
    (10, 'Sithole'),
    (11, 'Joubert'),
    (12, 'Mahlangu'),
    (13, 'Venter'),
    (14, 'Ndlovu'),
    (15, 'Coetzee'),
    (16, 'Radebe'),
    (17, 'Steyn'),
    (18, 'Mthembu'),
    (19, 'Du Plessis');

CREATE TABLE #Loc (rn INT, StorageLocationId INT);
INSERT #Loc
SELECT ROW_NUMBER() OVER (ORDER BY StorageLocationId), StorageLocationId
FROM dbo.StorageLocations WHERE IsActive = 1;

-- Recipients (fake names, fake .test emails, fake phone numbers)
INSERT dbo.Recipients (FullName, IdentifierNo, Email, PhoneNumber, Department)
SELECT f.nm + ' ' + l.nm,
       'PERF-' + RIGHT('000000' + CAST(N.n AS VARCHAR(10)), 6),
       LOWER(REPLACE(f.nm, ' ', '')) + '.' + LOWER(REPLACE(l.nm, ' ', '')) + CAST(N.n AS VARCHAR(10)) + '@example.test',
       '000-' + RIGHT('0000000' + CAST(N.n AS VARCHAR(10)), 7),
       CASE WHEN N.n % 5 = 0 THEN NULL
            ELSE CASE N.n % 4 WHEN 0 THEN 'Computer Science' WHEN 1 THEN 'Accounting'
                              WHEN 2 THEN 'Nursing' ELSE 'Law' END END
FROM #N N
JOIN #F f ON f.i = N.n % 20
JOIN #L l ON l.i = (N.n * 7) % 20;

-- Packages: statuses cycle Registered / InStorage / ReadyForCollection / Collected
INSERT dbo.Packages (F20Identifier, RecipientId, SenderName, PackageType, Classification, Fee,
                     PaymentStatus, Status, StorageLocationId, CreatedByUserId, CreatedAtUtc,
                     CollectedAtUtc, CollectedByUserId)
SELECT 'F20-' + CAST(10000 + N.n AS VARCHAR(10)),
       r.RecipientId,
       'Sample Sender ' + CAST(N.n % 25 AS VARCHAR(5)),
       CASE N.n % 3 WHEN 0 THEN 'Envelope' WHEN 1 THEN 'Box' ELSE 'Parcel' END,
       d.Cls,
       CASE WHEN d.Cls = 'WorkRelated' THEN 0.00 ELSE 10.00 END,
       CASE WHEN d.Cls = 'WorkRelated' THEN 'Exempt'
            WHEN d.Status = 'Collected' THEN 'Paid' ELSE 'Unpaid' END,
       d.Status,
       CASE WHEN d.Status = 'Registered' THEN NULL ELSE loc.StorageLocationId END,
       @intake,
       d.Created,
       CASE WHEN d.Status = 'Collected' THEN DATEADD(MINUTE, 3, d.Created) END,
       CASE WHEN d.Status = 'Collected' THEN @collect END
FROM #N N
JOIN dbo.Recipients r ON r.IdentifierNo = 'PERF-' + RIGHT('000000' + CAST(N.n AS VARCHAR(10)), 6)
JOIN #Loc loc ON loc.rn = (N.n % @locCount) + 1
CROSS APPLY (SELECT
        CASE N.n % 4 WHEN 0 THEN 'Registered' WHEN 1 THEN 'InStorage'
                     WHEN 2 THEN 'ReadyForCollection' ELSE 'Collected' END AS Status,
        CASE WHEN N.n % 3 = 0 THEN 'WorkRelated' ELSE 'Personal' END AS Cls,
        DATEADD(MINUTE, -(N.n * 5), SYSUTCDATETIME()) AS Created) d;

-- Status history: one row per step each package has gone through
INSERT dbo.PackageStatusHistory (PackageId, FromStatus, ToStatus, ChangedByUserId, ChangedAtUtc)
SELECT PackageId, NULL, 'Registered', @intake, CreatedAtUtc
FROM dbo.Packages WHERE F20Identifier LIKE 'F20-[1-9][0-9][0-9][0-9][0-9]';

INSERT dbo.PackageStatusHistory (PackageId, FromStatus, ToStatus, ChangedByUserId, ChangedAtUtc)
SELECT PackageId, 'Registered', 'InStorage', @storage, DATEADD(MINUTE, 1, CreatedAtUtc)
FROM dbo.Packages
WHERE F20Identifier LIKE 'F20-[1-9][0-9][0-9][0-9][0-9]' AND Status IN ('InStorage', 'ReadyForCollection', 'Collected');

INSERT dbo.PackageStatusHistory (PackageId, FromStatus, ToStatus, ChangedByUserId, ChangedAtUtc)
SELECT PackageId, 'InStorage', 'ReadyForCollection', @storage, DATEADD(MINUTE, 2, CreatedAtUtc)
FROM dbo.Packages
WHERE F20Identifier LIKE 'F20-[1-9][0-9][0-9][0-9][0-9]' AND Status IN ('ReadyForCollection', 'Collected');

INSERT dbo.PackageStatusHistory (PackageId, FromStatus, ToStatus, ChangedByUserId, ChangedAtUtc)
SELECT PackageId, 'ReadyForCollection', 'Collected', @collect, CollectedAtUtc
FROM dbo.Packages
WHERE F20Identifier LIKE 'F20-[1-9][0-9][0-9][0-9][0-9]' AND Status = 'Collected';

DROP TABLE #N, #F, #L, #Loc;
PRINT 'Inserted 50000 synthetic packages (with recipients and status history).';

GO