using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using HardwarePOS.Data;
using HardwarePOS.Helpers;
using HardwarePOS.Models;
using HardwarePOS.Services;
using Microsoft.Data.SqlClient;

internal static class Program
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool TerminateProcess(nint processHandle,uint code);
    private static int _checks;
    private static void Equal<T>(T expected,T actual,string name)
    {
        if(!Equals(expected,actual)) throw new Exception($"{name}: expected {expected}, actual {actual}");
        _checks++; Console.WriteLine($"PASS {name}");
    }
    private static void Reject(Action action,string name)
    {
        try{action();}catch(InvalidOperationException){_checks++;Console.WriteLine($"PASS {name}");return;}
        throw new Exception($"{name}: expected rejection");
    }
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var paged = new PagedList<Product>();
            paged.SetSource(Enumerable.Range(1, 23).Select(i => new Product { ProductId=i, ProductName=$"Product {i:00}" }));
            Equal(10,paged.Items.Count,"Pagination limits displayed rows");
            Equal(3,paged.TotalPages,"Pagination counts partial final page");
            Equal(false,paged.PreviousCommand.CanExecute(null),"First page disables previous");
            paged.NextCommand.Execute(null);
            Equal(11,paged.Items.First().ProductId,"Next page starts after preceding records");
            paged.LastCommand.Execute(null);
            Equal(3,paged.Items.Count,"Last page contains remaining records");
            Equal(false,paged.NextCommand.CanExecute(null),"Last page disables next");
            paged.Sort(nameof(Product.ProductName),System.ComponentModel.ListSortDirection.Descending);
            Equal(23,paged.Items.First().ProductId,"Sorting applies across all pages");
            paged.PageSize=25;
            Equal(23,paged.Items.Count,"Page size changes visible record count");
            paged.PageSize=10;paged.LastCommand.Execute(null);
            paged.SetSource([new Product {ProductId=100}]);
            Equal(1,paged.Page,"Filtering resets to first page");
            paged.SetSource(Enumerable.Range(1,21).Select(i=>new Product {ProductId=i}));paged.LastCommand.Execute(null);
            paged.SetSource([new Product {ProductId=1}],resetPage:false);
            Equal(1,paged.Page,"Shrinking results clamp the page");
            paged.SetSource([]);
            Equal("No records",paged.RangeLabel,"Empty pagination status");
            Equal(false,paged.NextCommand.CanExecute(null),"Empty results disable navigation");
            Equal(true,InventoryRules.IsReturnEligible(new DateTime(2026,10,7,14,0,0),new DateTime(2026,10,14,14,0,0)),"Return at exact seven-day deadline");
            Equal(false,InventoryRules.IsReturnEligible(new DateTime(2026,10,7,14,0,0),new DateTime(2026,10,14,14,0,1)),"Return after deadline blocked");
            Reject(()=>InventoryRules.ValidateQuantity(0),"Zero quantity rejected");
            Reject(()=>InventoryRules.ValidateQuantity(-1),"Negative quantity rejected");
            Reject(()=>InventoryRules.ValidateQuantity(.0001m),"Excess precision rejected");
            Reject(()=>InventoryRules.ValidateQuantity(.5m,false),"Fractional pieces rejected");
            Equal(9.5m,10m-.5m,"Half-kilo balance");
            var shares=InventoryRules.AllocatePaidTotal([1m,1m,1m],1m);
            Equal(1m,shares.Sum(),"Line allocations preserve paid total");
            var first=InventoryRules.Refund(1m,3m,0,1,0);
            var second=InventoryRules.Refund(1m,3m,1,1,first);
            var third=InventoryRules.Refund(1m,3m,2,1,first+second);
            Equal(1m,first+second+third,"Partial refunds preserve final penny");
            Reject(()=>InventoryRules.Refund(100,2,1,2,50),"Over-return blocked");
            var day=new DateTime(2026,1,1);
            var totals=new[] {new DailyTotal {Date=day,Amount=100,TransactionCount=1},new DailyTotal {Date=day.AddDays(1),Amount=-20,RefundAmount=20}};
            var daily=SalesReportBuilder.BuildDaily(totals,day,day.AddDays(1),day,day.AddDays(10));
            Equal(80m,daily.TotalSales,"Daily net sales"); Equal(100m,daily.SalesAmount,"Sales before refunds"); Equal(20m,daily.RefundAmount,"Refunds reported by processing date");
            Equal(daily.TotalSales,SalesReportBuilder.BuildWeekly(totals,2026,1,day,day.AddDays(10)).TotalSales,"Weekly reconciliation");
            Equal(daily.TotalSales,SalesReportBuilder.BuildMonthly(totals,2026,day,day.AddDays(10)).TotalSales,"Monthly reconciliation");
            Equal(daily.TotalSales,SalesReportBuilder.BuildMonthly(totals,2026,day,day.AddDays(10)).TotalSales,"Yearly all months reconciliation");
            var february=SalesReportBuilder.BuildDaily(totals,new DateTime(2026,2,1),new DateTime(2026,2,28),day,new DateTime(2026,3,1));
            Equal(true,february.Periods.All(p=>p.Start.Month==2 && p.End.Month==2),"Yearly February stays inside February");
            Equal(0m,february.TotalSales,"February excludes January sales");
            static string WeekLabel(int week,DateTime start,DateTime end)=>
                $"Week {week} \u2014 {start.ToString("MMM d",SalesReportText.Culture)}\u2013{end.Day.ToString(SalesReportText.Culture)}";
            void ExpectWeeks(int year,int month,int count,DateTime lastStart,DateTime lastEnd)
            {
                var built=SalesReportBuilder.BuildWeekly([],year,month,new DateTime(year,1,1),new DateTime(year,12,31));
                Equal(count,built.Periods.Count,$"{year}-{month:00} week count");
                Equal(WeekLabel(1,new DateTime(year,month,1),new DateTime(year,month,7)),built.Periods[0].FullLabel,$"{year}-{month:00} first week");
                Equal(WeekLabel(count,lastStart,lastEnd),built.Periods[^1].FullLabel,$"{year}-{month:00} last week");
                Equal(lastStart,built.Periods[^1].Start,$"{year}-{month:00} last week start");
                Equal(lastEnd,built.Periods[^1].End,$"{year}-{month:00} last week end");
                Equal(SalesPeriodStatus.Complete,built.Periods[^1].Status,$"{year}-{month:00} last week is not partial");
            }
            ExpectWeeks(2025,2,4,new DateTime(2025,2,22),new DateTime(2025,2,28));
            ExpectWeeks(2024,2,5,new DateTime(2024,2,29),new DateTime(2024,2,29));
            ExpectWeeks(2026,4,5,new DateTime(2026,4,29),new DateTime(2026,4,30));
            ExpectWeeks(2026,1,5,new DateTime(2026,1,29),new DateTime(2026,1,31));
            var leapMonth=SalesReportBuilder.BuildDaily([],new DateTime(2024,2,1),new DateTime(2024,2,29),new DateTime(2024,1,1),new DateTime(2024,3,1));
            Equal(29,leapMonth.Periods.Count,"February 2024 has 29 days");
            Equal(new DateTime(2024,2,29),leapMonth.Periods[^1].Start,"Leap day stays in February");
            Equal(true,leapMonth.Periods.All(p=>p.Start.Month==2 && p.End.Month==2),"Leap-month days stay inside February");
            var march=SalesReportBuilder.BuildDaily([],new DateTime(2026,3,1),new DateTime(2026,3,31),new DateTime(2026,1,1),new DateTime(2026,4,1));
            Equal(31,march.Periods.Count,"March 2026 has 31 days");
            Equal(true,march.Periods.All(p=>p.Start.Month==3),"March days stay inside March");
            var year2025=SalesReportBuilder.BuildMonthly([],2025,new DateTime(2025,1,1),new DateTime(2026,1,1));
            Equal(new DateTime(2025,1,1),year2025.RangeStart,"2025 starts on January 1");
            Equal(new DateTime(2025,12,31),year2025.RangeEnd,"2025 ends on December 31");
            Equal(12,year2025.Periods.Count,"2025 lists every month");
            var stockDay=StockReportRange.Day(new DateTime(2026,10,12));
            Equal(new DateTime(2026,10,12),stockDay.Start,"Stock daily starts at midnight");
            Equal(new DateTime(2026,10,13),stockDay.End,"Stock daily ends at the next midnight");
            Equal("October 12, 2026",stockDay.Label,"Stock daily label");
            Equal(true,stockDay.Contains(new DateTime(2026,10,12,23,59,59)),"Stock daily includes the last second");
            Equal(false,stockDay.Contains(new DateTime(2026,10,13)),"Stock daily excludes the next day");
            var stockWeek=StockReportRange.Week(new DateTime(2026,10,14));
            Equal(new DateTime(2026,10,12),stockWeek.Start,"Stock week starts Monday");
            Equal(new DateTime(2026,10,19),stockWeek.End,"Stock week ends the next Monday");
            Equal("Week of October 12\u201318, 2026",stockWeek.Label,"Stock week label");
            Equal(true,stockWeek.Contains(new DateTime(2026,10,18,23,59,0)),"Stock week includes Sunday");
            Equal(false,stockWeek.Contains(new DateTime(2026,10,19)),"Stock week excludes the next Monday");
            var crossing=StockReportRange.Week(new DateTime(2026,12,31));
            Equal(new DateTime(2026,12,28),crossing.Start,"Year-end week starts on its Monday");
            Equal(new DateTime(2027,1,4),crossing.End,"Year-end week ends the following Monday");
            Equal("Week of December 28, 2026\u2013January 3, 2027",crossing.Label,"Year-end week label");
            var leap=StockReportRange.Month(2024,2);
            Equal(new DateTime(2024,3,1),leap.End,"Leap-year February ends on March 1");
            Equal(true,leap.Contains(new DateTime(2024,2,29,18,0,0)),"Leap day stays in February");
            Equal(false,leap.Contains(new DateTime(2024,3,1)),"March 1 stays out of February");
            var longMonth=StockReportRange.Month(2026,3);
            Equal(new DateTime(2026,4,1),longMonth.End,"March ends on April 1");
            Equal("March 2026",longMonth.Label,"Monthly stock label");
            var stockYear=StockReportRange.Year(2025);
            Equal(new DateTime(2025,1,1),stockYear.Start,"Stock year starts January 1");
            Equal(new DateTime(2026,1,1),stockYear.End,"Stock year ends the next January 1");
            Equal("Year 2025",stockYear.Label,"Yearly stock label");
            Equal(false,stockYear.Contains(new DateTime(2026,1,1)),"Next New Year is excluded");
            var zone=TimeZoneInfo.Local;
            var insideUtc=TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026,10,12,23,30,0),zone);
            var outsideUtc=TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026,10,13,0,30,0),zone);
            Equal(true,stockDay.ContainsUtc(insideUtc,zone),"Local late evening stays on the selected day");
            Equal(false,stockDay.ContainsUtc(outsideUtc,zone),"The next local morning stays out of the selected day");
            if(args.Contains("--database")) DatabaseChecks(args.Contains("--visual"));
            Console.WriteLine($"{_checks} checks passed.");
            // Offscreen WPF/Skia shutdown handlers can wait on an absent dispatcher loop.
            // SQL cleanup and file writes have completed; terminate only this test host.
            if(args.Contains("--visual")) { Console.Out.Flush(); TerminateProcess(new nint(-1),0); }
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex.ToString());if(args.Contains("--visual")){Console.Error.Flush();TerminateProcess(new nint(-1),1);}return 1;}
    }

    private static void DatabaseChecks(bool visual)
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
        using var config=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"src/HardwarePOS/appsettings.json")));
        var cs=new SqlConnectionStringBuilder(config.RootElement.GetProperty("ConnectionStrings").GetProperty("HardwarePOS").GetString());
        var database="HardwarePOS_Checks_"+Guid.NewGuid().ToString("N");
        cs.InitialCatalog="master";
        using var master=new SqlConnection(cs.ConnectionString);master.Open();
        using(var create=master.CreateCommand()){create.CommandText=$"CREATE DATABASE [{database}];";create.ExecuteNonQuery();}
        cs.InitialCatalog=database;
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"appsettings.json"),JsonSerializer.Serialize(new {ConnectionStrings=new {HardwarePOS=cs.ConnectionString}}));
        try
        {
            using var conn=new SqlConnection(cs.ConnectionString);conn.Open();
            void Execute(string sql) {using var cmd=conn.CreateCommand();cmd.CommandText=sql;cmd.ExecuteNonQuery();}
            decimal Number(string sql){using var cmd=conn.CreateCommand();cmd.CommandText=sql;return Convert.ToDecimal(cmd.ExecuteScalar());}
            foreach(var file in new[]{"02_CreateTables.sql","05_UpgradeSchema.sql"})
            {
                var script=File.ReadAllText(Path.Combine(root,"Database",file));
                script=Regex.Replace(script,@"^USE HardwarePOS;\s*$","",RegexOptions.Multiline);
                foreach(var batch in Regex.Split(script,@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase))
                    if(!string.IsNullOrWhiteSpace(batch)) Execute(batch);
            }
            Execute("""
                INSERT dbo.Roles(RoleName) VALUES('Admin'),('Cashier');
                INSERT dbo.Users(Username,PasswordHash,PasswordSalt,FullName,RoleId) VALUES('check-admin',0x00,0x00,'Check admin',1),('check-cashier',0x00,0x00,'Check cashier',2);
                INSERT dbo.Suppliers(CompanyName) VALUES('Supplier A'),('Supplier B');
                INSERT dbo.Categories(CategoryName) VALUES('Fasteners'),('Paint');
                INSERT dbo.Products(ProductName,ProductCode,UnitOfMeasure,StockQty,CategoryId,UnitId) VALUES('Legacy nails','LEGACY','Kilogram',10,1,(SELECT UnitId FROM dbo.Units WHERE UnitName='Kilogram'));
                """);
            InventorySchemaUpgrade.Ensure();InventorySchemaUpgrade.Ensure();
            Equal(1m,Number("SELECT COUNT(*) FROM dbo.InventoryBatches WHERE ProductId=1"),"Upgrade idempotence");
            Equal(1m,Number("SELECT COUNT(*) FROM dbo.InventoryBatches WHERE SupplierId IS NULL AND ProductId=1"),"Legacy provenance stays unknown");
            SessionManager.SignIn(new UserAccount {UserId=1,RoleName="Admin",FullName="Check admin",IsActive=true});
            var products=new ProductRepository();var inventory=new InventoryRepository();var sales=new SalesRepository();var returns=new ReturnsRepository();
            var kg=(int)Number("SELECT UnitId FROM dbo.Units WHERE UnitName='Kilogram'");
            var product=products.Insert(new Product {ProductName="Nails",ProductCode="NAILS",UnitId=kg,UnitOfMeasure="Kilogram",CategoryId=1,Brand="Brand A",Color="Silver",ProductDetails="2 inch",AllowsFractional=true,SellingPrice=100});
            inventory.StockIn(1,product,10,DateTime.Today.AddDays(-2),null,"First delivery",1);
            inventory.StockIn(2,product,10,DateTime.Today.AddDays(-1),null,"Second delivery",1);
            var batches=BatchStock.GetBatches(product);
            Equal(2,batches.Count,"Two suppliers keep separate deliveries");Equal(20m,products.GetById(product)!.StockQty,"Combined product balance");
            Equal("Brand A",products.GetById(product)!.Brand,"Product brand persistence");
            Equal(1,products.GetAll(categoryId:1,unitId:kg).Count(p=>p.ProductId==product),"Combined category and unit filtering");
            string Sell(decimal qty)=>sales.CompleteSale(1,[new CartItem {ProductId=product,ProductName="Nails",UnitOfMeasure="Kilogram",UnitPrice=100,RegularUnitPrice=100,Quantity=qty}],100*qty,0,0,100*qty,100*qty,0,"Pickup",null,null,null);
            var invoice=Sell(.5m);var sale=sales.GetHistory().Single(s=>s.InvoiceNo==invoice);
            Equal(19.5m,products.GetById(product)!.StockQty,"Checkout saves fractional stock");
            Equal(9.5m,BatchStock.GetBatches(product)[0].RemainingQuantity,"FIFO deducts first delivery");
            Equal(10m,BatchStock.GetBatches(product)[1].RemainingQuantity,"Second supplier untouched");
            var line=returns.GetItems(sale.SaleId).Single();
            Equal(25m,returns.Return(sale.SaleId,line.SaleItemId,.25m,"Usable","Customer changed mind"),"Partial refund amount");
            Equal(19.75m,products.GetById(product)!.StockQty,"Usable return restores saleable stock");
            Reject(()=>sales.VoidSale(sale.SaleId,1,"Admin","Test"),"Void after return blocked");
            Equal(25m,returns.Return(sale.SaleId,line.SaleItemId,.25m,"Defective","Defective nails"),"Defective refund amount");
            Equal(19.75m,products.GetById(product)!.StockQty,"Defective return does not restore saleable stock");
            var bo=returns.GetBadStock().First();Equal(batches[0].BatchId,bo.BatchId!.Value,"BO retains original supplier delivery");
            Reject(()=>returns.Return(sale.SaleId,line.SaleItemId,.001m,"Usable","Repeat"),"Duplicate full return blocked");
            returns.DisposeBadStock(bo.BadStockId,.25m,"ReturnedToSupplier","Sent to Supplier A");
            Equal(19.75m,products.GetById(product)!.StockQty,"BO disposition does not double-deduct stock");
            inventory.StockOut(product,1,"Damaged",DateTime.Today,"Damaged on shelf",1,batches[1].BatchId);
            Equal(18.75m,products.GetById(product)!.StockQty,"Damage removes saleable stock once");
            Equal(9m,BatchStock.GetBatches(product)[1].RemainingQuantity,"Damage targets specified delivery");
            var voidInvoice=Sell(1);var voidSale=sales.GetHistory().Single(s=>s.InvoiceNo==voidInvoice);sales.VoidSale(voidSale.SaleId,1,"Admin","Wrong transaction");
            Equal(18.75m,products.GetById(product)!.StockQty,"Void restores balance");
            Equal(18.75m,BatchStock.GetBatches(product).Sum(b=>b.RemainingQuantity),"Void restores batch balances");
            Reject(()=>sales.VoidSale(voidSale.SaleId,1,"Admin","Again"),"Duplicate void blocked");
            var expiredInvoice=Sell(1);var expired=sales.GetHistory().Single(s=>s.InvoiceNo==expiredInvoice);
            Execute($"UPDATE dbo.Sales SET SaleDate=DATEADD(day,-8,SYSDATETIME()) WHERE SaleId={expired.SaleId};");
            Reject(()=>returns.Return(expired.SaleId,returns.GetItems(expired.SaleId).Single().SaleItemId,1,"Usable","Late"),"Database rejects late return");
            var reports=new SalesReportRepository();var range=reports.GetDailyTotals(DateTime.Today.AddDays(-10),DateTime.Today);
            Equal(100m,range.Sum(d=>d.Amount),"Report net totals exclude void and deduct refunds");
            Equal(100m,reports.GetDetails(DateTime.Today.AddDays(-10),DateTime.Today).Sum(d=>d.Amount),"Detail totals reconcile");
            Equal(100m,reports.GetProducts(DateTime.Today.AddDays(-10),DateTime.Today).Sum(d=>d.NetSales),"Product totals reconcile");
            var snapshot=reports.Capture(DateTime.Today.AddDays(-10),DateTime.Today,true);
            Equal(snapshot.Totals.Sum(d=>d.Amount),snapshot.Details.Sum(d=>d.Amount),"Captured detail matches sales snapshot");
            Equal(snapshot.Totals.Sum(d=>d.Amount),snapshot.Products.Sum(d=>d.NetSales),"Captured products match sales snapshot");
            Equal(17.75m,BatchStock.GetBatches(product).Sum(b=>b.RemainingQuantity),"Final batch balance");
            Equal(17.75m,products.GetById(product)!.StockQty,"Final product balance");
            SessionManager.SignIn(new UserAccount {UserId=2,RoleName="Cashier"});
            Reject(()=>returns.Return(expired.SaleId,line.SaleItemId,1,"Usable","Cashier"),"Cashier cannot process returns");
            Reject(()=>sales.VoidSale(expired.SaleId,2,"Cashier","Cashier"),"Cashier cannot void another user's transaction");
            Equal(true,reports.GetDailyTotals(DateTime.Today.AddDays(-10),DateTime.Today).Count>0,"Cashier can read store sales");
            SessionManager.SignIn(new UserAccount {UserId=1,RoleName="Admin",FullName="Check admin",IsActive=true});
            InventorySchemaUpgrade.Ensure();
            Equal(17.75m,BatchStock.GetBatches(product).Sum(b=>b.RemainingQuantity),"Repeat upgrade preserves operational data");
            // A sale spanning deliveries must restore the exact original allocations.
            var spanningInvoice=Sell(10);var spanning=sales.GetHistory().Single(s=>s.InvoiceNo==spanningInvoice);
            Equal(2m,Number($"SELECT COUNT(*) FROM dbo.SaleAllocations a JOIN dbo.SaleItems si ON si.SaleItemId=a.SaleItemId WHERE si.SaleId={spanning.SaleId}"),"Large sale spans supplier deliveries");
            sales.VoidSale(spanning.SaleId,1,"Admin","Allocation test");
            Equal(17.75m,BatchStock.GetBatches(product).Sum(b=>b.RemainingQuantity),"Multi-delivery void restores every batch");
            // Legacy receipts have no allocations; return attribution must stay unknown.
            Execute("""
                INSERT dbo.Sales(InvoiceNo,SaleDate,CashierId,Subtotal,TotalDue,CashTendered,ChangeAmount)
                VALUES('LEGACY-RETURN',SYSDATETIME(),1,30,30,30,0);
                INSERT dbo.SaleItems(SaleId,ProductId,Quantity,UnitPrice,LineTotal)
                VALUES(SCOPE_IDENTITY(),1,3,10,30);
                """);
            var legacy=sales.GetHistory().Single(s=>s.InvoiceNo=="LEGACY-RETURN");var legacyLine=returns.GetItems(legacy.SaleId).Single();
            Equal(10m,returns.Return(legacy.SaleId,legacyLine.SaleItemId,1,"Usable","Legacy receipt"),"Legacy refund allocation");
            Equal(true,BatchStock.GetBatches(1).All(b=>b.SupplierId is null),"Legacy return does not invent supplier");
            var piece=(int)Number("SELECT UnitId FROM dbo.Units WHERE UnitName='Piece'");
            var paint=products.Insert(new Product {ProductName="Paint",ProductCode="PAINT",UnitId=piece,CategoryId=2,SellingPrice=50});
            Reject(()=>inventory.StockIn(1,paint,.5m,DateTime.Today,null,null,1),"Stock-in respects whole-unit products");
            inventory.StockIn(1,paint,1,DateTime.Today,null,"One can",1);
            var outcomes=new System.Collections.Concurrent.ConcurrentBag<bool>();
            Parallel.For(0,2,_=>
            {
                try {sales.CompleteSale(1,[new CartItem {ProductId=paint,ProductName="Paint",Quantity=1,UnitPrice=50}],50,0,0,50,50,0,"Pickup",null,null,null);outcomes.Add(true);}
                catch(InvalidOperationException){outcomes.Add(false);}
            });
            Equal(1,outcomes.Count(x=>x),"Concurrent checkout cannot oversell");
            Equal(0m,products.GetById(paint)!.StockQty,"Concurrent stock balance remains nonnegative");
            var paintSale=sales.GetHistory().First(s=>sales.GetSaleItems(s.SaleId).Any(i=>i.ProductName=="Paint"));
            Reject(()=>returns.Return(paintSale.SaleId,returns.GetItems(paintSale.SaleId).Single().SaleItemId,.5m,"Usable","Half can"),"Customer return respects whole units");
            var beforeFailures=products.GetById(product)!.StockQty;
            Reject(()=>inventory.StockOut(product,1000,"Damaged",DateTime.Today,null,1),"Stock-out cannot exceed balance");
            Equal(beforeFailures,products.GetById(product)!.StockQty,"Rejected stock-out leaves balance unchanged");
            var all=reports.GetDailyTotals(DateTime.Today.AddDays(-10),DateTime.Today);
            Equal(all.Sum(d=>d.Amount),new DashboardRepository().GetSummary().TotalRevenue,"Dashboard and reports net sales agree");
            Equal(all.Sum(d=>d.Amount),reports.GetProducts(DateTime.Today.AddDays(-10),DateTime.Today).Sum(p=>p.NetSales),"All product allocations reconcile after legacy returns");
            Execute("""
                INSERT dbo.Sales(InvoiceNo,SaleDate,CashierId,Subtotal,TotalDue,CashTendered,ChangeAmount)
                VALUES('LEGACY-ROUNDING',SYSDATETIME(),1,3,100000,100000,0);
                DECLARE @sale int=SCOPE_IDENTITY();
                INSERT dbo.SaleItems(SaleId,ProductId,Quantity,UnitPrice,LineTotal)
                VALUES(@sale,1,1,1,1),(@sale,1,1,1,1),(@sale,1,1,1,1);
                """);
            var rounding=sales.GetHistory().Single(s=>s.InvoiceNo=="LEGACY-ROUNDING");
            foreach(var legacyItem in returns.GetItems(rounding.SaleId))
                Equal(legacyItem.Paid,Number($"SELECT PaidAmount FROM dbo.SaleLineFacts WHERE SaleItemId={legacyItem.SaleItemId}"),"Legacy SQL allocation agrees with refund calculation");
            Execute($"DELETE dbo.SaleItems WHERE SaleId={rounding.SaleId}; DELETE dbo.Sales WHERE SaleId={rounding.SaleId};");
            var ownInvoice=sales.CompleteSale(2,[new CartItem {ProductId=product,ProductName="Nails",UnitOfMeasure="Kilogram",Quantity=.5m,UnitPrice=100}],50,0,0,50,50,0,"Pickup",null,null,null);
            var ownSale=sales.GetHistory().Single(s=>s.InvoiceNo==ownInvoice);
            Equal(2,ownSale.CashierId,"Transaction history retains cashier ownership");
            var beforeVoid=products.GetById(product)!.StockQty;
            SessionManager.SignIn(new UserAccount {UserId=2,RoleName="Cashier",FullName="Check cashier",IsActive=true});
            Equal(true,SalePermissions.CanVoid(2),"Cashier menu permits own transaction");
            Equal(false,SalePermissions.CanVoid(1),"Cashier menu hides another user's void action");
            Reject(()=>sales.VoidSale(expired.SaleId,1,"Forged admin","Wrong owner"),"Caller cannot bypass ownership using admin arguments");
            sales.VoidSale(ownSale.SaleId,1,"Forged admin","Cashier correction");
            Equal(beforeVoid+.5m,products.GetById(product)!.StockQty,"Cashier void restores stock");
            Equal(2m,Number($"SELECT VoidedBy FROM dbo.Sales WHERE SaleId={ownSale.SaleId}"),"Cashier void audit uses actual session identity");
            Equal(1m,Number($"SELECT COUNT(*) FROM dbo.ActivityLog WHERE ActivityType='Void' AND CreatedBy=2"),"Cashier void writes an audit entry");
            Reject(()=>sales.VoidSale(ownSale.SaleId,2,"Cashier","Again"),"Cashier duplicate void blocked");
            SessionManager.SignOut();
            Reject(()=>sales.VoidSale(expired.SaleId,1,"Admin","Logged out"),"Signed-out sessions cannot void");
            SessionManager.SignIn(new UserAccount {UserId=1,RoleName="Admin",FullName="Check admin",IsActive=true});
            var wall=products.Insert(new Product {ProductName="Wall Paint",ProductCode="WALL",UnitId=piece,CategoryId=2,SellingPrice=80});
            inventory.StockIn(1,wall,10,DateTime.Today,null,"Ten cans",1);
            Equal(10m,products.GetById(wall)!.StockQty,"Wall paint starts saleable at 10");
            Equal(0m,products.GetById(wall)!.BoQty,"Wall paint starts with no BO");
            inventory.StockOut(wall,2,"Damaged",DateTime.Today,"Dented cans",1);
            var damaged=products.GetById(wall)!;
            Equal(8m,damaged.StockQty,"Damage deducts saleable stock once");
            Equal(2m,damaged.BoQty,"Damage moves quantity into BO");
            Equal(10m,damaged.TotalPhysicalQty,"Total physical keeps damaged quantity");
            var wallBo=returns.GetBadStock().First(b=>b.ProductName=="Wall Paint");
            Equal(8m,wallBo.SaleableQty,"BO list shows saleable stock");
            Equal(2m,wallBo.ProductBoQty,"BO list shows remaining BO");
            Equal(10m,wallBo.TotalPhysicalQty,"BO list shows total physical stock");
            Reject(()=>returns.DisposeBadStock(wallBo.BadStockId,0,"Disposed","None"),"Zero BO quantity rejected");
            Reject(()=>returns.DisposeBadStock(wallBo.BadStockId,-1,"Disposed","None"),"Negative BO quantity rejected");
            Reject(()=>returns.DisposeBadStock(wallBo.BadStockId,.5m,"Disposed","Half can"),"Fractional BO quantity rejected");
            Reject(()=>returns.DisposeBadStock(wallBo.BadStockId,3,"Disposed","Too many"),"BO quantity over the balance rejected");
            Equal(8m,products.GetById(wall)!.StockQty,"Rejected BO actions leave saleable stock");
            Equal(2m,products.GetById(wall)!.BoQty,"Rejected BO actions leave BO");
            returns.DisposeBadStock(wallBo.BadStockId,2,"Disposed","Thrown away");
            Equal(8m,products.GetById(wall)!.StockQty,"Disposal does not deduct saleable stock");
            Equal(0m,products.GetById(wall)!.BoQty,"Disposal clears remaining BO");
            Equal(8m,products.GetById(wall)!.TotalPhysicalQty,"Total physical after disposal is saleable only");
            var boHistory=returns.GetDispositions(wallBo.BadStockId);
            Equal(1,boHistory.Count,"BO history records the disposal");
            Equal("Disposed",boHistory[0].Action,"BO history action");
            Equal(2m,boHistory[0].Quantity,"BO history quantity");
            Equal("Thrown away",boHistory[0].Reason,"BO history reason");
            Equal("Check admin",boHistory[0].CreatedByName,"BO history user");
            Equal(1m,Number("SELECT COUNT(*) FROM dbo.ActivityLog WHERE ActivityType=N'BO' AND CreatedBy=1 AND Description LIKE N'%Damaged to BO: 2 Wall Paint%' AND Description LIKE N'%Dented cans%'"),"Damage to BO is audited");
            Equal(1m,Number($"SELECT COUNT(*) FROM dbo.ActivityLog WHERE ActivityType=N'BO' AND CreatedBy=1 AND Description LIKE N'%BO #{wallBo.BadStockId}: Disposed, 2.%' AND Description LIKE N'%Thrown away%'"),"BO disposal is audited");
            Execute($"""
                INSERT dbo.Sales(InvoiceNo,SaleDate,CashierId,Subtotal,TotalDue,CashTendered,ChangeAmount)
                VALUES('FEB-SALE','2026-02-10',1,40,40,40,0);
                DECLARE @sale int=SCOPE_IDENTITY();
                INSERT dbo.SaleItems(SaleId,ProductId,Quantity,UnitPrice,LineTotal)
                VALUES(@sale,{product},1,40,40);
                DECLARE @item int=SCOPE_IDENTITY();
                INSERT dbo.CustomerReturns(SaleItemId,Quantity,RefundAmount,ItemCondition,Reason,CreatedBy,CreatedAt)
                VALUES(@item,1,15,'Defective','Late damage',1,'2026-03-10');
                INSERT dbo.Sales(InvoiceNo,SaleDate,CashierId,Subtotal,TotalDue,CashTendered,ChangeAmount,IsVoided)
                VALUES('FEB-VOID','2026-02-12',1,99,99,99,0,1);
                """);
            var febTotals=reports.GetDailyTotals(new DateTime(2026,2,1),new DateTime(2026,2,28));
            var marTotals=reports.GetDailyTotals(new DateTime(2026,3,1),new DateTime(2026,3,31));
            Equal(40m,reports.GetDailyTotals(new DateTime(2026,2,10),new DateTime(2026,2,10)).Sum(d=>d.Amount),"October-style single day includes only that date");
            Equal(0m,reports.GetDailyTotals(new DateTime(2026,2,11),new DateTime(2026,2,11)).Sum(d=>d.Amount),"The following day excludes that sale");
            Equal(40m,febTotals.Sum(d=>d.Amount),"February sales exclude the void and the March refund");
            Equal(0m,febTotals.Sum(d=>d.RefundAmount),"February has no processed refund");
            Equal(-15m,marTotals.Sum(d=>d.Amount),"March deducts the refund on its processing date");
            Equal(15m,marTotals.Sum(d=>d.RefundAmount),"March reports the refund amount");
            Equal(febTotals.Sum(d=>d.Amount),SalesReportBuilder.BuildWeekly(febTotals,2026,2,new DateTime(2026,1,1),new DateTime(2026,10,9)).TotalSales,"Weekly February matches its daily totals");
            var yearTotals=reports.GetDailyTotals(new DateTime(2026,1,1),new DateTime(2026,12,31));
            Equal(yearTotals.Sum(d=>d.Amount),SalesReportBuilder.BuildMonthly(yearTotals,2026,new DateTime(2026,1,1),DateTime.Today).TotalSales,"Yearly all months matches daily totals");
            var februaryRows=SalesReportBuilder.BuildDaily(febTotals,new DateTime(2026,2,1),new DateTime(2026,2,28),new DateTime(2026,1,1),DateTime.Today);
            Equal(true,februaryRows.Periods.All(p=>p.Start.Month==2 && p.End.Month==2),"Stored February rows stay inside February");
            SessionManager.SignIn(new UserAccount {UserId=2,RoleName="Cashier",FullName="Check cashier",IsActive=true});
            Equal(40m,new SalesReportRepository().GetDailyTotals(new DateTime(2026,2,1),new DateTime(2026,2,28)).Sum(d=>d.Amount),"Cashier can read February store sales");
            SessionManager.SignIn(new UserAccount {UserId=1,RoleName="Admin",FullName="Check admin",IsActive=true});
            var boundary=products.Insert(new Product {ProductName="Boundary Paint",ProductCode="BOUND",UnitId=piece,CategoryId=2,SellingPrice=10});
            inventory.StockIn(1,boundary,2,new DateTime(2026,3,15),new DateTime(2027,6,1),"Inside",1);
            inventory.StockIn(1,boundary,1,new DateTime(2026,3,16),null,"Outside",1);
            int StockInId(string remarks)
            {
                using var cmd=conn.CreateCommand();
                cmd.CommandText="SELECT TOP 1 StockInId FROM dbo.StockIns WHERE ProductId=@Product AND Remarks=@Remarks ORDER BY StockInId DESC;";
                cmd.Parameters.AddWithValue("@Product",boundary);
                cmd.Parameters.AddWithValue("@Remarks",remarks);
                return (int)cmd.ExecuteScalar()!;
            }
            void Stamp(string table,string idColumn,int id,DateTime utc,string movement)
            {
                using var cmd=conn.CreateCommand();
                cmd.CommandText=$"UPDATE dbo.{table} SET CreatedAt=@At WHERE {idColumn}=@Id; UPDATE dbo.InventoryLedger SET CreatedAt=@At WHERE ReferenceId=@Id AND MovementType=@Movement;";
                cmd.Parameters.AddWithValue("@At",utc);
                cmd.Parameters.AddWithValue("@Id",id);
                cmd.Parameters.AddWithValue("@Movement",movement);
                cmd.ExecuteNonQuery();
            }
            var localZone=TimeZoneInfo.Local;
            DateTime ToUtc(DateTime local)=>TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local,DateTimeKind.Unspecified),localZone);
            var inside=StockInId("Inside");
            var outside=StockInId("Outside");
            Stamp("StockIns","StockInId",inside,ToUtc(new DateTime(2026,3,15,23,30,0)),"IN");
            Stamp("StockIns","StockInId",outside,ToUtc(new DateTime(2026,3,16,0,30,0)),"IN");
            inventory.StockOut(boundary,1,"Damaged",new DateTime(2026,3,15),"Scratched",1);
            int stockOutId;
            using(var cmd=conn.CreateCommand())
            {
                cmd.CommandText="SELECT TOP 1 StockOutId FROM dbo.StockOuts WHERE ProductId=@Product ORDER BY StockOutId DESC;";
                cmd.Parameters.AddWithValue("@Product",boundary);
                stockOutId=(int)cmd.ExecuteScalar()!;
            }
            Stamp("StockOuts","StockOutId",stockOutId,ToUtc(new DateTime(2026,3,15,23,30,0)),"OUT");
            var dayRows=inventory.GetStockInReport(new DateTime(2026,3,15),new DateTime(2026,3,16));
            Equal(1,dayRows.Count(r=>r.ProductName=="Boundary Paint"),"Daily stock in keeps only the selected date");
            Equal(new DateTime(2027,6,1),dayRows.Single(r=>r.ProductName=="Boundary Paint").ExpirationDate!.Value.Date,"Stock in report shows the delivery expiration");
            Equal(2m,dayRows.Single(r=>r.ProductName=="Boundary Paint").Quantity,"Daily stock in quantity");
            var week=StockReportRange.Week(new DateTime(2026,3,15));
            var weekRows=inventory.GetStockInReport(week.Start,week.End);
            Equal(1,weekRows.Count(r=>r.ProductName=="Boundary Paint"),"Weekly stock in stays inside Monday through Sunday");
            var march=inventory.GetStockInReport(new DateTime(2026,3,1),new DateTime(2026,4,1));
            Equal(2,march.Count(r=>r.ProductName=="Boundary Paint"),"Monthly stock in includes both March records");
            Equal(0,inventory.GetStockInReport(new DateTime(2026,2,1),new DateTime(2026,3,1)).Count(r=>r.ProductName=="Boundary Paint"),"February excludes March stock in");
            Equal(2,inventory.GetStockInReport(new DateTime(2026,1,1),new DateTime(2027,1,1)).Count(r=>r.ProductName=="Boundary Paint"),"Yearly stock in includes the selected year");
            Equal(0,inventory.GetStockInReport(new DateTime(2025,1,1),new DateTime(2026,1,1)).Count(r=>r.ProductName=="Boundary Paint"),"Year 2025 excludes 2026 stock in");
            var outDay=inventory.GetStockOutReport(new DateTime(2026,3,15),new DateTime(2026,3,16));
            Equal(1,outDay.Count(r=>r.ProductName=="Boundary Paint"),"Daily stock out keeps only the selected date");
            Equal("Damaged",outDay.Single(r=>r.ProductName=="Boundary Paint").Reason,"Stock out report keeps the recorded reason");
            Equal(0,inventory.GetStockOutReport(new DateTime(2026,3,16),new DateTime(2026,3,17)).Count(r=>r.ProductName=="Boundary Paint"),"The next day excludes that stock out");
            Equal(true,inventory.GetHistory(boundary).Any(r=>r.MovementType=="IN") && inventory.GetHistory(boundary).Any(r=>r.MovementType=="OUT"),"Stock movement log still lists both movements");
            if(visual) VisualChecks.Run(product);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            using var drop=master.CreateCommand();
            drop.CommandText=$"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}];";
            drop.ExecuteNonQuery();
        }
    }
}
