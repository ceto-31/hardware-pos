using HardwarePOS.Models;
using Microsoft.Data.SqlClient;

namespace HardwarePOS.Data;

public class ProductRepository
{
    public List<Product> GetAll(
        string? search = null,
        bool includeArchived = false,
        int? categoryId = null)
    {
        var list = new List<Product>();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT p.ProductId, p.ProductName, p.ProductDetails, p.Barcode,
                   ISNULL(u.UnitName, p.UnitOfMeasure), p.CostPrice, p.SellingPrice,
                   p.StockQty, p.ReorderLevel, p.CategoryId, c.CategoryName,
                   p.SupplierId, s.CompanyName, p.IsArchived, p.ProductCode, p.UnitId, p.ImagePath, p.ExpirationDate,
                   p.SalePrice, p.SaleStartDate, p.SaleEndDate
            FROM dbo.Products p
            LEFT JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
            LEFT JOIN dbo.Suppliers s ON s.SupplierId = p.SupplierId
            LEFT JOIN dbo.Units u ON u.UnitId = p.UnitId
            WHERE (@IncludeArchived = 1 OR p.IsArchived = 0)
              AND (@CategoryId IS NULL OR p.CategoryId = @CategoryId)
              AND (
                    @Search IS NULL OR @Search = N''
                    OR p.ProductCode LIKE N'%' + @Search + N'%'
                    OR p.ProductName LIKE N'%' + @Search + N'%'
                    OR p.Barcode LIKE N'%' + @Search + N'%'
                    OR p.ProductDetails LIKE N'%' + @Search + N'%'
                  )
            ORDER BY p.ProductName;
            """;
        cmd.Parameters.AddWithValue("@IncludeArchived", includeArchived);
        cmd.Parameters.AddWithValue("@CategoryId", (object?)categoryId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Search", (object?)search ?? DBNull.Value);

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
            SELECT p.ProductId, p.ProductName, p.ProductDetails, p.Barcode,
                   ISNULL(u.UnitName, p.UnitOfMeasure), p.CostPrice, p.SellingPrice,
                   p.StockQty, p.ReorderLevel, p.CategoryId, c.CategoryName,
                   p.SupplierId, s.CompanyName, p.IsArchived, p.ProductCode, p.UnitId, p.ImagePath, p.ExpirationDate,
                   p.SalePrice, p.SaleStartDate, p.SaleEndDate
            FROM dbo.Products p
            LEFT JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
            LEFT JOIN dbo.Suppliers s ON s.SupplierId = p.SupplierId
            LEFT JOIN dbo.Units u ON u.UnitId = p.UnitId
            WHERE p.ProductId = @Id;
            """;
        cmd.Parameters.AddWithValue("@Id", productId);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public Product? GetByBarcode(string barcode)
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT p.ProductId, p.ProductName, p.ProductDetails, p.Barcode,
                   ISNULL(u.UnitName, p.UnitOfMeasure), p.CostPrice, p.SellingPrice,
                   p.StockQty, p.ReorderLevel, p.CategoryId, c.CategoryName,
                   p.SupplierId, s.CompanyName, p.IsArchived, p.ProductCode, p.UnitId, p.ImagePath, p.ExpirationDate,
                   p.SalePrice, p.SaleStartDate, p.SaleEndDate
            FROM dbo.Products p
            LEFT JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
            LEFT JOIN dbo.Suppliers s ON s.SupplierId = p.SupplierId
            LEFT JOIN dbo.Units u ON u.UnitId = p.UnitId
            WHERE p.Barcode = @Barcode AND p.IsArchived = 0;
            """;
        cmd.Parameters.AddWithValue("@Barcode", barcode);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public int Insert(Product product)
    {
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
                        (ProductCode, ProductName, ProductDetails, Barcode, UnitId, UnitOfMeasure,
                         CostPrice, SellingPrice, StockQty, ReorderLevel, CategoryId, SupplierId, IsArchived,
                         ExpirationDate, SalePrice, SaleStartDate, SaleEndDate)
                    VALUES
                        (@ProductCode, @Name, @Details, @Barcode, @UnitId,
                         ISNULL((SELECT UnitName FROM dbo.Units WHERE UnitId = @UnitId), N'Piece'),
                         @Cost, @Sell, @Stock, @Reorder, @CategoryId, @SupplierId, 0,
                         @ExpirationDate, @SalePrice, @SaleStartDate, @SaleEndDate);
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
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE dbo.Products SET
                ProductCode = @ProductCode,
                ProductName = @Name,
                ProductDetails = @Details,
                Barcode = @Barcode,
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
                UPDATE dbo.Products SET IsArchived = 1 WHERE ProductId = @Id;
            ELSE
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
        cmd.Parameters.AddWithValue(
            "@Barcode",
            string.IsNullOrWhiteSpace(product.Barcode) ? DBNull.Value : product.Barcode);
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
        Barcode = reader.IsDBNull(3) ? null : reader.GetString(3),
        UnitOfMeasure = reader.GetString(4),
        CostPrice = reader.GetDecimal(5),
        SellingPrice = reader.GetDecimal(6),
        StockQty = reader.GetDecimal(7),
        ReorderLevel = reader.GetDecimal(8),
        CategoryId = reader.IsDBNull(9) ? null : reader.GetInt32(9),
        CategoryName = reader.IsDBNull(10) ? null : reader.GetString(10),
        SupplierId = reader.IsDBNull(11) ? null : reader.GetInt32(11),
        SupplierName = reader.IsDBNull(12) ? null : reader.GetString(12),
        IsArchived = reader.GetBoolean(13),
        ProductCode = reader.IsDBNull(14) ? string.Empty : reader.GetString(14),
        UnitId = reader.IsDBNull(15) ? null : reader.GetInt32(15),
        ImagePath = reader.FieldCount > 16 && !reader.IsDBNull(16) ? reader.GetString(16) : null,
        ExpirationDate = ReadOptionalDate(reader, 17),
        SalePrice = ReadOptionalDecimal(reader, 18),
        SaleStartDate = ReadOptionalDate(reader, 19),
        SaleEndDate = ReadOptionalDate(reader, 20)
    };
}
