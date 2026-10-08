using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HardwarePOS.Helpers;
using HardwarePOS.Models;
using Microsoft.Win32;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace HardwarePOS.Services;

public static class ReportPreview
{
    public static async Task ShowAsync(ReportDocument report,string fileName)
    {
        var path=Path.Combine(Path.GetTempPath(),$"4kv-preview-{Guid.NewGuid():N}.pdf");
        try
        {
            ReportPdfExporter.Export(path,report);
            var file=await StorageFile.GetFileFromPathAsync(path);
            var pdf=await PdfDocument.LoadFromFileAsync(file);
            var root=new DockPanel {Background=new SolidColorBrush(Color.FromRgb(226,232,240))};
            var toolbar=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(12)};
            DockPanel.SetDock(toolbar,Dock.Top);root.Children.Add(toolbar);
            var status=new TextBlock {Text=$"{report.Title} · {pdf.PageCount} pages",Margin=new Thickness(8),VerticalAlignment=VerticalAlignment.Center}; toolbar.Children.Add(status);
            var panel=new StackPanel {Margin=new Thickness(16)};
            root.Children.Add(new ScrollViewer {Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto});
            var window=new Window {Title="Report preview",Width=960,Height=800,Content=root,Owner=Application.Current.MainWindow,WindowStartupLocation=WindowStartupLocation.CenterOwner};
            var fixedDocument=new FixedDocument();
            for(uint i=0;i<pdf.PageCount;i++)
            {
                using var page=pdf.GetPage(i);
                using var stream=new InMemoryRandomAccessStream();
                await page.RenderToStreamAsync(stream,new PdfPageRenderOptions {DestinationWidth=1240});
                stream.Seek(0);
                using var reader=new DataReader(stream.GetInputStreamAt(0));
                await reader.LoadAsync((uint)stream.Size);
                var bytes=new byte[(int)stream.Size];reader.ReadBytes(bytes);
                var bitmap=new BitmapImage();
                using(var memory=new MemoryStream(bytes)) {bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=memory;bitmap.EndInit();bitmap.Freeze();}
                panel.Children.Add(new Border {Margin=new Thickness(0,0,0,16),Background=Brushes.White,Child=new Image {Source=bitmap,Width=794,Stretch=Stretch.Uniform}});
                var fixedPage=new FixedPage {Width=page.Size.Width,Height=page.Size.Height};
                fixedPage.Children.Add(new Image {Source=bitmap,Width=page.Size.Width,Height=page.Size.Height});
                var content=new PageContent {Child=fixedPage};fixedDocument.Pages.Add(content);
            }
            if(SessionManager.IsAdmin)
            {
                var print=new Button {Content="Print",Padding=new Thickness(16,8,16,8),Margin=new Thickness(8,0,0,0)};
                print.Click+=(_,_)=>
                {
                    if(!SessionManager.IsAdmin) return;
                    try {var dialog=new PrintDialog();if(dialog.ShowDialog()==true) dialog.PrintDocument(fixedDocument.DocumentPaginator,report.Title);}
                    catch(Exception ex){DialogService.ShowError(ex.Message,"Print report");}
                };
                var save=new Button {Content="Save PDF",Padding=new Thickness(16,8,16,8),Margin=new Thickness(8,0,0,0)};
                save.Click+=(_,_)=>
                {
                    if(!SessionManager.IsAdmin) return;
                    var dialog=new SaveFileDialog {Filter="PDF files|*.pdf",FileName=fileName};
                    if(dialog.ShowDialog()!=true) return;
                    try{File.Copy(path,dialog.FileName,true);DialogService.ShowInfo("PDF saved.","Reports");}
                    catch(Exception ex){DialogService.ShowError(ex.Message,"Save PDF");}
                };
                toolbar.Children.Add(print);toolbar.Children.Add(save);
            }
            window.ShowDialog();
        }
        finally { if(File.Exists(path)) File.Delete(path); }
    }
}
