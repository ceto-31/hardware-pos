using System.IO;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace HardwarePOS.Data;

public static class DbConnectionFactory
{
    private static readonly Lazy<string> ConnectionString = new(LoadConnectionString);

    public static SqlConnection Create() => new(ConnectionString.Value);

    private static string LoadConnectionString()
    {
        var basePath = AppContext.BaseDirectory;
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var cs = config.GetConnectionString("HardwarePOS");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("Connection string 'HardwarePOS' is missing in appsettings.json.");

        return cs;
    }
}
