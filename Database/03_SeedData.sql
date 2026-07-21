-- Hardware POS: Seed data
-- Prerequisites: 01_CreateDatabase.sql, 02_CreateTables.sql
USE HardwarePOS;
GO

/* Roles */
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleName = N'Admin')
    INSERT INTO dbo.Roles (RoleName) VALUES (N'Admin');
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleName = N'Cashier')
    INSERT INTO dbo.Roles (RoleName) VALUES (N'Cashier');
GO

DECLARE @AdminRoleId INT = (SELECT RoleId FROM dbo.Roles WHERE RoleName = N'Admin');
DECLARE @CashierRoleId INT = (SELECT RoleId FROM dbo.Roles WHERE RoleName = N'Cashier');

/* Users: admin / Password123 , cashier / Cashier@123
   PBKDF2-SHA256, 100000 iterations, 32-byte salt, 32-byte hash */
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = N'admin')
BEGIN
    INSERT INTO dbo.Users (Username, PasswordHash, PasswordSalt, FullName, RoleId, IsActive)
    VALUES (
        N'admin',
        CONVERT(VARBINARY(64), 0xED2DCE7B0F6C779299D9D59FCF763ADF106293B1E5A25BF5239A8708149401D0),
        CONVERT(VARBINARY(32), 0xA1B2C3D4E5F60718293A4B5C6D7E8F90112233445566778899AABBCCDDEEFF00),
        N'System Administrator',
        @AdminRoleId,
        1
    );
END
ELSE
BEGIN
    -- Keep admin password in sync when re-running seed
    UPDATE dbo.Users
    SET PasswordHash = CONVERT(VARBINARY(64), 0xED2DCE7B0F6C779299D9D59FCF763ADF106293B1E5A25BF5239A8708149401D0),
        PasswordSalt = CONVERT(VARBINARY(32), 0xA1B2C3D4E5F60718293A4B5C6D7E8F90112233445566778899AABBCCDDEEFF00)
    WHERE Username = N'admin';
END

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = N'cashier')
BEGIN
    INSERT INTO dbo.Users (Username, PasswordHash, PasswordSalt, FullName, RoleId, IsActive)
    VALUES (
        N'cashier',
        CONVERT(VARBINARY(64), 0x2B8E51E0893A6DE9F09E72D89280A47124FCF64A3778373A10F282B16F48F113),
        CONVERT(VARBINARY(32), 0x00112233445566778899AABBCCDDEEFF0F1E2D3C4B5A69788796A5B4C3D2E1F0),
        N'Store Cashier',
        @CashierRoleId,
        1
    );
END
GO

/* App settings — PHP + 12% VAT */
MERGE dbo.AppSettings AS t
USING (VALUES
    (N'StoreName', N'HARDWARE'),
    (N'Currency', N'PHP'),
    (N'TaxRate', N'0.12'),
    (N'ReceiptFooter', N'Thank you for shopping with us!')
) AS s (SettingKey, SettingValue)
ON t.SettingKey = s.SettingKey
WHEN MATCHED THEN UPDATE SET SettingValue = s.SettingValue
WHEN NOT MATCHED THEN INSERT (SettingKey, SettingValue) VALUES (s.SettingKey, s.SettingValue);
GO

/* Categories */
IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE CategoryName = N'Fasteners')
    INSERT INTO dbo.Categories (CategoryName) VALUES (N'Fasteners');
IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE CategoryName = N'Paint')
    INSERT INTO dbo.Categories (CategoryName) VALUES (N'Paint');
IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE CategoryName = N'Plumbing')
    INSERT INTO dbo.Categories (CategoryName) VALUES (N'Plumbing');
IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE CategoryName = N'Electrical')
    INSERT INTO dbo.Categories (CategoryName) VALUES (N'Electrical');
GO

/* Suppliers */
IF NOT EXISTS (SELECT 1 FROM dbo.Suppliers WHERE CompanyName = N'Metro Build Supply')
BEGIN
    INSERT INTO dbo.Suppliers (CompanyName, ContactPerson, Phone, Email, Address, IsActive)
    VALUES (N'Metro Build Supply', N'Juan Dela Cruz', N'09171234567', N'sales@metrobuild.ph', N'123 Industrial Ave, Manila', 1);
