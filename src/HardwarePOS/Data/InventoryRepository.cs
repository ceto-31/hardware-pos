using HardwarePOS.Models;
using Microsoft.Data.SqlClient;

namespace HardwarePOS.Data;

public class InventoryRepository
{
    public void StockIn(int supplierId, int productId, decimal quantity, decimal cost,
        DateTime dateReceived, string? remarks, int? createdBy)
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        try
        {
            int stockInId;
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO dbo.StockIns (SupplierId, ProductId, Quantity, Cost, DateReceived, Remarks, CreatedBy)
                    VALUES (@SupplierId, @ProductId, @Qty, @Cost, @Date, @Remarks, @CreatedBy);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);
                    """;
                cmd.Parameters.AddWithValue("@SupplierId", supplierId);
                cmd.Parameters.AddWithValue("@ProductId", productId);
                cmd.Parameters.AddWithValue("@Qty", quantity);
                cmd.Parameters.AddWithValue("@Cost", cost);
                cmd.Parameters.AddWithValue("@Date", dateReceived.Date);
                cmd.Parameters.AddWithValue("@Remarks", (object?)remarks ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CreatedBy", (object?)createdBy ?? DBNull.Value);
                stockInId = (int)cmd.ExecuteScalar()!;
            }

            decimal newBalance;
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    UPDATE dbo.Products
                    SET StockQty = StockQty + @Qty,
                        CostPrice = CASE WHEN @Cost > 0 THEN @Cost ELSE CostPrice END
                    WHERE ProductId = @ProductId;
                    SELECT StockQty FROM dbo.Products WHERE ProductId = @ProductId;
                    """;
                cmd.Parameters.AddWithValue("@Qty", quantity);
                cmd.Parameters.AddWithValue("@Cost", cost);
                cmd.Parameters.AddWithValue("@ProductId", productId);
                newBalance = (decimal)cmd.ExecuteScalar()!;
            }

            InsertLedger(conn, tx, productId, "IN", quantity, newBalance, stockInId, remarks, createdBy);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public void StockOut(int productId, decimal quantity, string reason, DateTime dateOut,
        string? remarks, int? createdBy)
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        try
        {
            decimal currentStock;
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "SELECT StockQty FROM dbo.Products WHERE ProductId = @Id;";
                cmd.Parameters.AddWithValue("@Id", productId);
                currentStock = (decimal)(cmd.ExecuteScalar() ?? 0m);
            }

            if (currentStock < quantity)
                throw new InvalidOperationException($"Insufficient stock. Available: {currentStock}.");

            int stockOutId;
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO dbo.StockOuts (ProductId, Quantity, Reason, DateOut, Remarks, CreatedBy)
                    VALUES (@ProductId, @Qty, @Reason, @Date, @Remarks, @CreatedBy);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);
                    """;
                cmd.Parameters.AddWithValue("@ProductId", productId);
                cmd.Parameters.AddWithValue("@Qty", quantity);
                cmd.Parameters.AddWithValue("@Reason", reason);
                cmd.Parameters.AddWithValue("@Date", dateOut.Date);
                cmd.Parameters.AddWithValue("@Remarks", (object?)remarks ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CreatedBy", (object?)createdBy ?? DBNull.Value);
                stockOutId = (int)cmd.ExecuteScalar()!;
            }

            decimal newBalance;
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    UPDATE dbo.Products SET StockQty = StockQty - @Qty WHERE ProductId = @ProductId;
                    SELECT StockQty FROM dbo.Products WHERE ProductId = @ProductId;
                    """;
                cmd.Parameters.AddWithValue("@Qty", quantity);
                cmd.Parameters.AddWithValue("@ProductId", productId);
                newBalance = (decimal)cmd.ExecuteScalar()!;
            }

            InsertLedger(conn, tx, productId, "OUT", -quantity, newBalance, stockOutId,
                $"{reason}: {remarks}".Trim(' ', ':'), createdBy);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public List<InventoryLedgerEntry> GetHistory(int? productId = null, int take = 200)
    {
        var list = new List<InventoryLedgerEntry>();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT TOP (@Take)
                l.LedgerId, l.ProductId, p.ProductName, l.MovementType, l.QtyChange,
                l.BalanceAfter, l.ReferenceId, l.Remarks, u.FullName, l.CreatedAt
            FROM dbo.InventoryLedger l
            INNER JOIN dbo.Products p ON p.ProductId = l.ProductId
            LEFT JOIN dbo.Users u ON u.UserId = l.CreatedBy
            WHERE (@ProductId IS NULL OR l.ProductId = @ProductId)
            ORDER BY l.CreatedAt DESC, l.LedgerId DESC;
            """;
        cmd.Parameters.AddWithValue("@Take", take);
        cmd.Parameters.AddWithValue("@ProductId", (object?)productId ?? DBNull.Value);

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

    private static void InsertLedger(SqlConnection conn, SqlTransaction tx, int productId,
        string movementType, decimal qtyChange, decimal balanceAfter, int? referenceId,
        string? remarks, int? createdBy)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO dbo.InventoryLedger
                (ProductId, MovementType, QtyChange, BalanceAfter, ReferenceId, Remarks, CreatedBy)
            VALUES
                (@ProductId, @Type, @Qty, @Balance, @Ref, @Remarks, @CreatedBy);
            """;
        cmd.Parameters.AddWithValue("@ProductId", productId);
        cmd.Parameters.AddWithValue("@Type", movementType);
        cmd.Parameters.AddWithValue("@Qty", qtyChange);
        cmd.Parameters.AddWithValue("@Balance", balanceAfter);
        cmd.Parameters.AddWithValue("@Ref", (object?)referenceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Remarks", (object?)remarks ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedBy", (object?)createdBy ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }
}
