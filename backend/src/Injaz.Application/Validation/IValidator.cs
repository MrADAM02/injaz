namespace Injaz.Application.Validation;

/// <summary>
/// Validates the shape of a request (required fields, lengths, formats). Rules that need the database
/// (uniqueness, existence) belong in the handler and surface as Conflict/NotFound instead.
/// Implementations are discovered automatically by assembly scanning.
/// </summary>
public interface IValidator<T>
{
    void Validate(T request, ValidationContext context);
}

/// <summary>A single failed rule. <see cref="Code"/> is an i18n key suffix (frontend: <c>validation.{code}</c>).</summary>
public sealed record ValidationError(string Code, IReadOnlyDictionary<string, object>? Parameters = null);

/// <summary>Stable error codes shared with the frontend's translation files.</summary>
public static class ValidationCodes
{
    public const string Required = "required";
    public const string MinLength = "minLength";
    public const string MaxLength = "maxLength";
    public const string Email = "email";
    public const string InvalidValue = "invalidValue";
    public const string OutOfRange = "outOfRange";
    public const string Url = "url";
}
