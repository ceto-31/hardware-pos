using HardwarePOS.Helpers;
using HardwarePOS.Models;
using Microsoft.Data.SqlClient;

namespace HardwarePOS.Data;

public class UserRepository
{
    public UserAccount? Authenticate(string username, string password)
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT u.UserId, u.Username, u.FullName, u.RoleId, r.RoleName, u.IsActive,
                   u.PasswordHash, u.PasswordSalt
            FROM dbo.Users u
            INNER JOIN dbo.Roles r ON r.RoleId = u.RoleId
            WHERE u.Username = @Username;
            """;
        cmd.Parameters.AddWithValue("@Username", username);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return null;

        if (!reader.GetBoolean(5))
            return null;

        var hash = (byte[])reader["PasswordHash"];
        var salt = (byte[])reader["PasswordSalt"];
        if (!PasswordHasher.Verify(password, hash, salt))
            return null;

        return new UserAccount
        {
            UserId = reader.GetInt32(0),
            Username = reader.GetString(1),
            FullName = reader.GetString(2),
            RoleId = reader.GetInt32(3),
            RoleName = reader.GetString(4),
            IsActive = true
        };
    }
}
