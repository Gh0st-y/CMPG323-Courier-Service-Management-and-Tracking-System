-- T04: Courier Service Management and Tracking System
-- Target: SQL Server Express / LocalDB (per DECISIONS.md #1)
-- Idempotent: safe to re-run against an empty or existing database.
-- Run this whole file to (re)build the schema from scratch.

IF DB_ID('CourierService') IS NULL
BEGIN
    CREATE DATABASE CourierService;
END
GO
USE CourierService;
GO

-- ===================== Roles & Users (FR-01, FR-02, SR-01, SR-02) =====================
IF OBJECT_ID('dbo.Roles', 'U') IS NULL
CREATE TABLE dbo.Roles (
    RoleId      INT IDENTITY(1,1) PRIMARY KEY,
    RoleName    NVARCHAR(50) NOT NULL UNIQUE
    -- IntakeClerk, StorageStaff, CollectionStaff, Supervisor, SystemAdmin
);
GO

IF OBJECT_ID('dbo.Users', 'U') IS NULL
CREATE TABLE dbo.Users (
    UserId          INT IDENTITY(1,1) PRIMARY KEY,
    Username        NVARCHAR(100) NOT NULL UNIQUE,
    Email           NVARCHAR(200) NOT NULL UNIQUE,
    PasswordHash    NVARCHAR(200) NOT NULL,   -- BCrypt (NRF-013)
    RoleId          INT NOT NULL REFERENCES dbo.Roles(RoleId),
    IsActive        BIT NOT NULL DEFAULT 1,
    CreatedAtUtc    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    LastLoginUtc    DATETIME2 NULL
);
GO

-- ===================== Storage locations (FR-03, FR-04) =====================
IF OBJECT_ID('dbo.StorageLocations', 'U') IS NULL
CREATE TABLE dbo.StorageLocations (
    StorageLocationId  INT IDENTITY(1,1) PRIMARY KEY,
    Code               NVARCHAR(50) NOT NULL UNIQUE,   -- e.g. "Shelf A-3"
    Description        NVARCHAR(200) NULL,
    IsActive           BIT NOT NULL DEFAULT 1
);
GO

-- ===================== Recipients (FR-03; DR-004 data minimality) =====================
IF OBJECT_ID('dbo.Recipients', 'U') IS NULL
CREATE TABLE dbo.Recipients (
    RecipientId     INT IDENTITY(1,1) PRIMARY KEY,
    FullName        NVARCHAR(150) NOT NULL,
    IdentifierNo    NVARCHAR(50) NULL,     -- student/staff number, optional
    Email           NVARCHAR(200) NULL,
    PhoneNumber     NVARCHAR(30) NULL,
    Department      NVARCHAR(150) NULL,    -- optional per Functional Spec
    CreatedAtUtc    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

-- ===================== Packages (FR-03, FR-15, DR-008) =====================
IF OBJECT_ID('dbo.Packages', 'U') IS NULL
CREATE TABLE dbo.Packages (
    PackageId           INT IDENTITY(1,1) PRIMARY KEY,
    F20Identifier        NVARCHAR(30) NOT NULL UNIQUE,   -- QR payload value (CON-008)
    RecipientId          INT NOT NULL REFERENCES dbo.Recipients(RecipientId),
    SenderName            NVARCHAR(150) NULL,
    PackageType           NVARCHAR(50) NULL,     -- Envelope, Box, Parcel
    Classification         NVARCHAR(20) NOT NULL, -- 'Personal' or 'WorkRelated'
    Fee                    DECIMAL(6,2) NOT NULL, -- R10 or R0, from config (T06)
    PaymentStatus          NVARCHAR(20) NOT NULL DEFAULT 'Unpaid', -- Paid/Unpaid/Exempt (FR-17)
    Status                 NVARCHAR(30) NOT NULL DEFAULT 'Registered',
        -- Registered | InStorage | ReadyForCollection | Collected  (DECISIONS.md #7)
    StorageLocationId      INT NULL REFERENCES dbo.StorageLocations(StorageLocationId),
    Notes                  NVARCHAR(500) NULL,
    CreatedByUserId         INT NOT NULL REFERENCES dbo.Users(UserId),
    CreatedAtUtc             DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CollectedAtUtc            DATETIME2 NULL,
    CollectedByUserId          INT NULL REFERENCES dbo.Users(UserId),
    RowVersion                 ROWVERSION  -- optimistic concurrency (PR-02 / concurrent edits)
);
GO
CREATE INDEX IX_Packages_Status ON dbo.Packages(Status);
CREATE INDEX IX_Packages_RecipientId ON dbo.Packages(RecipientId);
CREATE INDEX IX_Packages_CreatedAtUtc ON dbo.Packages(CreatedAtUtc);
GO

-- ===================== Status history (FR-04, DR-009, DR-010) =====================
IF OBJECT_ID('dbo.PackageStatusHistory', 'U') IS NULL
CREATE TABLE dbo.PackageStatusHistory (
    PackageStatusHistoryId INT IDENTITY(1,1) PRIMARY KEY,
    PackageId               INT NOT NULL REFERENCES dbo.Packages(PackageId),
    FromStatus                NVARCHAR(30) NULL,
    ToStatus                   NVARCHAR(30) NOT NULL,
    ChangedByUserId              INT NOT NULL REFERENCES dbo.Users(UserId),
    ChangedAtUtc                  DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    Notes                          NVARCHAR(300) NULL
);
GO
CREATE INDEX IX_PackageStatusHistory_PackageId ON dbo.PackageStatusHistory(PackageId);
GO

-- ===================== Notifications (FR-06, FR-09, FR-10, IR-001..IR-004, DR-012) =====
IF OBJECT_ID('dbo.NotificationQueue', 'U') IS NULL
CREATE TABLE dbo.NotificationQueue (
    NotificationQueueId INT IDENTITY(1,1) PRIMARY KEY,
    PackageId            INT NOT NULL REFERENCES dbo.Packages(PackageId),
    Channel               NVARCHAR(10) NOT NULL,  -- 'Email' or 'SMS'
    TemplateKey            NVARCHAR(50) NOT NULL,  -- 'ReadyForCollection' | 'Collected'
    Status                   NVARCHAR(20) NOT NULL DEFAULT 'Pending', -- Pending/Sent/Failed
    AttemptCount              INT NOT NULL DEFAULT 0,
    EnqueuedAtUtc               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    LastAttemptAtUtc              DATETIME2 NULL
);
GO

IF OBJECT_ID('dbo.NotificationLog', 'U') IS NULL
CREATE TABLE dbo.NotificationLog (
    NotificationLogId INT IDENTITY(1,1) PRIMARY KEY,
    PackageId          INT NOT NULL REFERENCES dbo.Packages(PackageId),
    Channel              NVARCHAR(10) NOT NULL,
    RecipientAddress      NVARCHAR(200) NOT NULL,  -- email or phone (masked in UI, not in DB)
    Subject                NVARCHAR(200) NULL,
    Status                  NVARCHAR(20) NOT NULL,  -- Sent / Failed
    ErrorDetail              NVARCHAR(500) NULL,
    SentAtUtc                 DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
CREATE INDEX IX_NotificationLog_PackageId ON dbo.NotificationLog(PackageId);
GO

-- ===================== Audit log — append-only (SR-04, NFR-019, DR-010, DR-011) =====
IF OBJECT_ID('dbo.AuditLog', 'U') IS NULL
CREATE TABLE dbo.AuditLog (
    AuditLogId    BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId         INT NULL REFERENCES dbo.Users(UserId),  -- null for failed login of unknown user
    Action           NVARCHAR(100) NOT NULL,  -- 'LoginSuccess','LoginFailed','PackageCreated', etc.
    EntityType         NVARCHAR(50) NULL,      -- 'Package','User','CsvImport', ...
    EntityId             NVARCHAR(50) NULL,     -- e.g. F20Identifier or UserId as string
    Detail                 NVARCHAR(1000) NULL,  -- no personal data (SR-03)
    OccurredAtUtc            DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
-- No update/delete grants or code paths should ever target this table.
GO
CREATE INDEX IX_AuditLog_OccurredAtUtc ON dbo.AuditLog(OccurredAtUtc);
GO

-- ===================== Config (OR-01, NFR-024, IR-004) =====================
IF OBJECT_ID('dbo.AppConfig', 'U') IS NULL
CREATE TABLE dbo.AppConfig (
    ConfigKey     NVARCHAR(100) PRIMARY KEY,
    ConfigValue     NVARCHAR(1000) NOT NULL,
    Description       NVARCHAR(300) NULL
);
GO
-- Seed defaults (safe to re-run: MERGE avoids duplicate key errors)
MERGE dbo.AppConfig AS target
USING (VALUES
    ('Fee.Personal', '10.00', 'Fee (R) for a Personal package'),
    ('Fee.WorkRelated', '0.00', 'Fee (R) for a Work-related package'),
    ('Sms.Enabled', 'false', 'Toggle SMS channel on/off (email always sends)'),
    ('Notification.ReadyForCollection.Subject', 'Your package is ready for collection', ''),
    ('Notification.Collected.Subject', 'Your package has been collected', '')
) AS src (ConfigKey, ConfigValue, Description)
ON target.ConfigKey = src.ConfigKey
WHEN NOT MATCHED THEN
    INSERT (ConfigKey, ConfigValue, Description) VALUES (src.ConfigKey, src.ConfigValue, src.Description);
GO

-- Seed roles (idempotent)
MERGE dbo.Roles AS target
USING (VALUES ('IntakeClerk'), ('StorageStaff'), ('CollectionStaff'), ('Supervisor'), ('SystemAdmin'))
    AS src (RoleName)
ON target.RoleName = src.RoleName
WHEN NOT MATCHED THEN INSERT (RoleName) VALUES (src.RoleName);
GO
