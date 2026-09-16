// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Text;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.Dotnet.BuildIdentity;

/// <summary>Encodes the Arcade product version and RID as an embedded equality token.</summary>
internal static class BuildIdentityMetadata
{
    internal static string Compute(string version, string runtimeIdentifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeIdentifier);
        if (version.Any(char.IsWhiteSpace) || runtimeIdentifier.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("Build identity metadata must not contain whitespace.");
        }

        return Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes($"dotnetup-version-id-v1\n{version}\n{runtimeIdentifier}")));
    }

    internal static string Source(string version, string runtimeIdentifier)
    {
        var identity = Compute(version, runtimeIdentifier);
        return "namespace Microsoft.Dotnet.Installation.Internal;\n" +
            "internal static partial class DotnetupBuildIdentity\n{\n" +
            "    private static System.ReadOnlySpan<byte> Record\n    {\n" +
            "        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]\n" +
            "        get => \"DOTNETUP-ID-REC\\0\\u0001\\0\\0\\0\\u0040\\0\\0\\0" +
            identity + "END-ID\\0\\0\"u8;\n    }\n}\n";
    }

    internal static string Validate(Stream artifact, string version, string runtimeIdentifier)
    {
        var expected = Compute(version, runtimeIdentifier);
        var actual = DotnetupBuildIdentityReader.Read(artifact);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The artifact identity does not match its product version and RID.");
        }

        return actual;
    }

    internal static void WriteIfChanged(string path, ReadOnlySpan<byte> content)
    {
        if (File.Exists(path) && content.SequenceEqual(File.ReadAllBytes(path)))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, content.ToArray());
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}