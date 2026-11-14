using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Injaz.Application.Security;

namespace Injaz.Infrastructure.Security;

/// <summary>
/// PBKDF2-HMAC-SHA512 using only the BCL. Format: <c>pbkdf2-sha512$&lt;iterations&gt;$&lt;salt&gt;$&lt;hash&gt;</c> (base64).
/// Iterations follow the OWASP Password Storage Cheat Sheet; raising <see cref="Iterations"/> later makes existing
/// hashes report <see cref="PasswordVerification.SuccessRehashNeeded"/>, and they are upgraded on next login.
/// </summary>
internal sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    public const int Iterations = 210_000;

    private const string Scheme = "pbkdf2-sha512";
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(password, salt, Iterations);
        return string.Join('$', Scheme, Iterations.ToString(CultureInfo.InvariantCulture), Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public PasswordVerification Verify(string passwordHash, string password)
    {
        ArgumentNullException.ThrowIfNull(passwordHash);
        ArgumentNullException.ThrowIfNull(password);

        if (!TryParse(passwordHash, out var iterations, out var salt, out var expected))
        {
            return PasswordVerification.Failed;
        }

        var actual = Derive(password, salt, iterations, expected.Length);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
        {
            return PasswordVerification.Failed;
        }

        return iterations < Iterations ? PasswordVerification.SuccessRehashNeeded : PasswordVerification.Success;
    }

    private static byte[] Derive(string password, byte[] salt, int iterations, int length = HashSize) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, Algorithm, length);

    private static bool TryParse(string value, out int iterations, out byte[] salt, out byte[] hash)
    {
        iterations = 0;
        salt = hash = [];

        var parts = value.Split('$');
        if (parts.Length != 4
            || parts[0] != Scheme
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out iterations)
            || iterations <= 0)
        {
            return false;
        }

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            hash = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length > 0 && hash.Length > 0;
    }
}
