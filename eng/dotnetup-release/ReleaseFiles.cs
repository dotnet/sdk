// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Text.Json;

namespace Microsoft.DotNet.Dotnetup.Release;

internal static class ReleaseFiles
{
    // Keep all preparation, publication, and planning metadata on the same human-readable camel-case JSON contract.
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    internal static async Task<T> ReadJsonAsync<T>(string path)
    {
        // JSON null is invalid release input rather than an empty inventory that could appear to succeed.
        using FileStream stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, s_jsonOptions).ConfigureAwait(false)
            ?? throw new InvalidDataException($"Empty JSON document: {path}");
    }

    internal static async Task WriteJsonAsync<T>(string path, T value)
    {
        // Each phase persists its typed audit records for the pipeline's always-running metadata attachment step.
        using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, s_jsonOptions).ConfigureAwait(false);
    }

    internal static async Task<string> HashAsync(string path)
    {
        // Hash only actual nonempty files, never silently follow a link to a different release payload.
        FileInfo file = new(path);
        if (!file.Exists || file.Length == 0 || (file.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException($"Missing, empty, or linked release asset: {path}");
        }

        using FileStream stream = file.OpenRead();
        return Convert.ToHexStringLower(await SHA512.HashDataAsync(stream).ConfigureAwait(false));
    }

    internal static void Debug(string message)
    {
        if (string.Equals(Environment.GetEnvironmentVariable("SYSTEM_DEBUG"), "true", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"##[debug]{message}");
        }
    }
}
