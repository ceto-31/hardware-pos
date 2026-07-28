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
        if (!reader.Read() || !reader.GetBoolean(5)) return null;
        var hash = (byte[])reader["PasswordHash"];
        var salt = (byte[])reader["PasswordSalt"];
        if (!PasswordHasher.Verify(password, hash, salt)) return null;
        return MapUser(reader);
    }

    public UserAccount? GetByUsername(string username)
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT u.UserId, u.Username, u.FullName, u.RoleId, r.RoleName, u.IsActive
            FROM dbo.Users u
            INNER JOIN dbo.Roles r ON r.RoleId = u.RoleId
            WHERE u.Username = @Username;
            """;
        cmd.Parameters.AddWithValue("@Username", username);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapUser(reader) : null;
    }

    public (bool ColorOk, bool NumberOk, bool HobbyOk) VerifySecurityAnswers(
        string username, string color, string number, string hobby)
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT SecurityColorHash, SecurityNumberHash, SecurityHobbyHash
            FROM dbo.Users WHERE Username = @Username;
            """;
        cmd.Parameters.AddWithValue("@Username", username);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return (false, false, false);

        var colorHash = reader.IsDBNull(0) ? null : (byte[])reader[0];
        var numberHash = reader.IsDBNull(1) ? null : (byte[])reader[1];
        var hobbyHash = reader.IsDBNull(2) ? null : (byte[])reader[2];
        return (
            SecurityAnswerHasher.Matches(color, colorHash),
            SecurityAnswerHasher.Matches(number, numberHash),
            SecurityAnswerHasher.Matches(hobby, hobbyHash));
    }

    public void ResetPassword(string username, string newPassword)
    {
        var (hash, salt) = PasswordHasher.HashPassword(newPassword);
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE dbo.Users SET PasswordHash = @Hash, PasswordSalt = @Salt
            WHERE Username = @Username;
            """;
        cmd.Parameters.AddWithValue("@Hash", hash);
        cmd.Parameters.AddWithValue("@Salt", salt);
        cmd.Parameters.AddWithValue("@Username", username);
        cmd.ExecuteNonQuery();
    }

    public List<UserAccount> GetAll(string? search = null)
    {
        var list = new List<UserAccount>();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT u.UserId, u.Username, u.FullName, u.RoleId, r.RoleName, u.IsActive
            FROM dbo.Users u
            INNER JOIN dbo.Roles r ON r.RoleId = u.RoleId
            WHERE (@Search IS NULL OR @Search = N''
                   OR u.Username LIKE N'%' + @Search + N'%'
                   OR u.FullName LIKE N'%' + @Search + N'%')
            ORDER BY u.Username;
            """;
        cmd.Parameters.AddWithValue("@Search", (object?)search ?? DBNull.Value);
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) list.Add(MapUser(reader));
        return list;
    }

    public List<(int RoleId, string RoleName)> GetRoles()
    {
        var list = new List<(int, string)>();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT RoleId, RoleName FROM dbo.Roles ORDER BY RoleName;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) list.Add((reader.GetInt32(0), reader.GetString(1)));
        return list;
    }

    public int Insert(string username, string fullName, int roleId, string password,
        string color, string number, string hobby)
    {
        var (hash, salt) = PasswordHasher.HashPassword(password);
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO dbo.Users
                (Username, PasswordHash, PasswordSalt, FullName, RoleId, IsActive,
                 SecurityColorHash, SecurityNumberHash, SecurityHobbyHash)
            VALUES
                (@User, @Hash, @Salt, @Full, @Role, 1, @Color, @Number, @Hobby);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;
        cmd.Parameters.AddWithValue("@User", username.Trim());
        cmd.Parameters.AddWithValue("@Hash", hash);
        cmd.Parameters.AddWithValue("@Salt", salt);
        cmd.Parameters.AddWithValue("@Full", fullName.Trim());
        cmd.Parameters.AddWithValue("@Role", roleId);
        cmd.Parameters.AddWithValue("@Color", SecurityAnswerHasher.Hash(color));
        cmd.Parameters.AddWithValue("@Number", SecurityAnswerHasher.Hash(number));
        cmd.Parameters.AddWithValue("@Hobby", SecurityAnswerHasher.Hash(hobby));
        return (int)cmd.ExecuteScalar()!;
    }

    public void Update(int userId, string fullName, int roleId, bool isActive,
        string? newPassword, string? color, string? number, string? hobby)
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE dbo.Users SET
                FullName = @Full,
                RoleId = @Role,
                IsActive = @Active
            WHERE UserId = @Id;
            """;
        cmd.Parameters.AddWithValue("@Full", fullName.Trim());
        cmd.Parameters.AddWithValue("@Role", roleId);
        cmd.Parameters.AddWithValue("@Active", isActive);
        cmd.Parameters.AddWithValue("@Id", userId);
        cmd.ExecuteNonQuery();

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            var (hash, salt) = PasswordHasher.HashPassword(newPassword);
            using var p = conn.CreateCommand();
            p.CommandText = "UPDATE dbo.Users SET PasswordHash=@H, PasswordSalt=@S WHERE UserId=@Id;";
            p.Parameters.AddWithValue("@H", hash);
            p.Parameters.AddWithValue("@S", salt);
            p.Parameters.AddWithValue("@Id", userId);
            p.ExecuteNonQuery();
        }

        if (!string.IsNullOrWhiteSpace(color) || !string.IsNullOrWhiteSpace(number) || !string.IsNullOrWhiteSpace(hobby))
        {
            using var s = conn.CreateCommand();
            s.CommandText = """
                UPDATE dbo.Users SET
                    SecurityColorHash = COALESCE(@Color, SecurityColorHash),
                    SecurityNumberHash = COALESCE(@Number, SecurityNumberHash),
                    SecurityHobbyHash = COALESCE(@Hobby, SecurityHobbyHash)
                WHERE UserId = @Id;
                """;
            s.Parameters.AddWithValue("@Color", string.IsNullOrWhiteSpace(color) ? DBNull.Value : SecurityAnswerHasher.Hash(color));
            s.Parameters.AddWithValue("@Number", string.IsNullOrWhiteSpace(number) ? DBNull.Value : SecurityAnswerHasher.Hash(number));
            s.Parameters.AddWithValue("@Hobby", string.IsNullOrWhiteSpace(hobby) ? DBNull.Value : SecurityAnswerHasher.Hash(hobby));
            s.Parameters.AddWithValue("@Id", userId);
            s.ExecuteNonQuery();
        }
    }

    public void Deactivate(int userId)
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DECLARE @AdminRole INT = (SELECT RoleId FROM dbo.Roles WHERE RoleName = N'Admin');
            DECLARE @IsAdmin BIT = (SELECT CASE WHEN RoleId = @AdminRole THEN 1 ELSE 0 END FROM dbo.Users WHERE UserId = @Id);
            IF @IsAdmin = 1 AND (SELECT COUNT(*) FROM dbo.Users WHERE RoleId = @AdminRole AND IsActive = 1 AND UserId <> @Id) = 0
                THROW 50012, 'Cannot deactivate the last active Admin.', 1;
            UPDATE dbo.Users SET IsActive = 0 WHERE UserId = @Id;
            """;
        cmd.Parameters.AddWithValue("@Id", userId);
        cmd.ExecuteNonQuery();
    }

    private static UserAccount MapUser(SqlDataReader reader) => new()
    {
        UserId = reader.GetInt32(0),
        Username = reader.GetString(1),
        FullName = reader.GetString(2),
        RoleId = reader.GetInt32(3),
        RoleName = reader.GetString(4),
        IsActive = reader.GetBoolean(5)
    };
}
