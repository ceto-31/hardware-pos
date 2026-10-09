using HardwarePOS.Models;
using Microsoft.Data.SqlClient;

namespace HardwarePOS.Data;

public class ProductRepository
{
    public List<Product> GetAll(
        string? search = null,
        bool includeArchived = false,
        int? categoryId = null, int? unitId = null)
    {
        var list = new List<Product>();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT p.ProductId, p.ProductName, p.ProductDetails,
                   ISNULL(u.UnitName, p.UnitOfMeasure), p.CostPrice, p.SellingPrice,
                   p.StockQty, p.ReorderLevel, p.CategoryId, c.CategoryName,
                   p.SupplierId, s.CompanyName, p.IsArchived, p.ProductCode, p.UnitId, p.ImagePath, p.ExpirationDate,
                   p.SalePrice, p.SaleStartDate, p.SaleEndDate, p.Brand, p.Color, p.AllowsFractional,
                   bo.BoQty
            FROM dbo.Products p
            LEFT JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
            LEFT JOIN dbo.Suppliers s ON s.SupplierId = p.SupplierId
            LEFT JOIN dbo.Units u ON u.UnitId = p.UnitId
            OUTER APPLY (
                SELECT ISNULL(SUM(RemainingQuantity), 0) AS BoQty
                FROM dbo.BadStock
                WHERE ProductId = p.ProductId
            ) bo
            WHERE (@IncludeArchived = 1 OR p.IsArchived = 0)
              AND (@CategoryId IS NULL OR p.CategoryId = @CategoryId)
              AND (@UnitId IS NULL OR p.UnitId = @UnitId)
              AND (
                    @Search IS NULL OR @Search = N''
                    OR p.ProductCode LIKE N'%' + @Search + N'%'
                    OR p.ProductName LIKE N'%' + @Search + N'%'
                    OR p.Brand LIKE N'%' + @Search + N'%'
                    OR p.Color LIKE N'%' + @Search + N'%'
                    OR p.ProductDetails LIKE N'%' + @Search + N'%'
                  )
            ORDER BY p.IsArchived DESC, p.ProductName;
            """;
        cmd.Parameters.AddWithValue("@IncludeArchived", includeArchived);
        cmd.Parameters.AddWithValue("@CategoryId", (object?)categoryId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Search", (object?)search ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@UnitId", (object?)unitId ?? DBNull.Value);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(Map(reader));
        return list;
    }

    public Product? GetById(int productId)
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT p.ProductId, p.ProductName, p.ProductDetails,
                   ISNULL(u.UnitName, p.UnitOfMeasure), p.CostPrice, p.SellingPrice,
                   p.StockQty, p.ReorderLevel, p.CategoryId, c.CategoryName,
                   p.SupplierId, s.CompanyName, p.IsArchived, p.ProductCode, p.UnitId, p.ImagePath, p.ExpirationDate,
                   p.SalePrice, p.SaleStartDate, p.SaleEndDate, p.Brand, p.Color, p.AllowsFractional,
                   bo.BoQty
            FROM dbo.Products p
            LEFT JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
            LEFT JOIN dbo.Suppliers s ON s.SupplierId = p.SupplierId
            LEFT JOIN dbo.Units u ON u.UnitId = p.UnitId
            OUTER APPLY (
                SELECT ISNULL(SUM(RemainingQuantity), 0) AS BoQty
                FROM dbo.BadStock
                WHERE ProductId = p.ProductId
            ) bo
            WHERE p.ProductId = @Id;
            """;
        cmd.Parameters.AddWithValue("@Id", productId);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public int Insert(Product product)
    {
        Validate(product);
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        try
        {
            int productId;
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO dbo.Products
                        (ProductCode, ProductName, ProductDetails, UnitId, UnitOfMeasure,
                         CostPrice, SellingPrice, StockQty, ReorderLevel, CategoryId, SupplierId, IsArchived,
                         ExpirationDate, SalePrice, SaleStartDate, SaleEndDate, Brand, Color, AllowsFractional)
                    VALUES
                        (@ProductCode, @Name, @Details, @UnitId,
                         ISNULL((SELECT UnitName FROM dbo.Units WHERE UnitId = @UnitId), N'Piece'),
                         @Cost, @Sell, @Stock, @Reorder, @CategoryId, @SupplierId, 0,
                         @ExpirationDate, @SalePrice, @SaleStartDate, @SaleEndDate, @Brand, @Color, @Fractional);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);
                    """;
                AddParams(cmd, product);
                productId = (int)cmd.ExecuteScalar()!;
            }

            if (product.StockQty > 0)
            {
                using var ledger = conn.CreateCommand();
                ledger.Transaction = tx;
                ledger.CommandText = """
                    INSERT INTO dbo.InventoryLedger
                        (ProductId, MovementType, QtyChange, BalanceAfter, ReferenceId, Remarks, CreatedBy)
                    VALUES
                        (@ProductId, N'IN', @Qty, @Balance, NULL, N'Opening stock', NULL);
                    """;
                ledger.Parameters.AddWithValue("@ProductId", productId);
                ledger.Parameters.AddWithValue("@Qty", product.StockQty);
                ledger.Parameters.AddWithValue("@Balance", product.StockQty);
                ledger.ExecuteNonQuery();
                BatchStock.Add(conn, tx, productId, null, null, product.StockQty, DateTime.Now, product.ExpirationDate, "Opening stock — supplier/delivery unknown");
            }

            tx.Commit();
            return productId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public void Update(Product product)
    {
        Validate(product);
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            IF EXISTS(SELECT 1 FROM dbo.Products p WHERE p.ProductId=@Id AND
                (ISNULL(p.UnitId,0)<>ISNULL(@UnitId,0)) AND
                (EXISTS(SELECT 1 FROM dbo.InventoryLedger WHERE ProductId=@Id) OR EXISTS(SELECT 1 FROM dbo.SaleItems WHERE ProductId=@Id) OR EXISTS(SELECT 1 FROM dbo.InventoryBatches WHERE ProductId=@Id)))
                THROW 50121,'A product with stock history must keep its unit. Create a separate product for a different unit.',1;
            IF @Fractional=0 AND EXISTS(SELECT 1 FROM dbo.InventoryBatches WHERE ProductId=@Id AND RemainingQuantity<>FLOOR(RemainingQuantity))
                THROW 50122,'Fractional stock remains. Keep fractional quantities enabled.',1;
            IF @Fractional=0 AND EXISTS(SELECT 1 FROM dbo.SaleItems WHERE ProductId=@Id AND Quantity<>FLOOR(Quantity))
                THROW 50123,'This product has fractional sales. Keep fractional quantities enabled so returns retain their original units.',1;
            UPDATE dbo.Products SET
                ProductCode = @ProductCode,
                ProductName = @Name,
                ProductDetails = @Details, Brand=@Brand, Color=@Color, AllowsFractional=@Fractional,
                UnitId = @UnitId,
                UnitOfMeasure = ISNULL(
                    (SELECT UnitName FROM dbo.Units WHERE UnitId = @UnitId),
                    N'Piece'),
                CostPrice = @Cost,
                SellingPrice = @Sell,
                ReorderLevel = @Reorder,
                CategoryId = @CategoryId,
                SupplierId = @SupplierId,
                ImagePath = @ImagePath,
                ExpirationDate = @ExpirationDate,
                SalePrice = @SalePrice,
                SaleStartDate = @SaleStartDate,
                SaleEndDate = @SaleEndDate
            WHERE ProductId = @Id;
            """;
        AddParams(cmd, product);
        cmd.Parameters.AddWithValue("@Id", product.ProductId);
        cmd.ExecuteNonQuery();
    }

    private static void Validate(Product product)
    {
        BatchStock.RequireAdmin();
        if(string.IsNullOrWhiteSpace(product.ProductName)) throw new InvalidOperationException("Product name is required.");
        if(product.StockQty<0 || product.CostPrice<0 || product.SellingPrice<0 || product.ReorderLevel<0)
            throw new InvalidOperationException("Prices, stock, and reorder level cannot be negative.");
        if(product.StockQty>0) HardwarePOS.Services.InventoryRules.ValidateQuantity(product.StockQty,product.AllowsFractional);
    }

    public void Archive(int productId, bool archived = true)
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE dbo.Products SET IsArchived = @Archived WHERE ProductId = @Id;";
        cmd.Parameters.AddWithValue("@Archived", archived);
        cmd.Parameters.AddWithValue("@Id", productId);
        cmd.ExecuteNonQuery();
    }

