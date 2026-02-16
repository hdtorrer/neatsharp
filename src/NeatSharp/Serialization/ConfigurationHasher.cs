using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using NeatSharp.Configuration;

namespace NeatSharp.Serialization;

/// <summary>
/// Computes a deterministic SHA-256 hash of a <see cref="NeatSharpOptions"/> instance.
/// Used to detect configuration drift between checkpoint save and resume.
/// </summary>
public static class ConfigurationHasher
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Computes a SHA-256 hash of the specified configuration options.
    /// </summary>
    /// <param name="options">The configuration to hash.</param>
    /// <returns>A 64-character lowercase hexadecimal SHA-256 hash string.</returns>
    public static string ComputeHash(NeatSharpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var bytes = JsonSerializer.SerializeToUtf8Bytes(options, SerializerOptions);
        var hashBytes = SHA256.HashData(bytes);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
