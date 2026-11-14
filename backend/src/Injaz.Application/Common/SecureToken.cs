using System.Security.Cryptography;
using System.Text;

namespace Injaz.Application.Common;

/// <summary>
/// Opaque bearer secrets (refresh tokens, invitation links): 256 random bits, base64url for the client,
/// SHA-256 hex for the database. A fast hash is appropriate because the input is high-entropy, not a password.
/// </summary>
internal static class SecureToken
{
    public static (string Secret, string Hash) Generate()
    {
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (secret, Hash(secret));
    }

    public static string Hash(string secret) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
}
