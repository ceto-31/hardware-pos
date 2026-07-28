using System.Security.Cryptography;
using System.Text;

namespace HardwarePOS.Helpers;

public static class SecurityAnswerHasher
{
    public static string Normalize(string? answer) =>
        (answer ?? string.Empty).Trim().ToLowerInvariant();

    public static byte[] Hash(string? answer)
    {
        var normalized = Normalize(answer);
        return SHA256.HashData(Encoding.Unicode.GetBytes(normalized));
    }

    public static bool Matches(string? answer, byte[]? storedHash)
    {
        if (storedHash is null || storedHash.Length == 0) return false;
        var computed = Hash(answer);
        return CryptographicOperations.FixedTimeEquals(computed, storedHash);
    }
}
