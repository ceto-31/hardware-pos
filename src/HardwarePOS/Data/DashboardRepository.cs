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
        // Use local server time (SYSDATETIME) so "today" matches PH store hours
        cmd.CommandText = """
            SELECT
                ISNULL((SELECT SUM(TotalDue) FROM dbo.Sales WHERE CAST(SaleDate AS DATE) = CAST(SYSDATETIME() AS DATE)), 0),
                ISNULL((SELECT COUNT(*) FROM dbo.Sales WHERE CAST(SaleDate AS DATE) = CAST(SYSDATETIME() AS DATE)), 0),
                ISNULL((SELECT SUM(TotalDue) FROM dbo.Sales), 0);
            """;
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return new DashboardSummary
        {
            TodaySales = reader.GetDecimal(0),
            TodayTransactions = reader.GetInt32(1),
            TotalRevenue = reader.GetDecimal(2)
        };
    }

    public List<SalesPoint> GetDailySales(int days = 7)
    {
        var list = new List<SalesPoint>();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            ;WITH Days AS (
                SELECT CAST(DATEADD(DAY, -v.number, CAST(SYSDATETIME() AS DATE)) AS DATE) AS SaleDay
                FROM (VALUES (0),(1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12),(13)) v(number)
                WHERE v.number < @Days
            )
            SELECT d.SaleDay, ISNULL(SUM(s.TotalDue), 0) AS Amount
            FROM Days d
            LEFT JOIN dbo.Sales s ON CAST(s.SaleDate AS DATE) = d.SaleDay
            GROUP BY d.SaleDay
            ORDER BY d.SaleDay;
            """;
        cmd.Parameters.AddWithValue("@Days", days);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new SalesPoint
            {
                Label = reader.GetDateTime(0).ToString("MMM dd"),
                Amount = reader.GetDecimal(1)
            });
        }
        return list;
    }

    public List<SalesPoint> GetWeeklySales(int weeks = 8)
    {
        var list = new List<SalesPoint>();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            ;WITH Weeks AS (
                SELECT DATEADD(WEEK, -v.number, DATEADD(WEEK, DATEDIFF(WEEK, 0, SYSDATETIME()), 0)) AS WeekStart
                FROM (VALUES (0),(1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11)) v(number)
                WHERE v.number < @Weeks
            )
            SELECT w.WeekStart, ISNULL(SUM(s.TotalDue), 0) AS Amount
            FROM Weeks w
            LEFT JOIN dbo.Sales s
                ON s.SaleDate >= w.WeekStart AND s.SaleDate < DATEADD(WEEK, 1, w.WeekStart)
            GROUP BY w.WeekStart
            ORDER BY w.WeekStart;
            """;
        cmd.Parameters.AddWithValue("@Weeks", weeks);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new SalesPoint
            {
                Label = reader.GetDateTime(0).ToString("MMM dd"),
                Amount = reader.GetDecimal(1)
            });
        }
        return list;
    }

    public List<SalesPoint> GetMonthlySales(int months = 6)
    {
        var list = new List<SalesPoint>();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            ;WITH Months AS (
                SELECT DATEFROMPARTS(YEAR(DATEADD(MONTH, -v.number, SYSDATETIME())),
                                     MONTH(DATEADD(MONTH, -v.number, SYSDATETIME())), 1) AS MonthStart
                FROM (VALUES (0),(1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11)) v(number)
                WHERE v.number < @Months
            )
            SELECT m.MonthStart, ISNULL(SUM(s.TotalDue), 0) AS Amount
            FROM Months m
            LEFT JOIN dbo.Sales s
                ON s.SaleDate >= m.MonthStart AND s.SaleDate < DATEADD(MONTH, 1, m.MonthStart)
            GROUP BY m.MonthStart
            ORDER BY m.MonthStart;
            """;
        cmd.Parameters.AddWithValue("@Months", months);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new SalesPoint
            {
                Label = reader.GetDateTime(0).ToString("MMM yyyy"),
                Amount = reader.GetDecimal(1)
            });
        }
        return list;
    }
}
