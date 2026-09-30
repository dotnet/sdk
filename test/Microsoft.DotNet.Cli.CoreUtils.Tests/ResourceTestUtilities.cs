// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Resources;

namespace Microsoft.DotNet.Cli.CoreUtils.Tests;

internal static class ResourceTestUtilities
{
    internal static Assembly TestAssembly => typeof(ResourceTestUtilities).Assembly;

    internal static string NeutralBaseName
    {
        get
        {
            string resourceName = TestAssembly.GetManifestResourceNames()
                .Single(name => name.EndsWith("TestStrings.resources", StringComparison.Ordinal));

            return resourceName[..^".resources".Length];
        }
    }

    internal static byte[] WriteResources(params (string Key, object? Value)[] entries)
    {
        using MemoryStream stream = new();
        using (ResourceWriter writer = new(stream))
        {
            foreach ((string key, object? value) in entries)
            {
                writer.AddResource(key, value);
            }

            writer.Generate();
        }

        return stream.ToArray();
    }

    internal static void WriteResources(
        string root,
        string cultureName,
        string baseName,
        params (string Key, object? Value)[] entries)
    {
        string cultureDirectory = Path.Combine(root, cultureName);
        Directory.CreateDirectory(cultureDirectory);
        File.WriteAllBytes(
            Path.Combine(cultureDirectory, $"{baseName}.resources"),
            WriteResources(entries));
    }
}

internal sealed class TestDirectory : IDisposable
{
    internal TestDirectory(string testRunDirectory)
    {
        Path = System.IO.Path.Combine(
            testRunDirectory,
            nameof(Microsoft.DotNet.Cli.CoreUtils.Tests),
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Path);
    }

    internal string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
