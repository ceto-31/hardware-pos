using HardwarePOS.Models;

namespace HardwarePOS.Helpers;

public static class SessionManager
{
    public static UserAccount? CurrentUser { get; private set; }

    public static bool IsLoggedIn => CurrentUser is not null;

    public static bool IsAdmin =>
        string.Equals(CurrentUser?.RoleName, "Admin", StringComparison.OrdinalIgnoreCase);

    public static void SignIn(UserAccount user) => CurrentUser = user;

    public static void SignOut() => CurrentUser = null;
}
