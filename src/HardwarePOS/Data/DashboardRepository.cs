using HardwarePOS.Models;
using Microsoft.Data.SqlClient;

namespace HardwarePOS.Data;

public class DashboardRepository
{
    public DashboardSummary GetSummary()
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                ISNULL((SELECT SUM(TotalDue) FROM dbo.Sales WHERE CAST(SaleDate AS DATE) = CAST(SYSDATETIME() AS DATE)), 0),
                ISNULL((SELECT COUNT(*) FROM dbo.Sales WHERE CAST(SaleDate AS DATE) = CAST(SYSDATETIME() AS DATE)), 0),
                ISNULL((SELECT SUM(TotalDue) FROM dbo.Sales), 0),
                ISNULL((SELECT COUNT(*) FROM dbo.Products WHERE IsArchived = 0), 0),
                ISNULL((SELECT COUNT(*) FROM dbo.Suppliers WHERE IsArchived = 0), 0),
                ISNULL((SELECT COUNT(*) FROM dbo.Products WHERE IsArchived = 0 AND StockQty > 0 AND StockQty <= ReorderLevel), 0),
                ISNULL((SELECT COUNT(*) FROM dbo.Products WHERE IsArchived = 0 AND StockQty <= 0), 0);
            """;
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return new DashboardSummary
        {
            TodaySales = reader.GetDecimal(0),
            TodayTransactions = reader.GetInt32(1),
            TotalRevenue = reader.GetDecimal(2),
            TotalProducts = reader.GetInt32(3),
            TotalSuppliers = reader.GetInt32(4),
            LowStockCount = reader.GetInt32(5),
            OutOfStockCount = reader.GetInt32(6)
        };
    }

    public List<int> GetAvailableYears()
    {
        var years = new List<int>();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT DISTINCT YEAR(SaleDate) FROM dbo.Sales
            UNION SELECT YEAR(SYSDATETIME())
            ORDER BY 1 DESC;
            """;
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) years.Add(reader.GetInt32(0));
        if (years.Count == 0) years.Add(DateTime.Now.Year);
        return years;
    }

    public List<SalesPoint> GetDailySales(int year, int days = 14)
    {
        return QueryPoints("""
            ;WITH Days AS (
                SELECT CAST(DATEADD(DAY, -v.number, CAST(SYSDATETIME() AS DATE)) AS DATE) AS SaleDay
                FROM (VALUES (0),(1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12),(13),(14),(15),(16),(17),(18),(19),(20),(21),(22),(23),(24),(25),(26),(27),(28),(29),(30)) v(number)
                WHERE v.number < @Days
            )
            SELECT d.SaleDay, ISNULL(SUM(s.TotalDue), 0)
            FROM Days d
            LEFT JOIN dbo.Sales s ON CAST(s.SaleDate AS DATE) = d.SaleDay AND YEAR(s.SaleDate) = @Year
            GROUP BY d.SaleDay ORDER BY d.SaleDay;
            """, ("@Days", days), ("@Year", year), d => d.ToString("MMM dd"));
    }

    public List<SalesPoint> GetWeeklySales(int year, int weeks = 12)
    {
        return QueryPoints("""
            ;WITH Weeks AS (
                SELECT DATEADD(WEEK, -v.number, DATEADD(WEEK, DATEDIFF(WEEK, 0, SYSDATETIME()), 0)) AS WeekStart
                FROM (VALUES (0),(1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12),(13),(14),(15)) v(number)
                WHERE v.number < @Weeks
            )
            SELECT w.WeekStart, ISNULL(SUM(s.TotalDue), 0)
            FROM Weeks w
            LEFT JOIN dbo.Sales s ON s.SaleDate >= w.WeekStart AND s.SaleDate < DATEADD(WEEK, 1, w.WeekStart) AND YEAR(s.SaleDate) = @Year
            GROUP BY w.WeekStart ORDER BY w.WeekStart;
            """, ("@Weeks", weeks), ("@Year", year), d => d.ToString("MMM dd"));
    }

    public List<SalesPoint> GetMonthlySales(int year)
    {
        return QueryPoints("""
            ;WITH Months AS (
                SELECT DATEFROMPARTS(@Year, v.number, 1) AS MonthStart
                FROM (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) v(number)
            )
            SELECT m.MonthStart, ISNULL(SUM(s.TotalDue), 0)
            FROM Months m
            LEFT JOIN dbo.Sales s ON s.SaleDate >= m.MonthStart AND s.SaleDate < DATEADD(MONTH, 1, m.MonthStart)
            GROUP BY m.MonthStart ORDER BY m.MonthStart;
            """, ("@Year", year), ("@Dummy", 0), d => d.ToString("MMM"));
    }

    public List<SalesPoint> GetYearlySales()
    {
        var list = new List<SalesPoint>();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            ;WITH Years AS (
                SELECT YEAR(SYSDATETIME()) - v.number AS Yr
                FROM (VALUES (0),(1),(2),(3),(4)) v(number)
            )
            SELECT y.Yr, ISNULL(SUM(s.TotalDue), 0)
            FROM Years y
            LEFT JOIN dbo.Sales s ON YEAR(s.SaleDate) = y.Yr
            GROUP BY y.Yr ORDER BY y.Yr;
            """;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(new SalesPoint { Label = reader.GetInt32(0).ToString(), Amount = reader.GetDecimal(1) });
        return list;
    }

    public List<TopProductRow> GetTopProducts(int take = 8)
    {
        var list = new List<TopProductRow>();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT TOP (@Take) p.ProductName, SUM(si.Quantity), SUM(si.LineTotal)
            FROM dbo.SaleItems si
            INNER JOIN dbo.Products p ON p.ProductId = si.ProductId
            GROUP BY p.ProductName
            ORDER BY SUM(si.LineTotal) DESC;
            """;
        cmd.Parameters.AddWithValue("@Take", take);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(new TopProductRow
            {
                ProductName = reader.GetString(0),
                QtySold = reader.GetDecimal(1),
                Revenue = reader.GetDecimal(2)
            });
        return list;
    }

    public List<CategorySalesRow> GetSalesByCategory()
    {
        var list = new List<CategorySalesRow>();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT ISNULL(c.CategoryName, N'Uncategorized'), ISNULL(SUM(si.LineTotal), 0)
            FROM dbo.SaleItems si
            INNER JOIN dbo.Products p ON p.ProductId = si.ProductId
            LEFT JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
            GROUP BY c.CategoryName
            ORDER BY SUM(si.LineTotal) DESC;
            """;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(new CategorySalesRow { CategoryName = reader.GetString(0), Revenue = reader.GetDecimal(1) });
        return list;
    }

    public List<InventoryLedgerEntry> GetRecentStockMoves(int take = 10)
    {
        var list = new List<InventoryLedgerEntry>();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT TOP (@Take) l.LedgerId, l.ProductId, p.ProductName, l.MovementType, l.QtyChange,
                   l.BalanceAfter, l.ReferenceId, l.Remarks, u.FullName, l.CreatedAt
            FROM dbo.InventoryLedger l
            INNER JOIN dbo.Products p ON p.ProductId = l.ProductId
            LEFT JOIN dbo.Users u ON u.UserId = l.CreatedBy
            WHERE l.MovementType IN (N'IN', N'OUT')
            ORDER BY l.CreatedAt DESC;
            """;
        cmd.Parameters.AddWithValue("@Take", take);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new InventoryLedgerEntry
            {
                LedgerId = reader.GetInt64(0),
                ProductId = reader.GetInt32(1),
                ProductName = reader.GetString(2),
                MovementType = reader.GetString(3),
                QtyChange = reader.GetDecimal(4),
                BalanceAfter = reader.GetDecimal(5),
                ReferenceId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                Remarks = reader.IsDBNull(7) ? null : reader.GetString(7),
                CreatedByName = reader.IsDBNull(8) ? null : reader.GetString(8),
                CreatedAt = reader.GetDateTime(9)
            });
        }
        return list;
    }

    public List<Product> GetInventoryAlerts()
    {
        var list = new List<Product>();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT p.ProductId, p.ProductName, p.ProductDetails, p.Barcode,
                   ISNULL(u.UnitName, p.UnitOfMeasure), p.CostPrice, p.SellingPrice,
                   p.StockQty, p.ReorderLevel, p.CategoryId, c.CategoryName,
                   p.SupplierId, s.CompanyName, p.IsArchived, p.ProductCode, p.UnitId
            FROM dbo.Products p
            LEFT JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
            LEFT JOIN dbo.Suppliers s ON s.SupplierId = p.SupplierId
            LEFT JOIN dbo.Units u ON u.UnitId = p.UnitId
            WHERE p.IsArchived = 0 AND p.StockQty <= p.ReorderLevel
            ORDER BY CASE WHEN p.StockQty <= 0 THEN 0 ELSE 1 END, p.StockQty;
            """;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Product
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
                UnitId = reader.IsDBNull(15) ? null : reader.GetInt32(15)
            });
        }
        return list;
    }

    private static List<SalesPoint> QueryPoints(string sql, (string, object) p1, (string, object) p2, Func<DateTime, string> labeler)
    {
        var list = new List<SalesPoint>();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue(p1.Item1, p1.Item2);
        cmd.Parameters.AddWithValue(p2.Item1, p2.Item2);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var label = reader.GetFieldType(0) == typeof(int)
                ? reader.GetInt32(0).ToString()
                : labeler(reader.GetDateTime(0));
            list.Add(new SalesPoint { Label = label, Amount = reader.GetDecimal(1) });
        }
        return list;
    }
}
