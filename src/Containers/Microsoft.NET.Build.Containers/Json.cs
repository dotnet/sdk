using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Contains static methods for JSON serialization and deserialization used in the container build process.
/// OCI JSON serialization has a lot of quirks - the conformance tests expect `\t` indented JSON, and the behavior of JsonNode.ToJsonString()
/// may not match that of JsonSerializer.Serialize() in some cases, so we try to unify that here
/// </summary>
public static class Json
{
    public static JsonSerializerOptions _indentedOptions = new()
    {
        WriteIndented = false,
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, _indentedOptions);
    public static async Task SerializeAsync<T>(Stream stream, T value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        await JsonSerializer.SerializeAsync(stream, value, _indentedOptions, cancellationToken).ConfigureAwait(false);
    }
    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, _indentedOptions);
    public static T? Deserialize<T>(Stream stream) => JsonSerializer.Deserialize<T>(stream, _indentedOptions);
    public static ValueTask<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken) => JsonSerializer.DeserializeAsync<T>(stream, _indentedOptions, cancellationToken);

    public static byte[] GetBytes<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        // Serialize to a string and return its bytes
        return DigestAlgorithmExtensions.UTF8NoBom.GetBytes(Serialize(value));
    }

    public static byte[] GetBytes(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        // Return the bytes of the JSON string
        return DigestAlgorithmExtensions.UTF8NoBom.GetBytes(json);
    }

    public static long GetContentLength<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        // Serialize to a string and return its length
        return DigestAlgorithmExtensions.UTF8NoBom.GetBytes(Serialize(value)).Length;
    }
}
