using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Injaz.Infrastructure.Security;

/// <summary>Bound from the <c>Jwt</c> configuration section. <see cref="SigningKey"/> must come from a secret store in production.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 needs a key of at least 256 bits.</summary>
    public const int MinimumKeyBytes = 32;

    public string Issuer { get; set; } = "injaz";

    public string Audience { get; set; } = "injaz";

    public string SigningKey { get; set; } = string.Empty;

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    internal SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));

    internal bool IsValid() =>
        !string.IsNullOrWhiteSpace(Issuer)
        && !string.IsNullOrWhiteSpace(Audience)
        && Encoding.UTF8.GetByteCount(SigningKey) >= MinimumKeyBytes
        && AccessTokenLifetime > TimeSpan.Zero;
}
