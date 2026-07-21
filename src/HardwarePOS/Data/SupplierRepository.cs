using HardwarePOS.Models;
using Microsoft.Data.SqlClient;

namespace HardwarePOS.Data;

public class SupplierRepository
{
    public List<Supplier> GetAll(string? search = null, bool activeOnly = false)
    {
        var list = new List<Supplier>();
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT SupplierId, CompanyName, ContactPerson, Phone, Email, Address, IsActive
            FROM dbo.Suppliers
            WHERE (@ActiveOnly = 0 OR IsActive = 1)
              AND (
                    @Search IS NULL OR @Search = N''
                    OR CompanyName LIKE N'%' + @Search + N'%'
                    OR ContactPerson LIKE N'%' + @Search + N'%'
                    OR Phone LIKE N'%' + @Search + N'%'
                    OR Email LIKE N'%' + @Search + N'%'
                  )
            ORDER BY CompanyName;
            """;
        cmd.Parameters.AddWithValue("@ActiveOnly", activeOnly ? 1 : 0);
        cmd.Parameters.AddWithValue("@Search", (object?)search ?? DBNull.Value);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(Map(reader));
        return list;
    }

    public int Insert(Supplier supplier)
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO dbo.Suppliers (CompanyName, ContactPerson, Phone, Email, Address, IsActive)
            VALUES (@Company, @Contact, @Phone, @Email, @Address, @Active);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;
        AddParams(cmd, supplier);
        return (int)cmd.ExecuteScalar()!;
    }

    public void Update(Supplier supplier)
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE dbo.Suppliers SET
                CompanyName = @Company,
                ContactPerson = @Contact,
                Phone = @Phone,
                Email = @Email,
                Address = @Address,
                IsActive = @Active
            WHERE SupplierId = @Id;
            """;
        AddParams(cmd, supplier);
        cmd.Parameters.AddWithValue("@Id", supplier.SupplierId);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int supplierId)
    {
        using var conn = DbConnectionFactory.Create();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            IF EXISTS (SELECT 1 FROM dbo.Products WHERE SupplierId = @Id)
                OR EXISTS (SELECT 1 FROM dbo.StockIns WHERE SupplierId = @Id)
                UPDATE dbo.Suppliers SET IsActive = 0 WHERE SupplierId = @Id;
            ELSE
                DELETE FROM dbo.Suppliers WHERE SupplierId = @Id;
            """;
        cmd.Parameters.AddWithValue("@Id", supplierId);
        cmd.ExecuteNonQuery();
    }

    private static void AddParams(SqlCommand cmd, Supplier supplier)
    {
        cmd.Parameters.AddWithValue("@Company", supplier.CompanyName);
        cmd.Parameters.AddWithValue("@Contact", (object?)supplier.ContactPerson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Phone", (object?)supplier.Phone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Email", (object?)supplier.Email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Address", (object?)supplier.Address ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Active", supplier.IsActive);
    }

    private static Supplier Map(SqlDataReader reader) => new()
    {
        SupplierId = reader.GetInt32(0),
        CompanyName = reader.GetString(1),
        ContactPerson = reader.IsDBNull(2) ? null : reader.GetString(2),
        Phone = reader.IsDBNull(3) ? null : reader.GetString(3),
        Email = reader.IsDBNull(4) ? null : reader.GetString(4),
        Address = reader.IsDBNull(5) ? null : reader.GetString(5),
        IsActive = reader.GetBoolean(6)
    };
}
