namespace HardwarePOS.Models;

public class UserAccount
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    public bool IsProtected =>
        string.Equals(Username, "admin", StringComparison.OrdinalIgnoreCase);

    public string StatusLabel => IsActive ? "Active" : "Inactive";
}
