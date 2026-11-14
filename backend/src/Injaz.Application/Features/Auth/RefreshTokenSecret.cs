using System.Security.Cryptography;
using System.Text;

namespace Injaz.Application.Features.Auth;

/// <summary>Opaque refresh-token secrets: 256 random bits for the client, SHA-256 hex for the database.</summary>
internal static class RefreshTokenSecret
{
    public static (string Secret, string Hash) Generate()
    {
        var secret = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return (secret, Hash(secret));
    }

    /// <summary>A fast hash is appropriate here: the input is high-entropy random data, not a human password.</summary>
    public static string Hash(string secret) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
