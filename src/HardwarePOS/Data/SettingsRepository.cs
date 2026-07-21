using HardwarePOS.Models;
using Microsoft.Data.SqlClient;

namespace HardwarePOS.Data;

public class SettingsRepository
{
    public string GetValue(string key, string defaultValue = "")
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT SettingValue FROM dbo.AppSettings WHERE SettingKey = @Key;";
        cmd.Parameters.AddWithValue("@Key", key);
        var result = cmd.ExecuteScalar();
        return result is string s ? s : defaultValue;
    }

    public decimal GetTaxRate()
    {
        var raw = GetValue("TaxRate", "0.12");
        return decimal.TryParse(raw, out var rate) ? rate : 0.12m;
    }

    public string GetStoreName() => GetValue("StoreName", "HARDWARE");
}
