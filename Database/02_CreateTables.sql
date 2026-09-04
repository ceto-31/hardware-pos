-- Hardware POS: Tables, constraints, indexes
USE HardwarePOS;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------- Roles ---------- */
IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Roles
    (
        RoleId   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
        RoleName NVARCHAR(50) NOT NULL CONSTRAINT UQ_Roles_RoleName UNIQUE
    );
END
GO

/* ---------- Users ---------- */
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        UserId       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
        Username     NVARCHAR(50)  NOT NULL CONSTRAINT UQ_Users_Username UNIQUE,
        PasswordHash VARBINARY(64) NOT NULL,
        PasswordSalt VARBINARY(32) NOT NULL,
        FullName     NVARCHAR(100) NOT NULL,
        RoleId       INT NOT NULL,
        IsActive     BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
        CreatedAt    DATETIME2(0) NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (RoleId)
    );
END
GO

/* ---------- AppSettings ---------- */
IF OBJECT_ID(N'dbo.AppSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppSettings
    (
        SettingKey   NVARCHAR(50)  NOT NULL CONSTRAINT PK_AppSettings PRIMARY KEY,
        SettingValue NVARCHAR(200) NOT NULL
    );
END
GO

/* ---------- Categories ---------- */
IF OBJECT_ID(N'dbo.Categories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Categories
    (
        CategoryId   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Categories PRIMARY KEY,
        CategoryName NVARCHAR(100) NOT NULL CONSTRAINT UQ_Categories_Name UNIQUE
    );
END
GO

/* ---------- Suppliers ---------- */
IF OBJECT_ID(N'dbo.Suppliers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Suppliers
    (
        SupplierId    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Suppliers PRIMARY KEY,
        CompanyName   NVARCHAR(150) NOT NULL,
        ContactPerson NVARCHAR(100) NULL,
        Phone         NVARCHAR(30)  NULL,
        Email         NVARCHAR(100) NULL,
        Address       NVARCHAR(300) NULL,
        IsActive      BIT NOT NULL CONSTRAINT DF_Suppliers_IsActive DEFAULT (1),
        CreatedAt     DATETIME2(0) NOT NULL CONSTRAINT DF_Suppliers_CreatedAt DEFAULT (SYSUTCDATETIME())
    );
END
GO

/* ---------- Products ---------- */
IF OBJECT_ID(N'dbo.Products', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Products
    (
        ProductId      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
        ProductName    NVARCHAR(150) NOT NULL,
        ProductDetails NVARCHAR(500) NULL,
        Barcode        NVARCHAR(50)  NULL,
        UnitOfMeasure  NVARCHAR(30)  NOT NULL CONSTRAINT DF_Products_UOM DEFAULT (N'Piece'),
        CostPrice      DECIMAL(18,2) NOT NULL CONSTRAINT DF_Products_Cost DEFAULT (0),
        SellingPrice   DECIMAL(18,2) NOT NULL CONSTRAINT DF_Products_Sell DEFAULT (0),
        StockQty       DECIMAL(18,3) NOT NULL CONSTRAINT DF_Products_Stock DEFAULT (0),
        ReorderLevel   DECIMAL(18,3) NOT NULL CONSTRAINT DF_Products_Reorder DEFAULT (10),
        CategoryId     INT NULL,
        SupplierId     INT NULL,
        ImagePath      NVARCHAR(260) NULL,
        ExpirationDate DATE NULL,
        SalePrice      DECIMAL(18,2) NULL,
        SaleStartDate  DATE NULL,
        SaleEndDate    DATE NULL,
        IsArchived     BIT NOT NULL CONSTRAINT DF_Products_IsArchived DEFAULT (0),
        CreatedAt      DATETIME2(0) NOT NULL CONSTRAINT DF_Products_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_Products_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories (CategoryId),
        CONSTRAINT FK_Products_Suppliers FOREIGN KEY (SupplierId) REFERENCES dbo.Suppliers (SupplierId),
        CONSTRAINT CK_Products_Prices CHECK (CostPrice >= 0 AND SellingPrice >= 0),
        CONSTRAINT CK_Products_Stock CHECK (StockQty >= 0)
    );

    CREATE UNIQUE INDEX UX_Products_Barcode
        ON dbo.Products (Barcode)
        WHERE Barcode IS NOT NULL AND Barcode <> N'';
END
GO

/* ---------- StockIns ---------- */
IF OBJECT_ID(N'dbo.StockIns', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StockIns
    (
        StockInId    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StockIns PRIMARY KEY,
        SupplierId   INT NOT NULL,
        ProductId    INT NOT NULL,
        Quantity     DECIMAL(18,3) NOT NULL,
        Cost         DECIMAL(18,2) NOT NULL,
        DateReceived DATE NOT NULL,
        Remarks      NVARCHAR(300) NULL,
        CreatedBy    INT NULL,
        CreatedAt    DATETIME2(0) NOT NULL CONSTRAINT DF_StockIns_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_StockIns_Suppliers FOREIGN KEY (SupplierId) REFERENCES dbo.Suppliers (SupplierId),
        CONSTRAINT FK_StockIns_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (ProductId),
        CONSTRAINT FK_StockIns_Users FOREIGN KEY (CreatedBy) REFERENCES dbo.Users (UserId),
        CONSTRAINT CK_StockIns_Qty CHECK (Quantity > 0),
        CONSTRAINT CK_StockIns_Cost CHECK (Cost >= 0)
    );
END
GO

/* ---------- StockOuts ---------- */
IF OBJECT_ID(N'dbo.StockOuts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StockOuts
    (
        StockOutId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StockOuts PRIMARY KEY,
        ProductId  INT NOT NULL,
        Quantity   DECIMAL(18,3) NOT NULL,
        Reason     NVARCHAR(50) NOT NULL,
        DateOut    DATE NOT NULL,
        Remarks    NVARCHAR(300) NULL,
        CreatedBy  INT NULL,
        CreatedAt  DATETIME2(0) NOT NULL CONSTRAINT DF_StockOuts_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_StockOuts_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (ProductId),
        CONSTRAINT FK_StockOuts_Users FOREIGN KEY (CreatedBy) REFERENCES dbo.Users (UserId),
        CONSTRAINT CK_StockOuts_Qty CHECK (Quantity > 0),
        CONSTRAINT CK_StockOuts_Reason CHECK (Reason IN (N'Damaged', N'ReturnedToSupplier', N'InternalUse'))
    );
END
GO

/* ---------- InventoryLedger ---------- */
IF OBJECT_ID(N'dbo.InventoryLedger', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InventoryLedger
    (
        LedgerId      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_InventoryLedger PRIMARY KEY,
        ProductId     INT NOT NULL,
        MovementType  NVARCHAR(10) NOT NULL,
        QtyChange     DECIMAL(18,3) NOT NULL,
        BalanceAfter  DECIMAL(18,3) NOT NULL,
        ReferenceId   INT NULL,
        Remarks       NVARCHAR(300) NULL,
        CreatedBy     INT NULL,
        CreatedAt     DATETIME2(0) NOT NULL CONSTRAINT DF_InventoryLedger_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_InventoryLedger_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (ProductId),
        CONSTRAINT FK_InventoryLedger_Users FOREIGN KEY (CreatedBy) REFERENCES dbo.Users (UserId),
        CONSTRAINT CK_InventoryLedger_Type CHECK (MovementType IN (N'IN', N'OUT', N'SALE'))
    );

    CREATE INDEX IX_InventoryLedger_Product_CreatedAt
        ON dbo.InventoryLedger (ProductId, CreatedAt DESC);
END
GO

/* ---------- Discounts ---------- */
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
GO

/* ---------- DiscountProducts ---------- */
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
GO

/* ---------- Sales ---------- */
IF OBJECT_ID(N'dbo.Sales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Sales
    (
        SaleId          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Sales PRIMARY KEY,
        InvoiceNo       NVARCHAR(30) NOT NULL CONSTRAINT UQ_Sales_InvoiceNo UNIQUE,
        SaleDate        DATETIME2(0) NOT NULL CONSTRAINT DF_Sales_SaleDate DEFAULT (SYSUTCDATETIME()),
        CashierId       INT NOT NULL,
        Subtotal        DECIMAL(18,2) NOT NULL,
        TaxAmount       DECIMAL(18,2) NOT NULL CONSTRAINT DF_Sales_Tax DEFAULT (0),
        DiscountAmount  DECIMAL(18,2) NOT NULL CONSTRAINT DF_Sales_Discount DEFAULT (0),
        TotalDue        DECIMAL(18,2) NOT NULL,
        CashTendered    DECIMAL(18,2) NOT NULL,
        ChangeAmount    DECIMAL(18,2) NOT NULL,
        CONSTRAINT FK_Sales_Users FOREIGN KEY (CashierId) REFERENCES dbo.Users (UserId),
        CONSTRAINT CK_Sales_Amounts CHECK (Subtotal >= 0 AND TaxAmount >= 0 AND DiscountAmount >= 0 AND TotalDue >= 0)
    );

    CREATE INDEX IX_Sales_SaleDate ON dbo.Sales (SaleDate DESC);
END
GO

/* ---------- SaleItems ---------- */
IF OBJECT_ID(N'dbo.SaleItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SaleItems
    (
        SaleItemId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SaleItems PRIMARY KEY,
        SaleId     INT NOT NULL,
        ProductId  INT NOT NULL,
        Quantity   DECIMAL(18,3) NOT NULL,
        UnitPrice  DECIMAL(18,2) NOT NULL,
        LineTotal  DECIMAL(18,2) NOT NULL,
        CONSTRAINT FK_SaleItems_Sales FOREIGN KEY (SaleId) REFERENCES dbo.Sales (SaleId),
        CONSTRAINT FK_SaleItems_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (ProductId),
        CONSTRAINT CK_SaleItems_Qty CHECK (Quantity > 0)
    );
END
GO
