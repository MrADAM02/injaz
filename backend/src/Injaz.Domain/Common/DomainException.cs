namespace Injaz.Domain.Common;

/// <summary>
/// Thrown when a business rule or invariant would be violated (e.g. removing a workspace's only owner).
/// <see cref="Code"/> is a stable, machine-readable key that clients map to a localized message.
/// </summary>
public class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
