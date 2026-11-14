using System.Net.Mail;

namespace Injaz.Application.Validation;

/// <summary>Built-in rules. Add new ones here as extension methods so every validator speaks the same codes.</summary>
public static class ValidationRules
{
    // ---------- strings ----------

    public static FieldRules<string?> NotEmpty(this FieldRules<string?> rules) =>
        rules.Must(v => !string.IsNullOrWhiteSpace(v), ValidationCodes.Required);

    /// <summary>Null is allowed (combine with <see cref="NotEmpty(FieldRules{string})"/> when required).</summary>
    public static FieldRules<string?> MaxLength(this FieldRules<string?> rules, int max) =>
        rules.Must(v => v is null || v.Length <= max, ValidationCodes.MaxLength, Params("max", max));

    /// <summary>Null is allowed (combine with <see cref="NotEmpty(FieldRules{string})"/> when required).</summary>
    public static FieldRules<string?> MinLength(this FieldRules<string?> rules, int min) =>
        rules.Must(v => v is null || v.Length >= min, ValidationCodes.MinLength, Params("min", min));

    /// <summary>Pragmatic email check (single address, no display name). Null is allowed.</summary>
    public static FieldRules<string?> EmailAddress(this FieldRules<string?> rules) =>
        rules.Must(v => v is null || IsEmail(v), ValidationCodes.Email);

    /// <summary>Absolute http(s) URL. Null or empty is allowed.</summary>
    public static FieldRules<string?> HttpUrl(this FieldRules<string?> rules) =>
        rules.Must(
            v => string.IsNullOrEmpty(v)
                || (Uri.TryCreate(v, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)),
            ValidationCodes.Url);

    // ---------- identifiers / enums / ranges ----------

    public static FieldRules<Guid> NotEmpty(this FieldRules<Guid> rules) =>
        rules.Must(v => v != Guid.Empty, ValidationCodes.Required);

    public static FieldRules<TEnum> IsInEnum<TEnum>(this FieldRules<TEnum> rules)
        where TEnum : struct, Enum =>
        rules.Must(Enum.IsDefined, ValidationCodes.InvalidValue);

    /// <summary>For value types bound as nullable so that "omitted" can be told apart from a default value.</summary>
    public static FieldRules<T?> NotNull<T>(this FieldRules<T?> rules)
        where T : struct =>
        rules.Must(v => v.HasValue, ValidationCodes.Required);

    /// <summary>Null is allowed (an omitted optional field).</summary>
    public static FieldRules<TEnum?> IsInEnum<TEnum>(this FieldRules<TEnum?> rules)
        where TEnum : struct, Enum =>
        rules.Must(v => v is null || Enum.IsDefined(v.Value), ValidationCodes.InvalidValue);

    public static FieldRules<T> InclusiveBetween<T>(this FieldRules<T> rules, T min, T max)
        where T : struct, IComparable<T> =>
        rules.Must(v => v.CompareTo(min) >= 0 && v.CompareTo(max) <= 0, ValidationCodes.OutOfRange, Params("min", min, "max", max));

    // ---------- helpers ----------

    public static IReadOnlyDictionary<string, object> Params(string key, object value) =>
        new Dictionary<string, object>(StringComparer.Ordinal) { [key] = value };

    public static IReadOnlyDictionary<string, object> Params(string key1, object value1, string key2, object value2) =>
        new Dictionary<string, object>(StringComparer.Ordinal) { [key1] = value1, [key2] = value2 };

    private static bool IsEmail(string value) =>
        value.Length <= 254
        && !value.Any(char.IsWhiteSpace)
        && MailAddress.TryCreate(value, out var address)
        && string.Equals(address.Address, value, StringComparison.Ordinal)
        && address.Host.Contains('.', StringComparison.Ordinal);
}
