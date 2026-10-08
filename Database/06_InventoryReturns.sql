-- Additive upgrade. Run after 05_UpgradeSchema.sql against HardwarePOS.
-- Existing balances have unknown delivery provenance; never infer a supplier.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @lockResult int;
EXEC @lockResult = sys.sp_getapplock @Resource=N'HardwarePOS.InventoryReturns.v1', @LockMode=N'Exclusive', @LockOwner=N'Transaction';
IF @lockResult < 0 THROW 50100, 'Could not lock the inventory upgrade.', 1;
GO
IF COL_LENGTH('dbo.Products','Brand') IS NULL ALTER TABLE dbo.Products ADD Brand nvarchar(100) NULL;
IF COL_LENGTH('dbo.Products','Color') IS NULL ALTER TABLE dbo.Products ADD Color nvarchar(100) NULL;
IF COL_LENGTH('dbo.Products','AllowsFractional') IS NULL
BEGIN
    ALTER TABLE dbo.Products ADD AllowsFractional bit NOT NULL CONSTRAINT DF_Products_Fractional DEFAULT(0);
END;
GO
IF NOT EXISTS (SELECT 1 FROM dbo.AppSettings WHERE SettingKey=N'InventoryReturnsV1')
    UPDATE dbo.Products SET AllowsFractional=1
    WHERE LOWER(UnitOfMeasure) IN ('kg','kilogram','kilograms','kilo','kilos','meter','meters','metre','m','foot','feet','ft','liter','litre','l')
       OR StockQty <> FLOOR(StockQty);

