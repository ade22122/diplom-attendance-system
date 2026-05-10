using System.Security.Cryptography;
using System.Text;

namespace DiplomDesktop;

public static class DjangoPasswordHasher
{
    private const int DefaultIterations = 1_000_000;

    public static string Hash(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(12);
        var salt = Convert.ToBase64String(saltBytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "");
        if (salt.Length > 12)
        {
            salt = salt[..12];
        }

        var digest = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            Encoding.UTF8.GetBytes(salt),
            DefaultIterations,
            HashAlgorithmName.SHA256,
            32);
        return $"pbkdf2_sha256${DefaultIterations}${salt}${Convert.ToBase64String(digest)}";
    }

    public static bool Verify(string password, string encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return false;
        }

        var parts = encoded.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2_sha256")
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        try
        {
            var salt = Encoding.UTF8.GetBytes(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expected.Length);

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
