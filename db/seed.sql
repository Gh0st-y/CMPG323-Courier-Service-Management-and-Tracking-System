-- T12: Seed data 
--
-- Test login:
--   Username: admin
--   Password: Courier#2025


USE CourierService;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleName = 'IntakeClerk')
    INSERT INTO dbo.Roles (RoleName) VALUES ('IntakeClerk');
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleName = 'StorageStaff')
    INSERT INTO dbo.Roles (RoleName) VALUES ('StorageStaff');
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleName = 'CollectionStaff')
    INSERT INTO dbo.Roles (RoleName) VALUES ('CollectionStaff');
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleName = 'Supervisor')
    INSERT INTO dbo.Roles (RoleName) VALUES ('Supervisor');
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleName = 'SystemAdmin')
    INSERT INTO dbo.Roles (RoleName) VALUES ('SystemAdmin');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = 'admin')
BEGIN
    INSERT INTO dbo.Users (Username, Email, PasswordHash, RoleId, IsActive)
    SELECT
        'admin',
        'admin@courierservice.local',
        '$2b$11$3OSJvlyN.hQnccbS3.dds.DFhY.ipZhQtV9SSEP3bjxb7RR2dVK5W',
        RoleId,
        1
    FROM dbo.Roles
    WHERE RoleName = 'SystemAdmin';
END
GO
