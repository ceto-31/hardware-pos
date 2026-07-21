-- Backfill opening inventory ledger for products that have stock but no ledger history.
-- Safe to re-run: only inserts when a product has zero ledger rows.
USE HardwarePOS;
GO

INSERT INTO dbo.InventoryLedger
    (ProductId, MovementType, QtyChange, BalanceAfter, ReferenceId, Remarks, CreatedBy)
SELECT
    p.ProductId,
    N'IN',
    p.StockQty,
    p.StockQty,
    NULL,
    N'Opening stock (backfill)',
    NULL
FROM dbo.Products p
WHERE p.StockQty > 0
  AND NOT EXISTS (
        SELECT 1 FROM dbo.InventoryLedger l WHERE l.ProductId = p.ProductId
      );
GO

PRINT N'Opening ledger backfill complete.';
GO