END
IF NOT EXISTS (SELECT 1 FROM dbo.Suppliers WHERE CompanyName = N'Island Hardware Traders')
BEGIN
    INSERT INTO dbo.Suppliers (CompanyName, ContactPerson, Phone, Email, Address, IsActive)
    VALUES (N'Island Hardware Traders', N'Maria Santos', N'09189876543', N'orders@islandhw.ph', N'45 Warehouse Rd, Quezon City', 1);
END
GO

DECLARE @CatFastener INT = (SELECT CategoryId FROM dbo.Categories WHERE CategoryName = N'Fasteners');
DECLARE @CatPaint INT = (SELECT CategoryId FROM dbo.Categories WHERE CategoryName = N'Paint');
DECLARE @CatPlumbing INT = (SELECT CategoryId FROM dbo.Categories WHERE CategoryName = N'Plumbing');
DECLARE @CatElectrical INT = (SELECT CategoryId FROM dbo.Categories WHERE CategoryName = N'Electrical');
DECLARE @Sup1 INT = (SELECT TOP 1 SupplierId FROM dbo.Suppliers WHERE CompanyName = N'Metro Build Supply');
DECLARE @Sup2 INT = (SELECT TOP 1 SupplierId FROM dbo.Suppliers WHERE CompanyName = N'Island Hardware Traders');

/* Sample products */
IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Barcode = N'4801001000011')
BEGIN
    INSERT INTO dbo.Products (ProductName, ProductDetails, Barcode, UnitOfMeasure, CostPrice, SellingPrice, StockQty, ReorderLevel, CategoryId, SupplierId, IsArchived)
    VALUES (N'Common Nail 2 inch', N'Box of common nails, 2"', N'4801001000011', N'Box', 85.00, 120.00, 25, 10, @CatFastener, @Sup1, 0);
END

IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Barcode = N'4801001000028')
BEGIN
    INSERT INTO dbo.Products (ProductName, ProductDetails, Barcode, UnitOfMeasure, CostPrice, SellingPrice, StockQty, ReorderLevel, CategoryId, SupplierId, IsArchived)
    VALUES (N'Latex Paint White 4L', N'Interior latex paint, white', N'4801001000028', N'Piece', 450.00, 650.00, 8, 10, @CatPaint, @Sup2, 0);
END

IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Barcode = N'4801001000035')
BEGIN
    INSERT INTO dbo.Products (ProductName, ProductDetails, Barcode, UnitOfMeasure, CostPrice, SellingPrice, StockQty, ReorderLevel, CategoryId, SupplierId, IsArchived)
    VALUES (N'PVC Pipe 1/2 inch', N'Schedule 40 PVC pipe', N'4801001000035', N'Meter', 35.00, 55.00, 100, 20, @CatPlumbing, @Sup1, 0);
END

IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Barcode = N'4801001000042')
BEGIN
    INSERT INTO dbo.Products (ProductName, ProductDetails, Barcode, UnitOfMeasure, CostPrice, SellingPrice, StockQty, ReorderLevel, CategoryId, SupplierId, IsArchived)
    VALUES (N'Electrical Tape Black', N'PVC electrical insulation tape', N'4801001000042', N'Piece', 15.00, 25.00, 0, 15, @CatElectrical, @Sup2, 0);
END

IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Barcode = N'4801001000059')
BEGIN
    INSERT INTO dbo.Products (ProductName, ProductDetails, Barcode, UnitOfMeasure, CostPrice, SellingPrice, StockQty, ReorderLevel, CategoryId, SupplierId, IsArchived)
    VALUES (N'Wood Screw 1 inch', N'Phillips flat head wood screws', N'4801001000059', N'Box', 60.00, 95.00, 40, 12, @CatFastener, @Sup1, 0);
END
GO

PRINT N'Seed completed. Logins: admin/Password123 , cashier/Cashier@123';
GO
