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

    private static void Execute(SqlConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
