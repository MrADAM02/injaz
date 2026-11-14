namespace Injaz.Application.Validation;

/// <summary>Collects field errors for one request. Field names are normalized to the JSON (camelCase) names.</summary>
public sealed class ValidationContext
{
    private readonly Dictionary<string, List<ValidationError>> _errors = new(StringComparer.Ordinal);

    public bool IsValid => _errors.Count == 0;

    public IReadOnlyDictionary<string, IReadOnlyList<ValidationError>> Errors =>
        _errors.ToDictionary(e => e.Key, e => (IReadOnlyList<ValidationError>)e.Value.AsReadOnly(), StringComparer.Ordinal);

    /// <summary>Starts a rule chain for a string field. Pass <c>nameof(request.Property)</c> as <paramref name="field"/>.</summary>
    public FieldRules<string?> RuleFor(string field, string? value) => new(this, FieldName.Normalize(field), value);

    /// <summary>Starts a rule chain for any field. Pass <c>nameof(request.Property)</c> as <paramref name="field"/>.</summary>
    public FieldRules<T> RuleFor<T>(string field, T value) => new(this, FieldName.Normalize(field), value);

    public void AddError(string field, string code, IReadOnlyDictionary<string, object>? parameters = null) =>
        AddNormalizedError(FieldName.Normalize(field), code, parameters);

    public bool HasErrorFor(string field) => _errors.ContainsKey(FieldName.Normalize(field));

    internal bool HasNormalizedError(string normalizedField) => _errors.ContainsKey(normalizedField);

    internal void AddNormalizedError(string normalizedField, string code, IReadOnlyDictionary<string, object>? parameters)
    {
        if (!_errors.TryGetValue(normalizedField, out var list))
        {
            list = [];
            _errors[normalizedField] = list;
        }

        list.Add(new ValidationError(code, parameters));
    }
}

/// <summary>
/// A fluent rule chain for one field. Evaluation stops at the first failing rule for that field, so a
/// missing email reports <c>required</c> only — not <c>required</c> plus <c>email</c>.
/// </summary>
public readonly struct FieldRules<T>
{
    private readonly ValidationContext _context;

    internal FieldRules(ValidationContext context, string field, T value)
    {
        _context = context;
        Field = field;
        Value = value;
    }

    public string Field { get; }

    public T Value { get; }

    public FieldRules<T> Must(Func<T, bool> predicate, string code, IReadOnlyDictionary<string, object>? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (!_context.HasNormalizedError(Field) && !predicate(Value))
        {
            _context.AddNormalizedError(Field, code, parameters);
        }

        return this;
    }
}

public static class FieldName
{
    /// <summary>
    /// Converts a C# member path to its JSON form: <c>Title</c> → <c>title</c>,
    /// <c>Items[0].Text</c> → <c>items[0].text</c>. A leading <c>$.</c> (System.Text.Json paths) is removed.
    /// </summary>
    public static string Normalize(string field)
    {
        ArgumentNullException.ThrowIfNull(field);

        if (field.StartsWith("$.", StringComparison.Ordinal))
        {
            field = field[2..];
        }

        if (field.Length == 0 || field == "$")
        {
            return "body";
        }

        var segments = field.Split('.');
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (segment.Length > 0 && char.IsUpper(segment[0]))
            {
                segments[i] = char.ToLowerInvariant(segment[0]) + segment[1..];
            }
        }

        return string.Join('.', segments);
    }
}
