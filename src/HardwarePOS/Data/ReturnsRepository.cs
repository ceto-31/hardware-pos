using HardwarePOS.Helpers;
using HardwarePOS.Models;
using HardwarePOS.Services;
using Microsoft.Data.SqlClient;
using static HardwarePOS.Data.BatchStock;

namespace HardwarePOS.Data;

public class ReturnsRepository
{
    public List<ReturnableItem> GetItems(int saleId)
    {
        using var conn=DbConnectionFactory.Create(); conn.Open();
        return ReadItems(conn,null,saleId);
    }

    private static List<ReturnableItem> ReadItems(SqlConnection conn,SqlTransaction? tx,int saleId)
    {
        using var cmd=Command(conn,tx,"""
            SELECT si.SaleItemId,COALESCE(si.NameSnapshot,p.ProductName),COALESCE(si.UnitSnapshot,p.UnitOfMeasure),
                   si.Quantity,COALESCE(r.Qty,0),si.RefundableAmount,COALESCE(r.Refund,0),si.LineTotal,s.TotalDue
            FROM dbo.SaleItems si JOIN dbo.Sales s ON s.SaleId=si.SaleId JOIN dbo.Products p ON p.ProductId=si.ProductId
            OUTER APPLY(SELECT SUM(Quantity) Qty,SUM(RefundAmount) Refund FROM dbo.CustomerReturns WHERE SaleItemId=si.SaleItemId) r
            WHERE si.SaleId=@Sale ORDER BY si.SaleItemId;
            """,("@Sale",saleId));
        var list=new List<ReturnableItem>(); var values=new List<decimal>(); var missing=new List<bool>(); decimal paid=0;
        using var r=cmd.ExecuteReader();
        while(r.Read())
        {
            list.Add(new ReturnableItem {SaleItemId=r.GetInt32(0),ProductName=r.GetString(1),Unit=r.GetString(2),
                Quantity=r.GetDecimal(3),Returned=r.GetDecimal(4),Paid=r.IsDBNull(5)?0:r.GetDecimal(5),Refunded=r.GetDecimal(6)});
            missing.Add(r.IsDBNull(5)); values.Add(r.GetDecimal(7)); paid=r.GetDecimal(8);
        }
        var amounts=InventoryRules.AllocatePaidTotal(values,paid);
        for(var i=0;i<list.Count;i++) if(missing[i]) list[i].Paid=amounts[i];
        return list;
    }