    public bool HasMovementHistory(int productId)
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT CASE WHEN
                EXISTS (SELECT 1 FROM dbo.SaleItems WHERE ProductId = @Id)
                OR EXISTS (SELECT 1 FROM dbo.StockIns WHERE ProductId = @Id)
                OR EXISTS (SELECT 1 FROM dbo.StockOuts WHERE ProductId = @Id)
                OR EXISTS (SELECT 1 FROM dbo.InventoryLedger WHERE ProductId = @Id)
                OR EXISTS (SELECT 1 FROM dbo.InventoryBatches WHERE ProductId = @Id)
            THEN 1 ELSE 0 END;
            """;
        cmd.Parameters.AddWithValue("@Id", productId);
        return (int)cmd.ExecuteScalar()! == 1;
    }

    public void Delete(int productId)
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            IF EXISTS (SELECT 1 FROM dbo.SaleItems WHERE ProductId = @Id)
                OR EXISTS (SELECT 1 FROM dbo.StockIns WHERE ProductId = @Id)
                OR EXISTS (SELECT 1 FROM dbo.StockOuts WHERE ProductId = @Id)
                OR EXISTS (SELECT 1 FROM dbo.InventoryLedger WHERE ProductId = @Id)
                THROW 50020, 'This product has sales or stock history and cannot be deleted. Archive it instead so historical records stay intact.', 1;

            DELETE FROM dbo.DiscountProducts WHERE ProductId = @Id;
            DELETE FROM dbo.Products WHERE ProductId = @Id;
            """;
        cmd.Parameters.AddWithValue("@Id", productId);
        cmd.ExecuteNonQuery();
    }

    public void UpdateImagePath(int productId, string? imagePath)
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE dbo.Products SET ImagePath = @ImagePath WHERE ProductId = @Id;";
        cmd.Parameters.AddWithValue("@Id", productId);
        cmd.Parameters.AddWithValue("@ImagePath",
            string.IsNullOrWhiteSpace(imagePath) ? DBNull.Value : imagePath);
        cmd.ExecuteNonQuery();
    }

    public List<Category> GetCategories()
    {
        var list = new List<Category>();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT CategoryId, CategoryName FROM dbo.Categories ORDER BY CategoryName;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Category
            {
                CategoryId = reader.GetInt32(0),
                CategoryName = reader.GetString(1)
            });
        }
        return list;
    }

    private static void AddParams(SqlCommand cmd, Product product)
    {
        cmd.Parameters.AddWithValue(
            "@ProductCode",
            string.IsNullOrWhiteSpace(product.ProductCode) ? DBNull.Value : product.ProductCode.Trim());
        cmd.Parameters.AddWithValue("@Name", product.ProductName);
        cmd.Parameters.AddWithValue("@Details", (object?)product.ProductDetails ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Brand", (object?)product.Brand ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Color", (object?)product.Color ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Fractional", product.AllowsFractional);
        cmd.Parameters.AddWithValue("@UnitId", (object?)product.UnitId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Cost", product.CostPrice);
        cmd.Parameters.AddWithValue("@Sell", product.SellingPrice);
        cmd.Parameters.AddWithValue("@Stock", product.StockQty);
        cmd.Parameters.AddWithValue("@Reorder", product.ReorderLevel);
        cmd.Parameters.AddWithValue("@CategoryId", (object?)product.CategoryId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SupplierId", (object?)product.SupplierId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ImagePath",
            string.IsNullOrWhiteSpace(product.ImagePath) ? DBNull.Value : product.ImagePath);
        cmd.Parameters.AddWithValue("@ExpirationDate",
            product.ExpirationDate.HasValue ? product.ExpirationDate.Value.Date : DBNull.Value);
        cmd.Parameters.AddWithValue("@SalePrice", (object?)product.SalePrice ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SaleStartDate",
            product.SaleStartDate.HasValue ? product.SaleStartDate.Value.Date : DBNull.Value);
        cmd.Parameters.AddWithValue("@SaleEndDate",
            product.SaleEndDate.HasValue ? product.SaleEndDate.Value.Date : DBNull.Value);
    }

    private static DateTime? ReadOptionalDate(SqlDataReader reader, int index) =>
        reader.FieldCount > index && !reader.IsDBNull(index) ? reader.GetDateTime(index).Date : null;

    private static decimal? ReadOptionalDecimal(SqlDataReader reader, int index) =>
        reader.FieldCount > index && !reader.IsDBNull(index) ? reader.GetDecimal(index) : null;

    private static Product Map(SqlDataReader reader) => new()
    {
        ProductId = reader.GetInt32(0),
        ProductName = reader.GetString(1),
        ProductDetails = reader.IsDBNull(2) ? null : reader.GetString(2),
        UnitOfMeasure = reader.GetString(3),
        CostPrice = reader.GetDecimal(4),
        SellingPrice = reader.GetDecimal(5),
        StockQty = reader.GetDecimal(6),
        ReorderLevel = reader.GetDecimal(7),
        CategoryId = reader.IsDBNull(8) ? null : reader.GetInt32(8),
        CategoryName = reader.IsDBNull(9) ? null : reader.GetString(9),
        SupplierId = reader.IsDBNull(10) ? null : reader.GetInt32(10),
        SupplierName = reader.IsDBNull(11) ? null : reader.GetString(11),
        IsArchived = reader.GetBoolean(12),
        ProductCode = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
        UnitId = reader.IsDBNull(14) ? null : reader.GetInt32(14),
        ImagePath = reader.FieldCount > 15 && !reader.IsDBNull(15) ? reader.GetString(15) : null,
        ExpirationDate = ReadOptionalDate(reader, 16),
        SalePrice = ReadOptionalDecimal(reader, 17),
        SaleStartDate = ReadOptionalDate(reader, 18),
        SaleEndDate = ReadOptionalDate(reader, 19),
        Brand=reader.IsDBNull(20)?null:reader.GetString(20), Color=reader.IsDBNull(21)?null:reader.GetString(21), AllowsFractional=reader.GetBoolean(22),
        BoQty = reader.FieldCount > 23 && !reader.IsDBNull(23) ? reader.GetDecimal(23) : 0
    };
}
