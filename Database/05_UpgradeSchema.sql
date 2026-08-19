-- 4KV Hardware upgrade schema (idempotent)
USE HardwarePOS;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------- Users: security questions (SHA256 of normalized answers) ---------- */
IF COL_LENGTH('dbo.Users', 'SecurityColorHash') IS NULL
    ALTER TABLE dbo.Users ADD SecurityColorHash VARBINARY(32) NULL;
IF COL_LENGTH('dbo.Users', 'SecurityNumberHash') IS NULL
    ALTER TABLE dbo.Users ADD SecurityNumberHash VARBINARY(32) NULL;
IF COL_LENGTH('dbo.Users', 'SecurityHobbyHash') IS NULL
    ALTER TABLE dbo.Users ADD SecurityHobbyHash VARBINARY(32) NULL;
GO

/* ---------- Units master ---------- */
IF OBJECT_ID(N'dbo.Units', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Units
    (
        UnitId   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Units PRIMARY KEY,
        UnitName NVARCHAR(50) NOT NULL CONSTRAINT UQ_Units_Name UNIQUE,
        IsActive BIT NOT NULL CONSTRAINT DF_Units_IsActive DEFAULT (1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Units WHERE UnitName = N'Piece') INSERT INTO dbo.Units (UnitName) VALUES (N'Piece');
IF NOT EXISTS (SELECT 1 FROM dbo.Units WHERE UnitName = N'Box') INSERT INTO dbo.Units (UnitName) VALUES (N'Box');
IF NOT EXISTS (SELECT 1 FROM dbo.Units WHERE UnitName = N'Meter') INSERT INTO dbo.Units (UnitName) VALUES (N'Meter');
IF NOT EXISTS (SELECT 1 FROM dbo.Units WHERE UnitName = N'Kilogram') INSERT INTO dbo.Units (UnitName) VALUES (N'Kilogram');
IF NOT EXISTS (SELECT 1 FROM dbo.Units WHERE UnitName = N'Liter') INSERT INTO dbo.Units (UnitName) VALUES (N'Liter');
IF NOT EXISTS (SELECT 1 FROM dbo.Units WHERE UnitName = N'Pack') INSERT INTO dbo.Units (UnitName) VALUES (N'Pack');
GO

/* ---------- Products: ProductCode + UnitId ---------- */
IF COL_LENGTH('dbo.Products', 'ProductCode') IS NULL
    ALTER TABLE dbo.Products ADD ProductCode NVARCHAR(50) NULL;
GO

IF COL_LENGTH('dbo.Products', 'UnitId') IS NULL
    ALTER TABLE dbo.Products ADD UnitId INT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Products_Units')
BEGIN
    ALTER TABLE dbo.Products
        ADD CONSTRAINT FK_Products_Units FOREIGN KEY (UnitId) REFERENCES dbo.Units (UnitId);
END
GO

-- Backfill UnitId from UnitOfMeasure text
UPDATE p
SET UnitId = u.UnitId
FROM dbo.Products p
INNER JOIN dbo.Units u ON u.UnitName = p.UnitOfMeasure
WHERE p.UnitId IS NULL;
GO

UPDATE dbo.Products
SET UnitId = (SELECT TOP 1 UnitId FROM dbo.Units WHERE UnitName = N'Piece')
WHERE UnitId IS NULL;
GO

-- Backfill ProductCode
UPDATE dbo.Products
SET ProductCode = N'PRD-' + RIGHT(N'0000' + CAST(ProductId AS NVARCHAR(10)), 4)
WHERE ProductCode IS NULL OR ProductCode = N'';
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = N'UX_Products_ProductCode' AND object_id = OBJECT_ID(N'dbo.Products')
)
BEGIN
    CREATE UNIQUE INDEX UX_Products_ProductCode
        ON dbo.Products (ProductCode)
        WHERE ProductCode IS NOT NULL AND ProductCode <> N'';
END
GO

/* ---------- Products: optional photo filename ---------- */
IF COL_LENGTH('dbo.Products', 'ImagePath') IS NULL
    ALTER TABLE dbo.Products ADD ImagePath NVARCHAR(260) NULL;
GO

/* ---------- Suppliers: soft archive ---------- */
IF COL_LENGTH('dbo.Suppliers', 'IsArchived') IS NULL
BEGIN
    ALTER TABLE dbo.Suppliers ADD IsArchived BIT NOT NULL CONSTRAINT DF_Suppliers_IsArchived DEFAULT (0);
END
GO

UPDATE dbo.Suppliers SET IsArchived = 1 WHERE IsActive = 0 AND IsArchived = 0;
GO

/* ---------- ActivityLog ---------- */
IF OBJECT_ID(N'dbo.ActivityLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ActivityLog
    (
        ActivityId   BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ActivityLog PRIMARY KEY,
        ActivityType NVARCHAR(50)  NOT NULL,
        Description  NVARCHAR(500) NOT NULL,
        CreatedBy    INT NULL,
        CreatedAt    DATETIME2(0) NOT NULL CONSTRAINT DF_ActivityLog_CreatedAt DEFAULT (SYSDATETIME()),
        CONSTRAINT FK_ActivityLog_Users FOREIGN KEY (CreatedBy) REFERENCES dbo.Users (UserId)
    );
    CREATE INDEX IX_ActivityLog_CreatedAt ON dbo.ActivityLog (CreatedAt DESC);
END
GO

/* ---------- Seed security answers for demo users ----------
   Normalized answers hashed with SHA2_256:
   Color = blue, Number = 7, Hobby = reading
*/
DECLARE @ColorHash VARBINARY(32) = HASHBYTES('SHA2_256', N'blue');
DECLARE @NumberHash VARBINARY(32) = HASHBYTES('SHA2_256', N'7');
DECLARE @HobbyHash VARBINARY(32) = HASHBYTES('SHA2_256', N'reading');

UPDATE dbo.Users
SET SecurityColorHash = @ColorHash,
    SecurityNumberHash = @NumberHash,
    SecurityHobbyHash = @HobbyHash
WHERE Username IN (N'admin', N'cashier')
  AND (SecurityColorHash IS NULL OR SecurityNumberHash IS NULL OR SecurityHobbyHash IS NULL);
GO

PRINT N'05_UpgradeSchema completed.';
GO