    public decimal Return(int saleId,int itemId,decimal quantity,string condition,string reason)
    {
        RequireAdmin(); InventoryRules.ValidateQuantity(quantity);
        if(condition is not ("Usable" or "Defective")) throw new InvalidOperationException("Select the item condition.");
        reason=reason.Trim();
        if(reason.Length is 0 or >300) throw new InvalidOperationException("Enter a return reason (up to 300 characters).");
        var user=SessionManager.CurrentUser!.UserId;
        using var conn=DbConnectionFactory.Create(); conn.Open(); using var tx=conn.BeginTransaction();
        try
        {
            using(var sale=Command(conn,tx,"SELECT SaleDate,IsVoided,CAST(SYSDATETIME() AS datetime2(0)) FROM dbo.Sales WITH(UPDLOCK,HOLDLOCK) WHERE SaleId=@Sale;",("@Sale",saleId)))
            using(var r=sale.ExecuteReader())
            {
                if(!r.Read()) throw new InvalidOperationException("Sale not found.");
                if(r.GetBoolean(1)) throw new InvalidOperationException("Voided sales cannot be returned.");
                if(!InventoryRules.IsReturnEligible(r.GetDateTime(0),r.GetDateTime(2)))
                    throw new InvalidOperationException($"The 7-day return period ended on {r.GetDateTime(0).AddDays(7):MMM d, yyyy h:mm tt}.");
            }
            var item=ReadItems(conn,tx,saleId).SingleOrDefault(i=>i.SaleItemId==itemId)
                ?? throw new InvalidOperationException("Item is not on this receipt.");
            var refund=InventoryRules.Refund(item.Paid,item.Quantity,item.Returned,quantity,item.Refunded);
            int product;
            using(var cmd=Command(conn,tx,"SELECT ProductId FROM dbo.SaleItems WHERE SaleItemId=@Item;",("@Item",itemId))) product=(int)cmd.ExecuteScalar()!;
            using(var cmd=Command(conn,tx,"SELECT AllowsFractional FROM dbo.Products WITH(UPDLOCK,HOLDLOCK) WHERE ProductId=@Product;",("@Product",product)))
                InventoryRules.ValidateQuantity(quantity,(bool)cmd.ExecuteScalar()!);
            int returnId;
            using(var cmd=Command(conn,tx,"""
                INSERT dbo.CustomerReturns(SaleItemId,Quantity,RefundAmount,ItemCondition,Reason,CreatedBy)
                VALUES(@Item,@Qty,@Refund,@Condition,@Reason,@User); SELECT CAST(SCOPE_IDENTITY() AS int);
                """,("@Item",itemId),("@Qty",quantity),("@Refund",refund),("@Condition",condition),("@Reason",reason),("@User",user)))
                returnId=(int)cmd.ExecuteScalar()!;
            var allocations=new List<(int Id,int Batch,decimal Available)>();
            using(var cmd=Command(conn,tx,"SELECT AllocationId,BatchId,Quantity-ReturnedQuantity FROM dbo.SaleAllocations WITH(UPDLOCK,HOLDLOCK) WHERE SaleItemId=@Item ORDER BY AllocationId;",("@Item",itemId)))
            using(var r=cmd.ExecuteReader()) while(r.Read()) allocations.Add((r.GetInt32(0),r.GetInt32(1),r.GetDecimal(2)));
            var remaining=quantity;
            foreach(var allocation in allocations)
            {
                var take=Math.Min(remaining,allocation.Available); if(take<=0) continue;
                using(var cmd=Command(conn,tx,"UPDATE dbo.SaleAllocations SET ReturnedQuantity=ReturnedQuantity+@Qty WHERE AllocationId=@Id;",("@Qty",take),("@Id",allocation.Id))) cmd.ExecuteNonQuery();
                if(condition=="Usable")
                {
                    using var cmd=Command(conn,tx,"UPDATE dbo.InventoryBatches SET RemainingQuantity=RemainingQuantity+@Qty WHERE BatchId=@Batch;",("@Qty",take),("@Batch",allocation.Batch)); cmd.ExecuteNonQuery();
                }
                else AddBadStock(conn,tx,product,allocation.Batch,returnId,take,reason,user);
                remaining-=take;
            }
            if(remaining>0)
            {
                if(condition=="Usable") Add(conn,tx,product,null,null,remaining,DateTime.Now,null,"Legacy return — supplier/delivery unknown");
                else AddBadStock(conn,tx,product,null,returnId,remaining,reason,user);
            }
            var stockChange=condition=="Usable"?quantity:0m;
            using(var cmd=Command(conn,tx,"""
                UPDATE dbo.Products SET StockQty=StockQty+@Qty WHERE ProductId=@Product;
                INSERT dbo.InventoryLedger(ProductId,MovementType,QtyChange,BalanceAfter,ReferenceId,Remarks,CreatedBy)
                SELECT ProductId,@Type,@Qty,StockQty,@Return,@Reason,@User FROM dbo.Products WHERE ProductId=@Product;
                INSERT dbo.ActivityLog(ActivityType,Description,CreatedBy,CreatedAt) VALUES(N'Return',@Description,@User,SYSDATETIME());
                """,("@Qty",stockChange),("@Product",product),("@Type",condition=="Usable"?"RETURN":"BO"),("@Return",returnId),
                ("@Reason",$"Return #{returnId}: {condition}; {reason}"),("@User",user),("@Description",$"Return #{returnId}, sale #{saleId}: {quantity:0.###} returned ({condition}), refund ₱{refund:N2}."))) cmd.ExecuteNonQuery();
            tx.Commit(); return refund;
        }
        catch { tx.Rollback(); throw; }
    }

