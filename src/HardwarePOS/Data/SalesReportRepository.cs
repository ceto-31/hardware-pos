using HardwarePOS.Models;
using Microsoft.Data.SqlClient;

namespace HardwarePOS.Data;

public class SalesReportRepository
{
    public (List<DailyTotal> Totals, List<ProductSalesRow> Products, List<SalesDetailRow> Details, DateTime? RecordsStart) Capture(DateTime from,DateTime to,bool details)
    {
        using var conn=DbConnectionFactory.Create();conn.Open();
        using var tx=conn.BeginTransaction(System.Data.IsolationLevel.Serializable);
        var totals=GetDailyTotals(from,to,conn,tx);
        var products=GetProducts(from,to,conn,tx);
        var lines=details?GetDetails(from,to,conn,tx):[];
        var start=GetRecordsStartDate(conn,tx);
        tx.Commit();
        return (totals,products,lines,start);
    }
    public List<DailyTotal> GetDailyTotals(DateTime from, DateTime to, SqlConnection? source=null, SqlTransaction? transaction=null)
    {
        var list = new List<DailyTotal>();
        using var owned = source is null ? DbConnectionFactory.Create() : null; var conn=source??owned!; if(owned is not null) conn.Open();
        using var cmd = conn.CreateCommand(); cmd.Transaction=transaction;
        cmd.CommandText = """
            SELECT CAST(EventDate AS date), SUM(Amount), SUM(TransactionCount), SUM(RefundAmount)
            FROM dbo.SalesMovements
            WHERE EventDate >= @From AND EventDate < @ToExclusive
            GROUP BY CAST(EventDate AS date) ORDER BY CAST(EventDate AS date);
            """;
        cmd.Parameters.AddWithValue("@From", from.Date);
        cmd.Parameters.AddWithValue("@ToExclusive", to.Date.AddDays(1));
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new DailyTotal
            {
                Date = reader.GetDateTime(0).Date,
                Amount = reader.GetDecimal(1),
                TransactionCount = reader.GetInt32(2), RefundAmount = reader.GetDecimal(3)
            });
        }
        return list;
    }

    public List<SalesDetailRow> GetDetails(DateTime from,DateTime to, SqlConnection? source=null, SqlTransaction? transaction=null)
    {
        using var owned = source is null ? DbConnectionFactory.Create() : null; var conn=source??owned!; if(owned is not null) conn.Open();
        using var cmd=BatchStock.Command(conn,transaction,"""
            SELECT f.InvoiceNo,COALESCE(f.NameSnapshot,p.ProductName),COALESCE(f.UnitSnapshot,p.UnitOfMeasure),f.Quantity,f.PaidAmount,N'Sale',f.SaleDate
            FROM dbo.SaleLineFacts f JOIN dbo.Products p ON p.ProductId=f.ProductId
            WHERE f.IsVoided=0 AND f.SaleDate>=@From AND f.SaleDate<@To
            UNION ALL
            SELECT f.InvoiceNo,COALESCE(f.NameSnapshot,p.ProductName),COALESCE(f.UnitSnapshot,p.UnitOfMeasure),-r.Quantity,-r.RefundAmount,N'Refund',r.CreatedAt
            FROM dbo.CustomerReturns r JOIN dbo.SaleLineFacts f ON f.SaleItemId=r.SaleItemId JOIN dbo.Products p ON p.ProductId=f.ProductId
            WHERE f.IsVoided=0 AND r.CreatedAt>=@From AND r.CreatedAt<@To
            ORDER BY 7,1,2;
            """,("@From",from.Date),("@To",to.Date.AddDays(1)));
        using var r=cmd.ExecuteReader();var list=new List<SalesDetailRow>();
        while(r.Read()) list.Add(new SalesDetailRow {Invoice=r.GetString(0),ProductName=r.GetString(1),Unit=r.GetString(2),Quantity=r.GetDecimal(3),Amount=r.GetDecimal(4),Event=r.GetString(5),EventDate=r.GetDateTime(6)});
        return list;
    }

    public List<ProductSalesRow> GetProducts(DateTime from,DateTime to, SqlConnection? source=null, SqlTransaction? transaction=null)
    {
        using var owned = source is null ? DbConnectionFactory.Create() : null; var conn=source??owned!; if(owned is not null) conn.Open();
        using var cmd=BatchStock.Command(conn,transaction,"""
            WITH events AS (
                SELECT f.ProductId,COALESCE(f.UnitSnapshot,p.UnitOfMeasure) Unit,f.Quantity Sold,CAST(0 AS decimal(18,3)) Returned,f.PaidAmount Amount
                FROM dbo.SaleLineFacts f JOIN dbo.Products p ON p.ProductId=f.ProductId
                WHERE f.IsVoided=0 AND f.SaleDate>=@From AND f.SaleDate<@To
                UNION ALL
                SELECT f.ProductId,COALESCE(f.UnitSnapshot,p.UnitOfMeasure),0,r.Quantity,-r.RefundAmount
                FROM dbo.CustomerReturns r JOIN dbo.SaleLineFacts f ON f.SaleItemId=r.SaleItemId JOIN dbo.Products p ON p.ProductId=f.ProductId
                WHERE f.IsVoided=0 AND r.CreatedAt>=@From AND r.CreatedAt<@To
            )
            SELECT p.ProductName, e.Unit,SUM(e.Sold),SUM(e.Returned),SUM(e.Amount),p.ImagePath
            FROM events e JOIN dbo.Products p ON p.ProductId=e.ProductId
            GROUP BY p.ProductId,p.ProductName,e.Unit,p.ImagePath ORDER BY SUM(e.Amount) DESC,p.ProductId;
            """,("@From",from.Date),("@To",to.Date.AddDays(1)));
        using var r=cmd.ExecuteReader();var list=new List<ProductSalesRow>();
        while(r.Read()) list.Add(new ProductSalesRow {ProductName=r.GetString(0),Unit=r.GetString(1),Sold=r.GetDecimal(2),Returned=r.GetDecimal(3),NetSales=r.GetDecimal(4),ImagePath=r.IsDBNull(5)?null:r.GetString(5)});
        return list;
    }

    public DateTime? GetRecordsStartDate(SqlConnection? source=null, SqlTransaction? transaction=null)
    {
        using var owned = source is null ? DbConnectionFactory.Create() : null; var conn=source??owned!; if(owned is not null) conn.Open();
        using var cmd = conn.CreateCommand(); cmd.Transaction=transaction;
        cmd.CommandText = """
            SELECT MIN(d)
            FROM (
                SELECT MIN(CAST(SaleDate AS date)) AS d FROM dbo.Sales
                UNION ALL
                SELECT MIN(DateReceived) FROM dbo.StockIns
                UNION ALL
                SELECT MIN(DateOut) FROM dbo.StockOuts
                UNION ALL
                SELECT MIN(CAST(CreatedAt AS date)) FROM dbo.InventoryLedger
            ) x;
            """;
        var value = cmd.ExecuteScalar();
        return value is DateTime date ? date.Date : null;
    }
}
