using HardwarePOS.Helpers;
using Microsoft.Data.SqlClient;

namespace HardwarePOS.Data;

public class ActivityRepository
{
    public void Log(string activityType, string description, int? createdBy = null)
    {
        createdBy ??= SessionManager.CurrentUser?.UserId;
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO dbo.ActivityLog (ActivityType, Description, CreatedBy, CreatedAt)
            VALUES (@Type, @Desc, @By, SYSDATETIME());
            """;
        cmd.Parameters.AddWithValue("@Type", activityType);
        cmd.Parameters.AddWithValue("@Desc", description);
        cmd.Parameters.AddWithValue("@By", (object?)createdBy ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public List<(string Type, string Description, string? User, DateTime At)> GetRecent(int take = 20)
    {
        var list = new List<(string, string, string?, DateTime)>();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT TOP (@Take) a.ActivityType, a.Description, u.FullName, a.CreatedAt
            FROM dbo.ActivityLog a
            LEFT JOIN dbo.Users u ON u.UserId = a.CreatedBy
            ORDER BY a.CreatedAt DESC, a.ActivityId DESC;
            """;
        cmd.Parameters.AddWithValue("@Take", take);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add((
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetDateTime(3)));
        }
        return list;
    }
}