    public List<BadStockRow> GetBadStock(int? supplier=null)
    {
        using var conn=DbConnectionFactory.Create(); conn.Open();
        using var cmd=Command(conn,null,"""
            SELECT bo.BadStockId,p.ProductName,bo.BatchId,s.CompanyName,bo.CreatedAt,bo.Quantity,bo.RemainingQuantity,p.UnitOfMeasure,bo.Reason,
                   p.StockQty,ISNULL(totals.BoQty,0)
            FROM dbo.BadStock bo JOIN dbo.Products p ON p.ProductId=bo.ProductId
            LEFT JOIN dbo.InventoryBatches b ON b.BatchId=bo.BatchId LEFT JOIN dbo.Suppliers s ON s.SupplierId=b.SupplierId
            OUTER APPLY (SELECT SUM(RemainingQuantity) AS BoQty FROM dbo.BadStock WHERE ProductId=p.ProductId) totals
            WHERE (@Supplier IS NULL OR b.SupplierId=@Supplier) ORDER BY bo.CreatedAt DESC,bo.BadStockId DESC;
            """,("@Supplier",supplier is >0 ? supplier : null));
        using var r=cmd.ExecuteReader(); var rows=new List<BadStockRow>();
        while(r.Read()) rows.Add(new BadStockRow {BadStockId=r.GetInt32(0),ProductName=r.GetString(1),BatchId=r.IsDBNull(2)?null:r.GetInt32(2),
            SupplierName=r.IsDBNull(3)?"Supplier/delivery unknown":r.GetString(3),CreatedAt=r.GetDateTime(4),Quantity=r.GetDecimal(5),
            RemainingQuantity=r.GetDecimal(6),Unit=r.GetString(7),Reason=r.GetString(8),
            SaleableQty=r.GetDecimal(9),ProductBoQty=r.GetDecimal(10)});
        return rows;
    }

    public List<BadStockDisposition> GetDispositions(int badStockId)
    {
        using var conn=DbConnectionFactory.Create(); conn.Open();
        using var cmd=Command(conn,null,"""
            SELECT d.CreatedAt,d.Action,d.Quantity,d.Reason,ISNULL(u.FullName,N'')
            FROM dbo.BadStockDispositions d
            LEFT JOIN dbo.Users u ON u.UserId=d.CreatedBy
            WHERE d.BadStockId=@Id
            ORDER BY d.CreatedAt DESC,d.DispositionId DESC;
            """,("@Id",badStockId));
        using var r=cmd.ExecuteReader(); var rows=new List<BadStockDisposition>();
        while(r.Read()) rows.Add(new BadStockDisposition {CreatedAt=r.GetDateTime(0),Action=r.GetString(1),Quantity=r.GetDecimal(2),
            Reason=r.GetString(3),CreatedByName=r.GetString(4)});
        return rows;
    }

    public void DisposeBadStock(int badStockId,decimal quantity,string action,string reason)
    {
        RequireAdmin();
        if(action is not ("Disposed" or "ReturnedToSupplier")) throw new InvalidOperationException("Select a BO action.");
        reason=reason.Trim(); if(reason.Length is 0 or >300) throw new InvalidOperationException("Enter a reason (up to 300 characters).");
        using var conn=DbConnectionFactory.Create(); conn.Open(); using var tx=conn.BeginTransaction();
        try
        {
            bool allowsFractional; decimal remaining;
            using(var read=Command(conn,tx,"""
                SELECT p.AllowsFractional,b.RemainingQuantity
                FROM dbo.BadStock b WITH(UPDLOCK)
                JOIN dbo.Products p ON p.ProductId=b.ProductId
                WHERE b.BadStockId=@Id;
                """,("@Id",badStockId)))
            using(var row=read.ExecuteReader())
            {
                if(!row.Read()) throw new InvalidOperationException("BO entry not found.");
                allowsFractional=row.GetBoolean(0); remaining=row.GetDecimal(1);
            }
            InventoryRules.ValidateQuantity(quantity,allowsFractional);
            if(quantity>remaining) throw new InvalidOperationException("BO quantity exceeds the remaining defective stock.");
            using var cmd=Command(conn,tx,"""
                UPDATE dbo.BadStock SET RemainingQuantity=RemainingQuantity-@Qty
                WHERE BadStockId=@Id AND RemainingQuantity>=@Qty;
                IF @@ROWCOUNT=0 THROW 50110,'BO quantity exceeds the remaining defective stock.',1;
                INSERT dbo.BadStockDispositions(BadStockId,Quantity,Action,Reason,CreatedBy) VALUES(@Id,@Qty,@Action,@Reason,@User);
                INSERT dbo.ActivityLog(ActivityType,Description,CreatedBy,CreatedAt) VALUES(N'BO',@Description,@User,SYSDATETIME());
                """,("@Id",badStockId),("@Qty",quantity),("@Action",action),("@Reason",reason),("@User",SessionManager.CurrentUser!.UserId),
                ("@Description",$"BO #{badStockId}: {action}, {quantity:0.###}. {reason}"));
            cmd.ExecuteNonQuery(); tx.Commit();
        }
        catch { tx.Rollback(); throw; }
    }

}
