namespace Injaz.Domain.Common;

/// <summary>
/// Base type for aggregate roots and entities. Identifiers are UUIDv7: generated in-process (no DB round-trip),
/// time-ordered, and therefore index-friendly.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected init; } = Guid.CreateVersion7();
}
