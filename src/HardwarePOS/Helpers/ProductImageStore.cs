using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HardwarePOS.Helpers;

public static class ProductImageStore
{
    public static string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "4KVHardware",
        "ProductImages");

    public static string? GetFullPath(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name)) return null;
        var path = Path.Combine(Folder, name);
        return File.Exists(path) ? path : null;
    }

    public static ImageSource? Load(string? fileName)
    {
        var path = GetFullPath(fileName) ?? (File.Exists(fileName) ? fileName : null);
        if (path is null) return null;
        return LoadFromFile(path);
    }

    public static ImageSource? LoadFromFile(string path)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    public static string Save(int productId, string sourcePath)
    {
        Directory.CreateDirectory(Folder);
        var ext = Path.GetExtension(sourcePath);
        if (ext is not (".jpg" or ".jpeg" or ".png") || string.IsNullOrWhiteSpace(ext))
            ext = ".jpg";
        ext = ext.ToLowerInvariant();

        var fileName = $"{productId}{ext}";
        var dest = Path.Combine(Folder, fileName);

        foreach (var old in Directory.GetFiles(Folder, $"{productId}.*"))
        {
            if (!old.Equals(dest, StringComparison.OrdinalIgnoreCase))
                File.Delete(old);
        }

        File.Copy(sourcePath, dest, overwrite: true);
        return fileName;
    }

    public static void Delete(int productId)
    {
        if (!Directory.Exists(Folder)) return;
        foreach (var old in Directory.GetFiles(Folder, $"{productId}.*"))
            File.Delete(old);
    }
}
