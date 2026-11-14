namespace Injaz.Application.Common;

/// <summary>
/// A PATCH field: tells "omitted" (leave unchanged) apart from "sent", including "sent as null" (clear it).
/// The API's JSON converter produces <see cref="HasValue"/> = true only for properties present in the body.
/// </summary>
public readonly record struct Optional<T>
{
    public Optional(T value)
    {
        HasValue = true;
        Value = value;
    }

    public bool HasValue { get; }

    public T Value { get; }

    public static Optional<T> Unset => default;

#pragma warning disable CA2225 // A named alternative (new Optional<T>(value)) already exists.
    public static implicit operator Optional<T>(T value) => new(value);
#pragma warning restore CA2225

    public override string ToString() => HasValue ? $"{Value}" : "(unset)";
}
