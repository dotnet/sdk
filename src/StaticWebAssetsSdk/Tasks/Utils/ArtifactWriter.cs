// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Sdk.StaticWebAssets.Utils;

public static class ArtifactWriter
{
    public static readonly JsonSerializerOptions ArtifactJsonSerializationOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static void PersistFileIfChanged<T>(this Task task, T manifest, string artifactPath)
    {
        var data = JsonSerializer.SerializeToUtf8Bytes(manifest, ArtifactJsonSerializationOptions);
        var newHash = ComputeHash(data);
        var fileExists = File.Exists(artifactPath);
        var existingManifestHash = fileExists ? ComputeHash(artifactPath) : null;

        if (!fileExists)
        {
            task.Log.LogMessage(MessageImportance.Low, $"Creating artifact because artifact file '{artifactPath}' does not exist.");
            File.WriteAllBytes(artifactPath, data);
        }
        else if (!string.Equals(newHash, existingManifestHash, StringComparison.Ordinal))
        {
            task.Log.LogMessage(MessageImportance.Low, $"Updating artifact because artifact version '{newHash}' is different from existing artifact hash '{existingManifestHash}'.");
            File.WriteAllBytes(artifactPath, data);
        }
        else
        {
            task.Log.LogMessage(MessageImportance.Low, $"Skipping artifact updated because artifact version '{existingManifestHash}' has not changed.");
        }
    }

    private static string ComputeHash(string artifactPath) => ComputeHash(File.ReadAllBytes(artifactPath));

    private static string ComputeHash(byte[] data)
    {
#if NET6_0_OR_GREATER
        var hash = SHA256.HashData(data);
        return Convert.ToBase64String(hash);
#else
        using var sha256 = SHA256.Create();
        return Convert.ToBase64String(sha256.ComputeHash(data));
#endif
    }
}
