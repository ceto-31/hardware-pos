using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace HardwarePOS.Data;

public static class InventorySchemaUpgrade
{
    public static void Ensure()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("InventoryReturns.sql")!;
        using var reader = new StreamReader(stream);
        var sql = reader.ReadToEnd();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        try
        {
            foreach (var batch in Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                using var cmd = conn.CreateCommand();
                cmd.CommandText = batch;
                cmd.CommandTimeout = 120;
                cmd.ExecuteNonQuery();
            }
        }
        catch
        {
            using var rollback = conn.CreateCommand();
            rollback.CommandText = "IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;";
            rollback.ExecuteNonQuery();
            throw;
        }
    }
}
