-- T08: Seed data (roles, demo users, storage locations, ~200 synthetic packages)
-- Run AFTER db/schema.sql. Idempotent: safe to re-run, it will not create duplicates.
-- All data is synthetic: fake names, fake @example.test / @courier.test emails, fake phone numbers.
-- Demo login password for all 5 demo users is listed in the README (dev use only).
USE CourierService;
GO
SET NOCOUNT ON;
GO

-- 1. Roles (schema.sql already creates them; MERGE keeps this safe to re-run)
MERGE dbo.Roles AS t
USING (VALUES ('IntakeClerk'), ('StorageStaff'), ('CollectionStaff'), ('Supervisor'), ('SystemAdmin'))
    AS s (RoleName)
ON t.RoleName = s.RoleName
WHEN NOT MATCHED THEN INSERT (RoleName) VALUES (s.RoleName);
GO

-- 2. One demo user per role (passwords are BCrypt hashes, work factor 11)
MERGE dbo.Users AS t
USING (
    SELECT v.Username, v.Email, v.PasswordHash, r.RoleId
    FROM (VALUES
    ('intake.demo', 'intake.demo@courier.test', '$2b$11$5e8CC8a3HobUjT/.ZEa1W.l5xFiIe.lhD1W/k0BBOyfmtBruj55KW', 'IntakeClerk'),
    ('storage.demo', 'storage.demo@courier.test', '$2b$11$oQLelS.HrE1HmULuYBeBgOTI6CB/P.ZxeqSKmr8oa0nDLWkpdoB2y', 'StorageStaff'),
    ('collection.demo', 'collection.demo@courier.test', '$2b$11$4DCaYoreH2kkJusNPnu0ZeNKtdo.A/g6Yd9IOuhUvQ14/YHIG1uuu', 'CollectionStaff'),
    ('supervisor.demo', 'supervisor.demo@courier.test', '$2b$11$EZEb/3yx286fxaS6j6Xr6O4lmD/fJwtMIYIGYqVAg3upFhtAguDri', 'Supervisor'),
    ('admin.demo', 'admin.demo@courier.test', '$2b$11$eYQrzWK2np/M8roTrbEsreYYVSZIXMK9GoWJ8stnyfuDhH.7J7EH2', 'SystemAdmin')
    ) AS v (Username, Email, PasswordHash, RoleName)
    JOIN dbo.Roles r ON r.RoleName = v.RoleName
) AS s
ON t.Username = s.Username
WHEN NOT MATCHED THEN
    INSERT (Username, Email, PasswordHash, RoleId) VALUES (s.Username, s.Email, s.PasswordHash, s.RoleId);
GO

-- 3. Storage locations (one inactive, so "active only" can be tested)
MERGE dbo.StorageLocations AS t
USING (VALUES
    ('Shelf A-1', 'Front shelf A, level 1', 1),
    ('Shelf A-2', 'Front shelf A, level 2', 1),
    ('Shelf A-3', 'Front shelf A, level 3', 1),
    ('Shelf B-1', 'Back shelf B, level 1', 1),
    ('Shelf B-2', 'Back shelf B, level 2', 1),
    ('Shelf B-3', 'Back shelf B, level 3', 1),
    ('Cage C-1', 'Large parcels cage', 1),
    ('Old Store Room', 'Retired location (inactive)', 0)
) AS s (Code, Description, IsActive)
ON t.Code = s.Code
WHEN NOT MATCHED THEN INSERT (Code, Description, IsActive) VALUES (s.Code, s.Description, s.IsActive);
GO

-- 4. ~200 synthetic recipients + packages + status history
IF EXISTS (SELECT 1 FROM dbo.Recipients WHERE IdentifierNo LIKE 'SEED-%')
BEGIN
    PRINT 'Synthetic packages already seeded - nothing to do.';
    RETURN;
END

DECLARE @intake  INT = (SELECT UserId FROM dbo.Users WHERE Username = 'intake.demo');
DECLARE @storage INT = (SELECT UserId FROM dbo.Users WHERE Username = 'storage.demo');
DECLARE @collect INT = (SELECT UserId FROM dbo.Users WHERE Username = 'collection.demo');
DECLARE @locCount INT = (SELECT COUNT(*) FROM dbo.StorageLocations WHERE IsActive = 1);

IF @intake IS NULL OR @storage IS NULL OR @collect IS NULL OR @locCount = 0
    THROW 50001, 'Demo users or storage locations are missing.', 1;

-- numbers 1..200
SELECT TOP (200) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS INT) AS n
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
       'SEED-' + RIGHT('000000' + CAST(N.n AS VARCHAR(10)), 6),
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
SELECT 'F20-' + RIGHT('0000' + CAST(N.n AS VARCHAR(10)), 4),
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
JOIN dbo.Recipients r ON r.IdentifierNo = 'SEED-' + RIGHT('000000' + CAST(N.n AS VARCHAR(10)), 6)
JOIN #Loc loc ON loc.rn = (N.n % @locCount) + 1
CROSS APPLY (SELECT
        CASE N.n % 4 WHEN 0 THEN 'Registered' WHEN 1 THEN 'InStorage'
                     WHEN 2 THEN 'ReadyForCollection' ELSE 'Collected' END AS Status,
        CASE WHEN N.n % 3 = 0 THEN 'WorkRelated' ELSE 'Personal' END AS Cls,
        DATEADD(MINUTE, -(N.n * 180), SYSUTCDATETIME()) AS Created) d;

-- Status history: one row per step each package has gone through
INSERT dbo.PackageStatusHistory (PackageId, FromStatus, ToStatus, ChangedByUserId, ChangedAtUtc)
SELECT PackageId, NULL, 'Registered', @intake, CreatedAtUtc
FROM dbo.Packages WHERE F20Identifier LIKE 'F20-[0-9][0-9][0-9][0-9]';

INSERT dbo.PackageStatusHistory (PackageId, FromStatus, ToStatus, ChangedByUserId, ChangedAtUtc)
SELECT PackageId, 'Registered', 'InStorage', @storage, DATEADD(MINUTE, 1, CreatedAtUtc)
FROM dbo.Packages
WHERE F20Identifier LIKE 'F20-[0-9][0-9][0-9][0-9]' AND Status IN ('InStorage', 'ReadyForCollection', 'Collected');

INSERT dbo.PackageStatusHistory (PackageId, FromStatus, ToStatus, ChangedByUserId, ChangedAtUtc)
SELECT PackageId, 'InStorage', 'ReadyForCollection', @storage, DATEADD(MINUTE, 2, CreatedAtUtc)
FROM dbo.Packages
WHERE F20Identifier LIKE 'F20-[0-9][0-9][0-9][0-9]' AND Status IN ('ReadyForCollection', 'Collected');

INSERT dbo.PackageStatusHistory (PackageId, FromStatus, ToStatus, ChangedByUserId, ChangedAtUtc)
SELECT PackageId, 'ReadyForCollection', 'Collected', @collect, CollectedAtUtc
FROM dbo.Packages
WHERE F20Identifier LIKE 'F20-[0-9][0-9][0-9][0-9]' AND Status = 'Collected';

DROP TABLE #N, #F, #L, #Loc;
PRINT 'Inserted 200 synthetic packages (with recipients and status history).';

GO