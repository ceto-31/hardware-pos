using HardwarePOS.Models;
using Microsoft.Data.SqlClient;

namespace HardwarePOS.Data;

public class CategoryRepository
{
    public List<Category> GetAll(string? search = null)
    {
        var list = new List<Category>();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT CategoryId, CategoryName FROM dbo.Categories
            WHERE (@Search IS NULL OR @Search = N'' OR CategoryName LIKE N'%' + @Search + N'%')
            ORDER BY CategoryName;
            """;
        cmd.Parameters.AddWithValue("@Search", (object?)search ?? DBNull.Value);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(new Category { CategoryId = reader.GetInt32(0), CategoryName = reader.GetString(1) });
        return list;
    }

    public int Insert(string name)
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO dbo.Categories (CategoryName) VALUES (@Name); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        cmd.Parameters.AddWithValue("@Name", name.Trim());
        return (int)cmd.ExecuteScalar()!;
    }

    public void Update(int id, string name)
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE dbo.Categories SET CategoryName = @Name WHERE CategoryId = @Id;";
        cmd.Parameters.AddWithValue("@Name", name.Trim());
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            IF EXISTS (SELECT 1 FROM dbo.Products WHERE CategoryId = @Id)
                THROW 50010, 'Cannot delete category while products still use it.', 1;
            DELETE FROM dbo.Categories WHERE CategoryId = @Id;
            """;
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.ExecuteNonQuery();
    }
}

public class UnitRepository
{
    public List<UnitOfMeasureItem> GetAll(string? search = null, bool activeOnly = false)
    {
        var list = new List<UnitOfMeasureItem>();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT UnitId, UnitName, IsActive FROM dbo.Units
            WHERE (@ActiveOnly = 0 OR IsActive = 1)
              AND (@Search IS NULL OR @Search = N'' OR UnitName LIKE N'%' + @Search + N'%')
            ORDER BY UnitName;
            """;
        cmd.Parameters.AddWithValue("@ActiveOnly", activeOnly ? 1 : 0);
        cmd.Parameters.AddWithValue("@Search", (object?)search ?? DBNull.Value);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(new UnitOfMeasureItem
            {
                UnitId = reader.GetInt32(0),
                UnitName = reader.GetString(1),
                IsActive = reader.GetBoolean(2)
            });
        return list;
    }

    public int Insert(string name)
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO dbo.Units (UnitName, IsActive) VALUES (@Name, 1); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        cmd.Parameters.AddWithValue("@Name", name.Trim());
        return (int)cmd.ExecuteScalar()!;
    }

    public void Update(int id, string name, bool isActive)
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE dbo.Units SET UnitName = @Name, IsActive = @Active WHERE UnitId = @Id;";
        cmd.Parameters.AddWithValue("@Name", name.Trim());
        cmd.Parameters.AddWithValue("@Active", isActive);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            IF EXISTS (SELECT 1 FROM dbo.Products WHERE UnitId = @Id)
                THROW 50011, 'Cannot delete unit while products still use it.', 1;
            DELETE FROM dbo.Units WHERE UnitId = @Id;
            """;
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.ExecuteNonQuery();
    }
}