IF OBJECT_ID('dbo.InventoryBatches') IS NULL
CREATE TABLE dbo.InventoryBatches (
    BatchId int IDENTITY PRIMARY KEY,
    ProductId int NOT NULL REFERENCES dbo.Products(ProductId),
    SupplierId int NULL REFERENCES dbo.Suppliers(SupplierId),
    StockInId int NULL REFERENCES dbo.StockIns(StockInId),
    ReceivedAt datetime2(0) NOT NULL,
    ExpirationDate date NULL,
    OriginalQuantity decimal(18,3) NOT NULL CHECK(OriginalQuantity>=0),
    RemainingQuantity decimal(18,3) NOT NULL CHECK(RemainingQuantity>=0),
    SourceLabel nvarchar(200) NOT NULL,
    CONSTRAINT UQ_InventoryBatches_StockIn UNIQUE (StockInId, BatchId)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_InventoryBatches_FIFO' AND object_id=OBJECT_ID('dbo.InventoryBatches'))
CREATE INDEX IX_InventoryBatches_FIFO ON dbo.InventoryBatches(ProductId,ReceivedAt,BatchId) INCLUDE(RemainingQuantity,SupplierId);

IF OBJECT_ID('dbo.SaleAllocations') IS NULL
CREATE TABLE dbo.SaleAllocations (
    AllocationId int IDENTITY PRIMARY KEY,
    SaleItemId int NOT NULL REFERENCES dbo.SaleItems(SaleItemId),
    BatchId int NOT NULL REFERENCES dbo.InventoryBatches(BatchId),
    Quantity decimal(18,3) NOT NULL CHECK(Quantity>0),
    ReturnedQuantity decimal(18,3) NOT NULL DEFAULT(0),
    CHECK(ReturnedQuantity>=0 AND ReturnedQuantity<=Quantity)
);
IF OBJECT_ID('dbo.CustomerReturns') IS NULL
CREATE TABLE dbo.CustomerReturns (
    ReturnId int IDENTITY PRIMARY KEY,
    SaleItemId int NOT NULL REFERENCES dbo.SaleItems(SaleItemId),
    Quantity decimal(18,3) NOT NULL CHECK(Quantity>0),
    RefundAmount decimal(18,2) NOT NULL CHECK(RefundAmount>=0),
    ItemCondition nvarchar(20) NOT NULL CHECK(ItemCondition IN ('Usable','Defective')),
    Reason nvarchar(300) NOT NULL,
    CreatedBy int NOT NULL REFERENCES dbo.Users(UserId),
    CreatedAt datetime2(0) NOT NULL DEFAULT(SYSDATETIME())
);
IF OBJECT_ID('dbo.BadStock') IS NULL
CREATE TABLE dbo.BadStock (
    BadStockId int IDENTITY PRIMARY KEY,
    ProductId int NOT NULL REFERENCES dbo.Products(ProductId),
    BatchId int NULL REFERENCES dbo.InventoryBatches(BatchId),
    ReturnId int NULL REFERENCES dbo.CustomerReturns(ReturnId),
    Quantity decimal(18,3) NOT NULL CHECK(Quantity>0),
    RemainingQuantity decimal(18,3) NOT NULL,
    Reason nvarchar(300) NOT NULL,
    CreatedBy int NULL REFERENCES dbo.Users(UserId),
    CreatedAt datetime2(0) NOT NULL DEFAULT(SYSDATETIME()),
    CHECK(RemainingQuantity>=0 AND RemainingQuantity<=Quantity)
);
IF OBJECT_ID('dbo.BadStockDispositions') IS NULL
CREATE TABLE dbo.BadStockDispositions (
    DispositionId int IDENTITY PRIMARY KEY,
    BadStockId int NOT NULL REFERENCES dbo.BadStock(BadStockId),
    Quantity decimal(18,3) NOT NULL CHECK(Quantity>0),
    Action nvarchar(30) NOT NULL CHECK(Action IN ('Disposed','ReturnedToSupplier')),
    Reason nvarchar(300) NOT NULL,
    CreatedBy int NOT NULL REFERENCES dbo.Users(UserId),
    CreatedAt datetime2(0) NOT NULL DEFAULT(SYSDATETIME())
);
IF OBJECT_ID('dbo.StockOutAllocations') IS NULL
CREATE TABLE dbo.StockOutAllocations (
    StockOutId int NOT NULL REFERENCES dbo.StockOuts(StockOutId),
    BatchId int NOT NULL REFERENCES dbo.InventoryBatches(BatchId),
    Quantity decimal(18,3) NOT NULL CHECK(Quantity>0),
    PRIMARY KEY(StockOutId,BatchId)
);
GO
IF COL_LENGTH('dbo.SaleItems','UnitSnapshot') IS NULL ALTER TABLE dbo.SaleItems ADD UnitSnapshot nvarchar(30) NULL;
IF COL_LENGTH('dbo.SaleItems','NameSnapshot') IS NULL ALTER TABLE dbo.SaleItems ADD NameSnapshot nvarchar(300) NULL;
IF COL_LENGTH('dbo.SaleItems','RefundableAmount') IS NULL ALTER TABLE dbo.SaleItems ADD RefundableAmount decimal(18,2) NULL;
GO
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_InventoryLedger_Type')
ALTER TABLE dbo.InventoryLedger DROP CONSTRAINT CK_InventoryLedger_Type;
ALTER TABLE dbo.InventoryLedger ADD CONSTRAINT CK_InventoryLedger_Type CHECK (MovementType IN ('IN','OUT','SALE','VOID','RETURN','BO'));

IF NOT EXISTS (SELECT 1 FROM dbo.AppSettings WHERE SettingKey=N'InventoryReturnsV1')
BEGIN
    INSERT dbo.InventoryBatches(ProductId,SupplierId,ReceivedAt,ExpirationDate,OriginalQuantity,RemainingQuantity,SourceLabel)
    SELECT ProductId,NULL,CreatedAt,ExpirationDate,StockQty,StockQty,N'Existing stock — supplier/delivery unknown'
    FROM dbo.Products WHERE StockQty>0;
    INSERT dbo.AppSettings(SettingKey,SettingValue) VALUES(N'InventoryReturnsV1',N'1');
END;
GO
CREATE OR ALTER VIEW dbo.SaleLineFacts AS
WITH lines AS (
    SELECT si.*,s.InvoiceNo,s.SaleDate,s.IsVoided,s.TotalDue,
        SUM(si.LineTotal) OVER(PARTITION BY si.SaleId) AS LineSum,
        SUM(si.LineTotal) OVER(PARTITION BY si.SaleId ORDER BY si.SaleItemId ROWS UNBOUNDED PRECEDING) AS Cumulative
    FROM dbo.SaleItems si JOIN dbo.Sales s ON s.SaleId=si.SaleId
)
SELECT *, CAST(COALESCE(RefundableAmount,
    COALESCE(ROUND(TotalDue*CAST(Cumulative AS decimal(18,2))/NULLIF(CAST(LineSum AS decimal(18,2)),0),2)
           -ROUND(TotalDue*CAST(Cumulative-LineTotal AS decimal(18,2))/NULLIF(CAST(LineSum AS decimal(18,2)),0),2),0)) AS decimal(18,2)) AS PaidAmount
FROM lines;
GO
CREATE OR ALTER VIEW dbo.SalesMovements AS
SELECT SaleDate AS EventDate,TotalDue AS Amount,TotalDue AS SalesAmount,CAST(0 AS decimal(18,2)) AS RefundAmount,1 AS TransactionCount
FROM dbo.Sales WHERE IsVoided=0
UNION ALL
SELECT r.CreatedAt,-r.RefundAmount,CAST(0 AS decimal(18,2)),r.RefundAmount,0
FROM dbo.CustomerReturns r JOIN dbo.SaleItems si ON si.SaleItemId=r.SaleItemId JOIN dbo.Sales s ON s.SaleId=si.SaleId WHERE s.IsVoided=0;
GO
COMMIT TRANSACTION;
