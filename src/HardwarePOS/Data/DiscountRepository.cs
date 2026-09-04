using HardwarePOS.Models;
using Microsoft.Data.SqlClient;

namespace HardwarePOS.Data;

public class DiscountRepository
{
    public List<Discount> GetAll(string? search = null, bool includeArchived = false)
    {
        var list = new List<Discount>();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT d.DiscountId, d.DiscountName, d.ApplyScope, d.DiscountType, d.DiscountValue,
                   d.CategoryId, c.CategoryName, d.StartDate, d.EndDate, d.IsArchived, d.CreatedAt,
                   (SELECT COUNT(*) FROM dbo.DiscountProducts dp WHERE dp.DiscountId = d.DiscountId) AS ProductCount
            FROM dbo.Discounts d
            LEFT JOIN dbo.Categories c ON c.CategoryId = d.CategoryId
            WHERE (@IncludeArchived = 1 OR d.IsArchived = 0)
              AND (
                    @Search IS NULL OR @Search = N''
                    OR d.DiscountName LIKE N'%' + @Search + N'%'
                    OR c.CategoryName LIKE N'%' + @Search + N'%'
                  )
            ORDER BY d.StartDate DESC, d.DiscountName;
            """;
        cmd.Parameters.AddWithValue("@IncludeArchived", includeArchived);
        cmd.Parameters.AddWithValue("@Search", (object?)search ?? DBNull.Value);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(Map(reader));
        return list;
    }

    public Discount? GetById(int discountId)
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT d.DiscountId, d.DiscountName, d.ApplyScope, d.DiscountType, d.DiscountValue,
                   d.CategoryId, c.CategoryName, d.StartDate, d.EndDate, d.IsArchived, d.CreatedAt,
                   (SELECT COUNT(*) FROM dbo.DiscountProducts dp WHERE dp.DiscountId = d.DiscountId) AS ProductCount
            FROM dbo.Discounts d
            LEFT JOIN dbo.Categories c ON c.CategoryId = d.CategoryId
            WHERE d.DiscountId = @Id;
            """;
        cmd.Parameters.AddWithValue("@Id", discountId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        var discount = Map(reader);
        discount.ProductIds = GetProductIds(discountId);
        return discount;
    }

    public List<int> GetProductIds(int discountId)
    {
        var ids = new List<int>();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ProductId FROM dbo.DiscountProducts WHERE DiscountId = @Id ORDER BY ProductId;";
        cmd.Parameters.AddWithValue("@Id", discountId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            ids.Add(reader.GetInt32(0));
        return ids;
    }

    public List<Discount> GetDashboardDiscounts(int limit = 6)
    {
        var list = new List<Discount>();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT TOP (@Limit)
                   d.DiscountId, d.DiscountName, d.ApplyScope, d.DiscountType, d.DiscountValue,
                   d.CategoryId, c.CategoryName, d.StartDate, d.EndDate, d.IsArchived, d.CreatedAt,
                   (SELECT COUNT(*) FROM dbo.DiscountProducts dp WHERE dp.DiscountId = d.DiscountId) AS ProductCount
            FROM dbo.Discounts d
            LEFT JOIN dbo.Categories c ON c.CategoryId = d.CategoryId
            WHERE d.IsArchived = 0
              AND d.EndDate >= CAST(SYSDATETIME() AS DATE)
              AND (
                    d.StartDate <= CAST(SYSDATETIME() AS DATE)
                    OR d.StartDate <= DATEADD(DAY, 30, CAST(SYSDATETIME() AS DATE))
                  )
            ORDER BY CASE WHEN d.StartDate <= CAST(SYSDATETIME() AS DATE) THEN 0 ELSE 1 END,
                     d.StartDate, d.DiscountName;
            """;
        cmd.Parameters.AddWithValue("@Limit", limit);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(Map(reader));
        return list;
    }

    public List<Discount> GetActiveRulesForPos(DateTime date)
    {
        var list = new List<Discount>();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT d.DiscountId, d.DiscountName, d.ApplyScope, d.DiscountType, d.DiscountValue,
                   d.CategoryId, c.CategoryName, d.StartDate, d.EndDate, d.IsArchived, d.CreatedAt,
                   (SELECT COUNT(*) FROM dbo.DiscountProducts dp WHERE dp.DiscountId = d.DiscountId) AS ProductCount
            FROM dbo.Discounts d
            LEFT JOIN dbo.Categories c ON c.CategoryId = d.CategoryId
            WHERE d.IsArchived = 0
              AND d.StartDate <= @Date
              AND d.EndDate >= @Date
            ORDER BY d.DiscountId;
            """;
        cmd.Parameters.AddWithValue("@Date", date.Date);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(Map(reader));
        reader.Close();

        foreach (var discount in list.Where(d => d.ApplyScope == "Product"))
            discount.ProductIds = GetProductIds(conn, discount.DiscountId);

        return list;
    }

    public int Insert(Discount discount, IEnumerable<int> productIds)
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO dbo.Discounts
                    (DiscountName, ApplyScope, DiscountType, DiscountValue, CategoryId, StartDate, EndDate, IsArchived)
                VALUES
                    (@Name, @Scope, @Type, @Value, @CategoryId, @Start, @End, 0);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """;
            AddParams(cmd, discount);
            var id = (int)cmd.ExecuteScalar()!;
            SaveProductLinks(conn, tx, id, productIds);
            tx.Commit();
            return id;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public void Update(Discount discount, IEnumerable<int> productIds)
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                UPDATE dbo.Discounts SET
                    DiscountName = @Name,
                    ApplyScope = @Scope,
                    DiscountType = @Type,
                    DiscountValue = @Value,
                    CategoryId = @CategoryId,
                    StartDate = @Start,
                    EndDate = @End
                WHERE DiscountId = @Id;
                """;
            AddParams(cmd, discount);
            cmd.Parameters.AddWithValue("@Id", discount.DiscountId);
            cmd.ExecuteNonQuery();

            using var del = conn.CreateCommand();
            del.Transaction = tx;
            del.CommandText = "DELETE FROM dbo.DiscountProducts WHERE DiscountId = @Id;";
            del.Parameters.AddWithValue("@Id", discount.DiscountId);
            del.ExecuteNonQuery();

            SaveProductLinks(conn, tx, discount.DiscountId, productIds);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public void Archive(int discountId, bool archived = true)
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE dbo.Discounts SET IsArchived = @Archived WHERE DiscountId = @Id;";
        cmd.Parameters.AddWithValue("@Archived", archived);
        cmd.Parameters.AddWithValue("@Id", discountId);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int discountId)
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM dbo.Discounts WHERE DiscountId = @Id;";
        cmd.Parameters.AddWithValue("@Id", discountId);
        cmd.ExecuteNonQuery();
    }

