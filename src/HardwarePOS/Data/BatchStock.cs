using HardwarePOS.Helpers;
using HardwarePOS.Models;
using HardwarePOS.Services;
using Microsoft.Data.SqlClient;

namespace HardwarePOS.Data;

// Callers hold the product lock and use the same transaction for balance and ledger changes.
public static class BatchStock
{
    public static void RequireAdmin()
    {
        if (!SessionManager.IsAdmin) throw new InvalidOperationException("Admin access required.");
    }

    public static SqlCommand Command(SqlConnection conn, SqlTransaction? tx, string sql, params (string, object?)[] values)
    {
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var (name, value) in values) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }

    public static List<(int BatchId, decimal Quantity)> Take(SqlConnection conn, SqlTransaction tx, int productId, decimal quantity, int? batchId = null)
    {
        InventoryRules.ValidateQuantity(quantity);
        var available = new List<(int Id, decimal Qty)>();
        using (var cmd = Command(conn, tx, """
            SELECT BatchId,RemainingQuantity FROM dbo.InventoryBatches WITH (UPDLOCK,HOLDLOCK)
            WHERE ProductId=@Product AND RemainingQuantity>0 AND (@Batch IS NULL OR BatchId=@Batch)
            ORDER BY ReceivedAt,BatchId;
            """, ("@Product",productId),("@Batch",batchId)))
        using (var reader = cmd.ExecuteReader())
            while (reader.Read()) available.Add((reader.GetInt32(0),reader.GetDecimal(1)));
        if (available.Sum(b=>b.Qty) < quantity)
            throw new InvalidOperationException("Not enough stock in the selected delivery. Refresh stock and try again.");
        var allocations = new List<(int BatchId, decimal Quantity)>();
        foreach (var batch in available)
        {
            if (quantity == 0) break;
            var take = Math.Min(quantity,batch.Qty);
            using var cmd = Command(conn,tx,"UPDATE dbo.InventoryBatches SET RemainingQuantity=RemainingQuantity-@Qty WHERE BatchId=@Id;",("@Qty",take),("@Id",batch.Id));
            cmd.ExecuteNonQuery();
            allocations.Add((batch.Id,take));
            quantity -= take;
        }
        return allocations;
    }

    public static int Add(SqlConnection conn, SqlTransaction tx, int productId, int? supplier, int? stockIn, decimal quantity, DateTime received, DateTime? expiry, string source)
    {
        using var cmd = Command(conn,tx,"""
            INSERT dbo.InventoryBatches(ProductId,SupplierId,StockInId,ReceivedAt,ExpirationDate,OriginalQuantity,RemainingQuantity,SourceLabel)
            VALUES(@Product,@Supplier,@StockIn,@Date,@Expiry,@Qty,@Qty,@Source);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,("@Product",productId),("@Supplier",supplier),("@StockIn",stockIn),("@Date",received),("@Expiry",expiry),("@Qty",quantity),("@Source",source));
        return (int)cmd.ExecuteScalar()!;
    }

    public static void AddBadStock(SqlConnection conn, SqlTransaction tx,int product,int? batch,int? returnId,decimal quantity,string reason,int? user)
    {
        using var cmd=Command(conn,tx,"""
            INSERT dbo.BadStock(ProductId,BatchId,ReturnId,Quantity,RemainingQuantity,Reason,CreatedBy)
            VALUES(@Product,@Batch,@Return,@Qty,@Qty,@Reason,@User);
            """,("@Product",product),("@Batch",batch),("@Return",returnId),("@Qty",quantity),("@Reason",reason),("@User",user));
        cmd.ExecuteNonQuery();
    }

    public static List<InventoryBatch> GetBatches(int? productId=null,int? supplierId=null)
    {
        using var conn=DbConnectionFactory.Create(); conn.Open();
        using var cmd=Command(conn,null,"""
            SELECT b.BatchId,b.ProductId,p.ProductName,b.SupplierId,s.CompanyName,b.ReceivedAt,b.ExpirationDate,
                   b.OriginalQuantity,b.RemainingQuantity,COALESCE(u.UnitName,p.UnitOfMeasure),b.SourceLabel
            FROM dbo.InventoryBatches b JOIN dbo.Products p ON p.ProductId=b.ProductId
            LEFT JOIN dbo.Suppliers s ON s.SupplierId=b.SupplierId LEFT JOIN dbo.Units u ON u.UnitId=p.UnitId
            WHERE (@Product IS NULL OR b.ProductId=@Product) AND (@Supplier IS NULL OR b.SupplierId=@Supplier)
            ORDER BY b.ReceivedAt,b.BatchId;
            """,("@Product",productId),("@Supplier",supplierId is >0 ? supplierId : null));
        using var r=cmd.ExecuteReader(); var list=new List<InventoryBatch>();
        while(r.Read()) list.Add(new InventoryBatch {BatchId=r.GetInt32(0),ProductId=r.GetInt32(1),ProductName=r.GetString(2),
            SupplierId=r.IsDBNull(3)?null:r.GetInt32(3),SupplierName=r.IsDBNull(4)?"Supplier/delivery unknown":r.GetString(4),
            ReceivedAt=r.GetDateTime(5),ExpirationDate=r.IsDBNull(6)?null:r.GetDateTime(6),OriginalQuantity=r.GetDecimal(7),
            RemainingQuantity=r.GetDecimal(8),Unit=r.GetString(9),SourceLabel=r.GetString(10)});
        return list;
    }
}
