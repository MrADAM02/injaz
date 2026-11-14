using System.Text.Json;
using System.Text.Json.Serialization;
using Injaz.Application.Common;

namespace Injaz.Api.Common;

/// <summary>
/// (De)serializes <see cref="Optional{T}"/>. System.Text.Json only invokes a converter for properties that are
/// present in the JSON, so an absent property stays <see cref="Optional{T}.Unset"/>, while <c>"field": null</c>
/// becomes a set value of null — exactly the distinction JSON merge-patch needs.
/// </summary>
internal sealed class OptionalJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(OptionalConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]))!;

    private sealed class OptionalConverter<T> : JsonConverter<Optional<T>>
    {
        // Needed so "field": null reaches Read (otherwise STJ would assign default = unset).
        public override bool HandleNull => true;

        /// <remarks>
        /// Uses the inner type's converter directly rather than a nested <c>JsonSerializer.Deserialize</c>: a nested
        /// call stamps its own root path ("$") on errors, which would hide the field name from validation errors.
        /// </remarks>
        public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var converter = (JsonConverter<T>)options.GetConverter(typeof(T));
            return new(converter.Read(ref reader, typeof(T), options)!);
        }

        public override void Write(Utf8JsonWriter writer, Optional<T> value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                JsonSerializer.Serialize(writer, value.Value, options);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
