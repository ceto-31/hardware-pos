# Approved inventory, POS, returns, and reports update

## Product and POS flow

- Products now have brand, color, and specifications, alongside separate category and unit fields. Different variants remain separate product records.
- POS has separate Category and Unit filters. Select a cart line, enter its quantity, and choose Apply. Kilogram products offer 0.5 kg and 1 kg shortcuts.
- Quantities support three decimal places when the product allows fractions. Whole-unit products reject fractional quantities. Receipts and stock screens preserve fractional quantities.
- A half-kilo purchase from 10 kg leaves 9.5 kg. Checkout checks stock again inside its database transaction to prevent overselling.

## Deliveries and inventory

- Stock In shows current stock, incoming quantity, and the resulting balance before saving.
- Each delivery records its supplier, date, original quantity, remaining quantity, and optional expiry. The same product can have deliveries from multiple suppliers.
- Supplier Deliveries filters those records by supplier and product. The Stock In form also shows earlier deliveries for the selected product.
- Sales consume available batches in receipt-date order (FIFO). Voids and usable returns restore their original allocations when available.
- Stock Out can use a selected delivery or FIFO. Damaged stock moves into BO and is recorded once in the activity log, with the user, quantity, reason, and date. BO records retain the delivery/supplier when known.
- Saleable stock is the quantity available to sell. BO is the remaining defective quantity. Total physical stock is saleable plus remaining BO. Stock Monitoring shows all three. Disposing BO or returning it to the supplier reduces only the remaining BO quantity.
- Stock Out "Returned to supplier (good stock)" deducts saleable stock. If that product still has BO, the screen asks for confirmation, because damaged goods should leave through the BO tab. The BO tab lists each disposal and supplier return.
- Supplier attribution follows the recorded allocation. Staff must follow the same physical stock rotation for it to identify the actual delivery reliably.

## Returns and voids

- An administrator selects the original sale in POS history and opens Return. Each item can be returned partially or fully, up to its remaining unreturned quantity.
- Returns are allowed through the exact seven-day deadline after the sale timestamp, using the database clock. A partial return does not restart the deadline.
- Refunds use the original paid amount, proportionally including the original tax and discounts. Cumulative rounding ensures multiple partial refunds cannot exceed that amount.
- Usable items return to saleable stock. Defective items enter BO. A reason and administrator identity are recorded.
- Administrators can void completed sales; cashiers can void only their own completed transactions. A reason is required. The database checks ownership and records the logged-in user's identity. Voids restore stock once; a sale that already has a customer return cannot also be voided.
- Exchange settlement and supplier credit/replacement accounting are outside this update. Return-to-supplier here records the movement of defective goods.

## Reports and permissions

- Today's Sales is available to administrators and cashiers. It shows only the current day's store sales, refunds, net sales, receipt/product details, and top products. Refresh today reloads the current date; other daily-range selections do not affect it.
- Daily, weekly, monthly, and yearly reports use their selected period and show sales, refunds, net sales, and the best-selling products. Product rankings can use net sales amount or net quantity; quantities retain their units.
- Weekly sales use a month and a year. Each week stays inside that month: days 1–7, 8–14, 15–21, 22–28, and day 29 through the last day when the month is longer. Labels read "Week 1 — Feb 1–7". Weekly totals match that month's daily totals.
- Yearly sales use one year. All months lists that year's twelve months, titled "Yearly Sales — 2026". Choosing a month lists only that month's days, titled "February 2026 Sales", including its transactions and top products. The PDF states the year, month, exact date range, sales before refunds, refunds, net sales, transaction count, and top products.
- Daily reports support a date-range summary and a single-day receipt/product detail report.
- Refunds reduce net sales on the date the return is processed. Voided transactions are excluded. Product totals and detail lines reconcile with net sales.
- Administrators open an in-system PDF preview before saving or printing. The saved PDF is the same document shown in preview.
- Cashiers can view all-store sales reports and change viewing filters. Report tables are read-only; cashier previews have no Print or Save PDF controls. Stock reports and customer-return actions remain restricted to administrators.

## Archives and pagination

- Archives now has record counts, clearer Restore/Delete actions, product variant details, fractional stock quantities, and helpful empty states for products and suppliers.
- Stock Out shows placeholders for Category, Product, Delivery, and Reason. Reason starts unselected. The optional delivery can be cleared to use oldest stock first.
- Inventory History, Supplier Deliveries, BO, Products, Archives, and POS Transaction History use pagination with 10, 25, or 50 rows per page and First/Previous/Next/Last controls.
- Searches and filters reset to the first page; sorting applies to the complete filtered result. Changing pages clears the selected row to avoid acting on a hidden selection.
- Transaction-history pagination is available to both administrators and cashiers. Inventory history includes older records beyond the previous 200-row display limit.

## Upgrade and verification

### Dashboard presentation

- The dashboard highlights today's net sales, today's completed transactions, active products, and products needing restocking. Refresh reloads the figures and shows the update time.
- Administrators see a wider sales chart with period totals and a ranked product list by all-time net sales. Panels stack in narrower windows.
- Inventory alerts, recent transactions, expiry alerts, and promotions use readable lists with explicit empty states. New Sale and Sales Reports shortcuts provide quick navigation.
- The existing margin calculation is labeled as an estimate before refunds and order discounts; it uses current product costs. Quantities remain attached to their product units.

### Build and checks

- The rebuilt application's next startup runs the embedded `Database/06_InventoryReturns.sql` upgrade after the existing schema updater. It is transactional and safe to run again. A failed upgrade stops startup instead of running with a partially upgraded schema.
- Existing stock is seeded once as an opening batch with unknown supplier/delivery. Historical records remain intact; supplier attribution is not guessed from a product's preferred supplier.
- Unit changes are blocked after stock history exists. Fractional sales retain fractional-return support.
- Verification uses a newly created `HardwarePOS_Checks_<GUID>` database on the configured SQL Server, then drops only that temporary database. The real store database has not been upgraded by these tests.
- Run `dotnet build HardwarePOS.slnx --no-restore` and `dotnet run --project tests/HardwarePOS.Checks --no-restore -- --database --visual` from the repository root.
- The verification runner covers quantity precision, supplier allocations, returns, deadline enforcement, BO, voids, concurrency, permissions, and report reconciliation. It also renders WPF screens and every generated PDF page into the test output's `verification` folder.
- Offscreen screen renders do not verify live chart animation. Physical printer output still needs checking on the store's printer.
