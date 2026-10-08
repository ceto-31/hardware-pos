using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HardwarePOS.Models;
using HardwarePOS.Services;
using HardwarePOS.ViewModels;
using HardwarePOS.Views;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

internal static class VisualChecks
{
    public static void Run(int product)
    {
        var output=Path.Combine(AppContext.BaseDirectory,"verification");Directory.CreateDirectory(output);
        var app=new HardwarePOS.App {ShutdownMode=ShutdownMode.OnExplicitShutdown};app.InitializeComponent();
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        LiveCharts.Configure(c=>c.AddSkiaSharp());
        LiveChartsSkiaSharp.DefaultSKTypeface=SkiaSharp.SKTypeface.FromFamilyName("Segoe UI");
        var dashboard=new DashboardViewModel();dashboard.Load();
        Render(new DashboardView {DataContext=dashboard},Path.Combine(output,"dashboard-admin.png"),1200,820,true);
        Render(new DashboardView {DataContext=dashboard},Path.Combine(output,"dashboard-admin-bottom.png"),1200,820,true,true);
        Render(new DashboardView {DataContext=dashboard},Path.Combine(output,"dashboard-narrow.png"),860,820,true);
        dashboard.IsAdmin=false;
        dashboard.WelcomeMessage="Store Cashier";dashboard.UserDisplay="Cashier";
        Render(new DashboardView {DataContext=dashboard},Path.Combine(output,"dashboard-cashier.png"),1200,820,true);
        var emptyDashboard=new DashboardViewModel {IsAdmin=true,WelcomeMessage="System Administrator",UserDisplay="Admin"};
        Render(new DashboardView {DataContext=emptyDashboard},Path.Combine(output,"dashboard-empty.png"),1200,820,true);
        Render(new DashboardView {DataContext=emptyDashboard},Path.Combine(output,"dashboard-empty-bottom.png"),1200,820,true,true);
        var archives=new ArchivesViewModel();archives.Load();
        Render(new ArchivesView {DataContext=archives},Path.Combine(output,"archives-empty.png"));
        archives.ArchivedProducts=new(Enumerable.Range(1,13).Select(i=>new Product {ProductId=i,ProductCode=$"ARCH-{i:000}",ProductName=$"Archived paint {i:00}",Brand="Sample brand",Color="White",UnitOfMeasure="Liter",StockQty=2.5m,IsArchived=true}));
        Render(new ArchivesView {DataContext=archives},Path.Combine(output,"archives-products.png"));
        var products=new ProductsViewModel();products.Load();
        Render(new ProductsView {DataContext=products},Path.Combine(output,"products-paged.png"));
        var blankInventory=new InventoryViewModel();blankInventory.Load();blankInventory.SelectedTabIndex=2;
        Render(new InventoryView {DataContext=blankInventory},Path.Combine(output,"stock-out-placeholders.png"));
        var inventory=new InventoryViewModel(); inventory.Load();inventory.InCategoryId=1;inventory.InProductId=product;inventory.InSupplierId=2;inventory.InQuantityText="15";
        foreach(var (tab,name) in new[]{(1,"stock-in"),(2,"stock-out"),(3,"inventory-history"),(4,"supplier-deliveries"),(5,"bo")})
        {
            inventory.SelectedTabIndex=tab;inventory.OutCategoryId=1;inventory.OutProductId=product;
            Render(new InventoryView {DataContext=inventory},Path.Combine(output,name+".png"));
        }
        var pos=new PosViewModel();pos.Load();pos.SelectedProduct=pos.ProductList.First(p=>p.ProductId==product);pos.AddSelectedCommand.Execute(null);pos.QuantityInput="0.5";pos.ApplyQuantityCommand.Execute(null);
        Render(new PosView {DataContext=pos},Path.Combine(output,"pos.png"));
        pos.SelectedTab=1;
        Render(new PosView {DataContext=pos},Path.Combine(output,"pos-history.png"));
        var reports=new ReportsViewModel();reports.Load();Wait(reports);
        foreach(var view in new[]{ReportView.Today,ReportView.Daily,ReportView.Weekly,ReportView.Monthly,ReportView.Yearly,ReportView.StockIn,ReportView.StockOut,ReportView.Transactions})
        {
            reports.SelectedView=view;Wait(reports);
            if(reports.HasError) throw new Exception(reports.ErrorMessage);
            Render(new ReportsView {DataContext=reports},Path.Combine(output,$"reports-{view}.png"));
            var document=(ReportDocument)typeof(ReportsViewModel).GetMethod("BuildDocument",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(reports,null)!;
            Export(document,Path.Combine(output,$"report-{view}.pdf"));
        }
        reports.SelectedView=ReportView.Daily;reports.SingleDayDetail=true;reports.DailyFrom=DateTime.Today;Wait(reports);
        var detail=(ReportDocument)typeof(ReportsViewModel).GetMethod("BuildDocument",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(reports,null)!;
        Export(detail,Path.Combine(output,"report-single-day.pdf"));
        Render(new ReportsView {DataContext=reports},Path.Combine(output,"reports-single-day.png"));
        var admin=HardwarePOS.Helpers.SessionManager.CurrentUser!;
        foreach(var role in new[]{"Admin","Cashier"})
        {
            HardwarePOS.Helpers.SessionManager.SignIn(new UserAccount {UserId=role=="Admin"?1:2,RoleName=role,FullName="Report check"});
            // Today must ignore stale or missing daily-range filters for either role.
            reports.DailyFrom=null;reports.DailyTo=null;reports.SelectedView=ReportView.Today;Wait(reports);
            if(reports.HasError || reports.HasValidation || !reports.ShowDailyDetails || reports.ReportTitle!="Today's sales")
                throw new Exception($"Today's sales did not load for {role}");
            if(reports.VisibleSalesRows.Count!=1 || reports.VisibleSalesRows[0].Start.Date!=DateTime.Today || reports.DailyDetails.Any(d=>d.EventDate.Date!=DateTime.Today))
                throw new Exception($"Today's sales included another date for {role}");
            var expected=new HardwarePOS.Data.SalesReportRepository().GetDailyTotals(DateTime.Today,DateTime.Today).Sum(d=>d.Amount);
            if(reports.DailyDetails.Sum(d=>d.Amount)!=expected || reports.TopProducts.Sum(p=>p.NetSales)!=expected)
                throw new Exception($"Today's sales amounts did not reconcile for {role}");
            var todayDocument=(ReportDocument)typeof(ReportsViewModel).GetMethod("BuildDocument",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(reports,null)!;
            if(!todayDocument.Title.StartsWith("Today's sales")) throw new Exception("Today's preview has the wrong title");
            Render(new ReportsView {DataContext=reports},Path.Combine(output,$"reports-today-{role}.png"));
            Console.WriteLine($"PASS Today's sales date filtering, totals, and preview for {role}");
            reports.SelectedView=ReportView.Monthly;Wait(reports);
        }
        HardwarePOS.Helpers.SessionManager.SignIn(admin);
        Console.WriteLine("Visual verification files: "+output);
        app.Shutdown();
    }
    private static void Wait(ReportsViewModel vm)
    {
        var limit=DateTime.UtcNow.AddSeconds(30);
        while(vm.IsLoading && DateTime.UtcNow<limit) {Pump();Thread.Sleep(10);}
        if(vm.IsLoading) throw new TimeoutException("Report did not finish loading");
    }
    private static void Pump()
    {
        var frame=new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>frame.Continue=false));
        Dispatcher.PushFrame(frame);
    }
    private static void Render(FrameworkElement content,string path,double width=1200,double height=820,bool live=false,bool bottom=false)
    {
        // Offscreen rendering; no interactive windows or changes to the running POS.
        var host=new Border {Background=Brushes.White,Child=content,Width=width,Height=height};
        Window? window=null;
        if(live)
        {
            window=new Window {Content=host,Width=width,Height=height,Left=-20000,Top=-20000,ShowActivated=false,ShowInTaskbar=false,WindowStyle=WindowStyle.None};
            window.Show();
            for(var i=0;i<12;i++){Pump();Thread.Sleep(50);}
        }
        host.Measure(new Size(width,height));host.Arrange(new Rect(0,0,width,height));host.UpdateLayout();Pump();
        if(bottom && content is UserControl {Content:ScrollViewer scroll})
        {
            scroll.ScrollToEnd();Pump();host.UpdateLayout();
        }
        var bitmap=new RenderTargetBitmap((int)width,(int)height,96,96,PixelFormats.Pbgra32);bitmap.Render(host);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);png.Save(file);
        window?.Close();
    }
    private static void Export(ReportDocument document,string path)
    {
        ReportPdfExporter.Export(path,document);
        var file=StorageFile.GetFileFromPathAsync(path).AsTask().GetAwaiter().GetResult();
        var pdf=PdfDocument.LoadFromFileAsync(file).AsTask().GetAwaiter().GetResult();
        if(pdf.PageCount==0) throw new Exception("Empty PDF");
        for(uint i=0;i<pdf.PageCount;i++)
        {
            using var page=pdf.GetPage(i);using var stream=new InMemoryRandomAccessStream();
            page.RenderToStreamAsync(stream,new PdfPageRenderOptions {DestinationWidth=1000}).AsTask().GetAwaiter().GetResult();
            stream.Seek(0);using var reader=new DataReader(stream.GetInputStreamAt(0));reader.LoadAsync((uint)stream.Size).AsTask().GetAwaiter().GetResult();
            var bytes=new byte[(int)stream.Size];reader.ReadBytes(bytes);File.WriteAllBytes(path+$"-page-{i+1}.png",bytes);
        }
    }
}