    private static void SaveProductLinks(SqlConnection conn, SqlTransaction tx, int discountId, IEnumerable<int> productIds)
    {
        foreach (var productId in productIds.Distinct())
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO dbo.DiscountProducts (DiscountId, ProductId) VALUES (@DiscountId, @ProductId);";
            cmd.Parameters.AddWithValue("@DiscountId", discountId);
            cmd.Parameters.AddWithValue("@ProductId", productId);
            cmd.ExecuteNonQuery();
        }
    }

    private static List<int> GetProductIds(SqlConnection conn, int discountId)
    {
        var ids = new List<int>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ProductId FROM dbo.DiscountProducts WHERE DiscountId = @Id;";
        cmd.Parameters.AddWithValue("@Id", discountId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            ids.Add(reader.GetInt32(0));
        return ids;
    }

    private static void AddParams(SqlCommand cmd, Discount discount)
    {
        cmd.Parameters.AddWithValue("@Name", discount.DiscountName.Trim());
        cmd.Parameters.AddWithValue("@Scope", discount.ApplyScope);
        cmd.Parameters.AddWithValue("@Type", discount.DiscountType);
        cmd.Parameters.AddWithValue("@Value", discount.DiscountValue);
        cmd.Parameters.AddWithValue("@CategoryId", (object?)discount.CategoryId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Start", discount.StartDate.Date);
        cmd.Parameters.AddWithValue("@End", discount.EndDate.Date);
    }

    private static Discount Map(SqlDataReader reader) => new()
    {
        DiscountId = reader.GetInt32(0),
        DiscountName = reader.GetString(1),
        ApplyScope = reader.GetString(2),
        DiscountType = reader.GetString(3),
        DiscountValue = reader.GetDecimal(4),
        CategoryId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
        CategoryName = reader.IsDBNull(6) ? null : reader.GetString(6),
        StartDate = reader.GetDateTime(7),
        EndDate = reader.GetDateTime(8),
        IsArchived = reader.GetBoolean(9),
        CreatedAt = reader.GetDateTime(10),
        ProductCount = reader.GetInt32(11)
    };
}

public static class DiscountPricing
{
    public static decimal ResolveUnitPrice(Product product, IReadOnlyList<Discount> rules)
    {
        var masterPrice = TryResolveMasterPrice(product, rules);
        if (masterPrice.HasValue)
            return masterPrice.Value;
        return product.EffectivePrice;
    }

    public static decimal ComputeStoreWideDiscount(decimal subtotal, IReadOnlyList<Discount> rules)
    {
        if (subtotal <= 0) return 0;

        decimal bestDiscount = 0;
        foreach (var rule in rules.Where(r => r.ApplyScope == "Store"))
        {
            var amount = rule.DiscountType switch
            {
                "PercentOff" => Math.Round(subtotal * rule.DiscountValue / 100m, 2),
                "FixedAmount" => Math.Min(subtotal, rule.DiscountValue),
                _ => 0m
            };
            bestDiscount = Math.Max(bestDiscount, amount);
        }
        return bestDiscount;
    }

    private static decimal? TryResolveMasterPrice(Product product, IReadOnlyList<Discount> rules)
    {
        decimal? bestPrice = null;

        foreach (var rule in rules.Where(r => r.ApplyScope == "Product" && r.ProductIds.Contains(product.ProductId)))
            bestPrice = MinPrice(bestPrice, ApplyLineDiscount(product.SellingPrice, rule));

        if (product.CategoryId.HasValue)
        {
            foreach (var rule in rules.Where(r => r.ApplyScope == "Category" && r.CategoryId == product.CategoryId))
                bestPrice = MinPrice(bestPrice, ApplyLineDiscount(product.SellingPrice, rule));
        }

        return bestPrice;
    }

    private static decimal ApplyLineDiscount(decimal basePrice, Discount rule) =>
        rule.DiscountType switch
        {
            "PercentOff" => Math.Round(basePrice * (1 - rule.DiscountValue / 100m), 2),
            "SalePrice" => rule.DiscountValue,
            _ => basePrice
        };

    private static decimal? MinPrice(decimal? current, decimal candidate) =>
        current.HasValue ? Math.Min(current.Value, candidate) : candidate;
}
