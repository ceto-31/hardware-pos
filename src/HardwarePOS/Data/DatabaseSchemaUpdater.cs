using Microsoft.Data.SqlClient;

namespace HardwarePOS.Data;

public static class DatabaseSchemaUpdater
{
    public static void EnsureDiscountsSchema()
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();

        Execute(conn, """
            IF OBJECT_ID(N'dbo.Discounts', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Discounts
                (
                    DiscountId    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Discounts PRIMARY KEY,
                    DiscountName  NVARCHAR(100) NOT NULL,
                    ApplyScope    NVARCHAR(20)  NOT NULL,
                    DiscountType  NVARCHAR(20)  NOT NULL,
                    DiscountValue DECIMAL(18,2) NOT NULL,
                    CategoryId    INT NULL,
                    StartDate     DATE NOT NULL,
                    EndDate       DATE NOT NULL,
                    IsArchived    BIT NOT NULL CONSTRAINT DF_Discounts_IsArchived DEFAULT (0),
                    CreatedAt     DATETIME2(0) NOT NULL CONSTRAINT DF_Discounts_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CONSTRAINT FK_Discounts_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories (CategoryId),
                    CONSTRAINT CK_Discounts_Scope CHECK (ApplyScope IN (N'Store', N'Category', N'Product')),
                    CONSTRAINT CK_Discounts_Type CHECK (DiscountType IN (N'PercentOff', N'SalePrice', N'FixedAmount')),
                    CONSTRAINT CK_Discounts_Value CHECK (DiscountValue > 0),
                    CONSTRAINT CK_Discounts_Dates CHECK (EndDate >= StartDate)
                );

                CREATE INDEX IX_Discounts_Schedule ON dbo.Discounts (StartDate, EndDate, IsArchived);
            END
            """);

