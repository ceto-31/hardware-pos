using HardwarePOS.Models;
using Microsoft.Data.SqlClient;

namespace HardwarePOS.Data;

public class SalesRepository
{
    public string CompleteSale(
        int cashierId,
        IReadOnlyList<CartItem> items,
        decimal subtotal,
        decimal taxAmount,
        decimal discountAmount,
        decimal totalDue,
        decimal cashTendered,
        decimal changeAmount)
    {
        if (items.Count == 0)
            throw new InvalidOperationException("Cart is empty.");
        if (cashTendered < totalDue)
            throw new InvalidOperationException("Cash tendered is less than total due.");

        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        try
        {
            foreach (var item in items)
            {
                using var check = conn.CreateCommand();
                check.Transaction = tx;
                check.CommandText = """
                    SELECT StockQty
                    FROM dbo.Products WITH (UPDLOCK, ROWLOCK)
                    WHERE ProductId = @Id AND IsArchived = 0;
                    """;
                check.Parameters.AddWithValue("@Id", item.ProductId);
                var stockObj = check.ExecuteScalar();
                if (stockObj is null)
                    throw new InvalidOperationException($"Product '{item.ProductName}' is unavailable.");
                var stock = (decimal)stockObj;
                if (stock < item.Quantity)
                    throw new InvalidOperationException($"Insufficient stock for '{item.ProductName}'. Available: {stock}.");
            }

            var invoiceNo = $"INV-{DateTime.Now:yyyyMMddHHmmssfff}-{cashierId}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
            int saleId;
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO dbo.Sales
                        (InvoiceNo, SaleDate, CashierId, Subtotal, TaxAmount, DiscountAmount, TotalDue, CashTendered, ChangeAmount)
                    VALUES
                        (@Invoice, SYSDATETIME(), @Cashier, @Subtotal, @Tax, @Discount, @Total, @Cash, @Change);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);
                    """;
                cmd.Parameters.AddWithValue("@Invoice", invoiceNo);
                cmd.Parameters.AddWithValue("@Cashier", cashierId);
                cmd.Parameters.AddWithValue("@Subtotal", subtotal);
                cmd.Parameters.AddWithValue("@Tax", taxAmount);
                cmd.Parameters.AddWithValue("@Discount", discountAmount);
                cmd.Parameters.AddWithValue("@Total", totalDue);
                cmd.Parameters.AddWithValue("@Cash", cashTendered);
                cmd.Parameters.AddWithValue("@Change", changeAmount);
                saleId = (int)cmd.ExecuteScalar()!;
            }

            foreach (var item in items)
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = """
                        INSERT INTO dbo.SaleItems (SaleId, ProductId, Quantity, UnitPrice, LineTotal)
                        VALUES (@SaleId, @ProductId, @Qty, @Price, @Line);
                        """;
                    cmd.Parameters.AddWithValue("@SaleId", saleId);
                    cmd.Parameters.AddWithValue("@ProductId", item.ProductId);
                    cmd.Parameters.AddWithValue("@Qty", item.Quantity);
                    cmd.Parameters.AddWithValue("@Price", item.UnitPrice);
                    cmd.Parameters.AddWithValue("@Line", item.LineTotal);
                    cmd.ExecuteNonQuery();
                }

                decimal newBalance;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = """
                        UPDATE dbo.Products
                        SET StockQty = StockQty - @Qty
                        WHERE ProductId = @ProductId AND StockQty >= @Qty AND IsArchived = 0;
                        IF @@ROWCOUNT = 0
                            THROW 50003, 'Insufficient stock during checkout.', 1;
                        SELECT StockQty FROM dbo.Products WHERE ProductId = @ProductId;
                        """;
                    cmd.Parameters.AddWithValue("@Qty", item.Quantity);
                    cmd.Parameters.AddWithValue("@ProductId", item.ProductId);
                    newBalance = (decimal)cmd.ExecuteScalar()!;
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = """
                        INSERT INTO dbo.InventoryLedger
                            (ProductId, MovementType, QtyChange, BalanceAfter, ReferenceId, Remarks, CreatedBy)
                        VALUES
                            (@ProductId, N'SALE', @Qty, @Balance, @Ref, @Remarks, @CreatedBy);
                        """;
                    cmd.Parameters.AddWithValue("@ProductId", item.ProductId);
                    cmd.Parameters.AddWithValue("@Qty", -item.Quantity);
                    cmd.Parameters.AddWithValue("@Balance", newBalance);
                    cmd.Parameters.AddWithValue("@Ref", saleId);
                    cmd.Parameters.AddWithValue("@Remarks", $"Sale {invoiceNo}");
                    cmd.Parameters.AddWithValue("@CreatedBy", cashierId);
                    cmd.ExecuteNonQuery();
                }
            }

            tx.Commit();
            return invoiceNo;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public List<SaleHistoryRow> GetHistory(string? search = null, int? year = null)
    {
        var list = new List<SaleHistoryRow>();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT s.SaleId, s.InvoiceNo, s.SaleDate, u.FullName, s.Subtotal, s.TaxAmount,
                   s.DiscountAmount, s.TotalDue, s.CashTendered, s.ChangeAmount
            FROM dbo.Sales s
            INNER JOIN dbo.Users u ON u.UserId = s.CashierId
            WHERE (@Year IS NULL OR YEAR(s.SaleDate) = @Year)
              AND (@Search IS NULL OR @Search = N'' OR s.InvoiceNo LIKE N'%' + @Search + N'%' OR u.FullName LIKE N'%' + @Search + N'%')
            ORDER BY s.SaleDate DESC;
            """;
        cmd.Parameters.AddWithValue("@Year", (object?)year ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Search", (object?)search ?? DBNull.Value);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new SaleHistoryRow
            {
                SaleId = reader.GetInt32(0),
                InvoiceNo = reader.GetString(1),
                SaleDate = reader.GetDateTime(2),
                CashierName = reader.GetString(3),
                Subtotal = reader.GetDecimal(4),
                TaxAmount = reader.GetDecimal(5),
                DiscountAmount = reader.GetDecimal(6),
                TotalDue = reader.GetDecimal(7),
                CashTendered = reader.GetDecimal(8),
                ChangeAmount = reader.GetDecimal(9)
            });
        }
        return list;
    }

    public List<SaleHistoryItemRow> GetSaleItems(int saleId)
    {
        var list = new List<SaleHistoryItemRow>();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT p.ProductName, si.Quantity, si.UnitPrice, si.LineTotal
            FROM dbo.SaleItems si
            INNER JOIN dbo.Products p ON p.ProductId = si.ProductId
            WHERE si.SaleId = @Id;
            """;
        cmd.Parameters.AddWithValue("@Id", saleId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new SaleHistoryItemRow
            {
                ProductName = reader.GetString(0),
                Quantity = reader.GetDecimal(1),
                UnitPrice = reader.GetDecimal(2),
                LineTotal = reader.GetDecimal(3)
            });
        }
        return list;
    }

    public SaleHistoryRow? GetSale(int saleId) =>
        GetHistory().FirstOrDefault(s => s.SaleId == saleId);
}
