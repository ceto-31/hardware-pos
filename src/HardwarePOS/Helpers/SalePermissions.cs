namespace HardwarePOS.Helpers;

public static class SalePermissions
{
    public static bool CanVoid(int cashierId) => SessionManager.CurrentUser is { } user &&
        (SessionManager.IsAdmin ||
         (string.Equals(user.RoleName, "Cashier", StringComparison.OrdinalIgnoreCase) && user.UserId == cashierId));
}