        Execute(conn, """
            IF OBJECT_ID(N'dbo.DiscountProducts', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.DiscountProducts
                (
                    DiscountId INT NOT NULL,
                    ProductId  INT NOT NULL,
                    CONSTRAINT PK_DiscountProducts PRIMARY KEY (DiscountId, ProductId),
                    CONSTRAINT FK_DiscountProducts_Discounts FOREIGN KEY (DiscountId) REFERENCES dbo.Discounts (DiscountId) ON DELETE CASCADE,
                    CONSTRAINT FK_DiscountProducts_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (ProductId)
                );
            END
            """);
    }

    public static void EnsureBarcodeRemoved()
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();

        Execute(conn, """
            IF COL_LENGTH('dbo.Products', 'Barcode') IS NOT NULL
               OR EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'UX_Products_Barcode' AND object_id = OBJECT_ID(N'dbo.Products')
               )
            BEGIN
                DECLARE @BackupDir nvarchar(400) = CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(400));
                IF @BackupDir IS NULL OR @BackupDir = N''
                BEGIN
                    EXEC master.dbo.xp_instance_regread
                        N'HKEY_LOCAL_MACHINE',
                        N'Software\Microsoft\MSSQLServer\MSSQLServer',
                        N'BackupDirectory',
                        @BackupDir OUTPUT;
                END

                IF @BackupDir IS NULL OR @BackupDir = N''
                    THROW 50030, 'SQL Server backup directory is not configured. The Barcode column was not dropped.', 1;

                IF RIGHT(@BackupDir, 1) NOT IN (N'\', N'/')
                    SET @BackupDir = @BackupDir + N'\';

                DECLARE @BackupFile nvarchar(500) =
                    @BackupDir + N'HardwarePOS_before_barcode_drop_'
                    + CONVERT(nvarchar(8), SYSDATETIME(), 112) + N'_'
                    + REPLACE(CONVERT(nvarchar(8), SYSDATETIME(), 108), N':', N'')
                    + N'.bak';

                DECLARE @BackupSql nvarchar(max) = N'
                    BACKUP DATABASE [HardwarePOS]
                        TO DISK = @file
                        WITH COPY_ONLY, INIT, CHECKSUM,
                             NAME = N''HardwarePOS before barcode column drop'';';
                EXEC sys.sp_executesql @BackupSql, N'@file nvarchar(500)', @file = @BackupFile;

                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'UX_Products_Barcode' AND object_id = OBJECT_ID(N'dbo.Products')
                )
                    DROP INDEX UX_Products_Barcode ON dbo.Products;

                IF COL_LENGTH('dbo.Products', 'Barcode') IS NOT NULL
                    ALTER TABLE dbo.Products DROP COLUMN Barcode;
            END
            """);
    }

    public static void EnsureVoidSaleSchema()
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();

        Execute(conn, """
            IF COL_LENGTH('dbo.Sales', 'IsVoided') IS NULL
                ALTER TABLE dbo.Sales ADD IsVoided BIT NOT NULL CONSTRAINT DF_Sales_IsVoided DEFAULT (0);
            IF COL_LENGTH('dbo.Sales', 'VoidedBy') IS NULL
                ALTER TABLE dbo.Sales ADD VoidedBy INT NULL;
            IF COL_LENGTH('dbo.Sales', 'VoidedAt') IS NULL
                ALTER TABLE dbo.Sales ADD VoidedAt DATETIME2(0) NULL;
            IF COL_LENGTH('dbo.Sales', 'VoidReason') IS NULL
                ALTER TABLE dbo.Sales ADD VoidReason NVARCHAR(200) NULL;
            """);

        Execute(conn, """
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Sales_VoidedBy')
            BEGIN
                ALTER TABLE dbo.Sales
                    ADD CONSTRAINT FK_Sales_VoidedBy FOREIGN KEY (VoidedBy) REFERENCES dbo.Users (UserId);
            END
            """);

        Execute(conn, """
            IF EXISTS (
                SELECT 1
                FROM sys.check_constraints
                WHERE name = N'CK_InventoryLedger_Type'
                  AND parent_object_id = OBJECT_ID(N'dbo.InventoryLedger')
                  AND definition NOT LIKE N'%VOID%'
            )
            BEGIN
                ALTER TABLE dbo.InventoryLedger DROP CONSTRAINT CK_InventoryLedger_Type;
            END
            """);

        Execute(conn, """
            IF NOT EXISTS (
                SELECT 1
                FROM sys.check_constraints
                WHERE name = N'CK_InventoryLedger_Type'
                  AND parent_object_id = OBJECT_ID(N'dbo.InventoryLedger')
            )
            BEGIN
                ALTER TABLE dbo.InventoryLedger
                    ADD CONSTRAINT CK_InventoryLedger_Type
                    CHECK (MovementType IN (N'IN', N'OUT', N'SALE', N'VOID'));
            END
            """);
    }

    public static void EnsureOrderTypeSchema()
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        Execute(conn, """
            IF COL_LENGTH('dbo.Sales', 'OrderType') IS NULL
                ALTER TABLE dbo.Sales ADD OrderType NVARCHAR(20) NOT NULL CONSTRAINT DF_Sales_OrderType DEFAULT (N'Pickup');
            IF COL_LENGTH('dbo.Sales', 'CustomerName') IS NULL
                ALTER TABLE dbo.Sales ADD CustomerName NVARCHAR(120) NULL;
            IF COL_LENGTH('dbo.Sales', 'ContactNumber') IS NULL
                ALTER TABLE dbo.Sales ADD ContactNumber NVARCHAR(30) NULL;
            IF COL_LENGTH('dbo.Sales', 'DeliveryAddress') IS NULL
                ALTER TABLE dbo.Sales ADD DeliveryAddress NVARCHAR(300) NULL;
            """);
        Execute(conn, """
            IF NOT EXISTS (
                SELECT 1 FROM sys.check_constraints
                WHERE name = N'CK_Sales_OrderType' AND parent_object_id = OBJECT_ID(N'dbo.Sales')
            )
                ALTER TABLE dbo.Sales ADD CONSTRAINT CK_Sales_OrderType CHECK (OrderType IN (N'Pickup', N'Delivery'));
            """);
    }

    private static void Execute(SqlConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
