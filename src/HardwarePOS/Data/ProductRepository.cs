using HardwarePOS.Models;
using Microsoft.Data.SqlClient;

namespace HardwarePOS.Data;

public class ProductRepository
{
    public List<Product> GetAll(string? search = null, bool includeArchived = false)
    {
        var list = new List<Product>();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT p.ProductId, p.ProductName, p.ProductDetails, p.Barcode, p.UnitOfMeasure,
                   p.CostPrice, p.SellingPrice, p.StockQty, p.ReorderLevel,
                   p.CategoryId, c.CategoryName, p.SupplierId, s.CompanyName, p.IsArchived
            FROM dbo.Products p
            LEFT JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
            LEFT JOIN dbo.Suppliers s ON s.SupplierId = p.SupplierId
            WHERE (@IncludeArchived = 1 OR p.IsArchived = 0)
              AND (
                    @Search IS NULL OR @Search = N''
                    OR p.ProductName LIKE N'%' + @Search + N'%'
                    OR p.Barcode LIKE N'%' + @Search + N'%'
                    OR p.ProductDetails LIKE N'%' + @Search + N'%'
                  )
            ORDER BY p.ProductName;
            """;
        cmd.Parameters.AddWithValue("@IncludeArchived", includeArchived ? 1 : 0);
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
            SELECT p.ProductId, p.ProductName, p.ProductDetails, p.Barcode, p.UnitOfMeasure,
                   p.CostPrice, p.SellingPrice, p.StockQty, p.ReorderLevel,
                   p.CategoryId, c.CategoryName, p.SupplierId, s.CompanyName, p.IsArchived
            FROM dbo.Products p
            LEFT JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
            LEFT JOIN dbo.Suppliers s ON s.SupplierId = p.SupplierId
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
            SELECT p.ProductId, p.ProductName, p.ProductDetails, p.Barcode, p.UnitOfMeasure,
                   p.CostPrice, p.SellingPrice, p.StockQty, p.ReorderLevel,
                   p.CategoryId, c.CategoryName, p.SupplierId, s.CompanyName, p.IsArchived
            FROM dbo.Products p
            LEFT JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
            LEFT JOIN dbo.Suppliers s ON s.SupplierId = p.SupplierId
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
                        (ProductName, ProductDetails, Barcode, UnitOfMeasure, CostPrice, SellingPrice,
                         StockQty, ReorderLevel, CategoryId, SupplierId, IsArchived)
                    VALUES
                        (@Name, @Details, @Barcode, @Uom, @Cost, @Sell, @Stock, @Reorder, @CategoryId, @SupplierId, 0);
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
                ProductName = @Name,
                ProductDetails = @Details,
                Barcode = @Barcode,
                UnitOfMeasure = @Uom,
                CostPrice = @Cost,
                SellingPrice = @Sell,
                ReorderLevel = @Reorder,
                CategoryId = @CategoryId,
                SupplierId = @SupplierId
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
        cmd.Parameters.AddWithValue("@Name", product.ProductName);
        cmd.Parameters.AddWithValue("@Details", (object?)product.ProductDetails ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Barcode", string.IsNullOrWhiteSpace(product.Barcode) ? DBNull.Value : product.Barcode);
        cmd.Parameters.AddWithValue("@Uom", product.UnitOfMeasure);
        cmd.Parameters.AddWithValue("@Cost", product.CostPrice);
        cmd.Parameters.AddWithValue("@Sell", product.SellingPrice);
        cmd.Parameters.AddWithValue("@Stock", product.StockQty);
        cmd.Parameters.AddWithValue("@Reorder", product.ReorderLevel);
        cmd.Parameters.AddWithValue("@CategoryId", (object?)product.CategoryId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SupplierId", (object?)product.SupplierId ?? DBNull.Value);
    }

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
        IsArchived = reader.GetBoolean(13)
    };
}
