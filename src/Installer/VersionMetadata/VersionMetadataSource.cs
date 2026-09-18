// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.Dotnet.VersionMetadata;

/// <summary>Embeds readable Arcade product version and RID metadata for local equality checks.</summary>
internal static class VersionMetadataSource
{
    internal static byte[] CreateRecord(string version, string runtimeIdentifier)
    {
        var metadata = DotnetupVersionMetadataReader.Format(version, runtimeIdentifier);
        return Encoding.ASCII.GetBytes("DOTNETUP-VR-REC\0\u0001\0\0\0\0\0\0\0" +
            metadata.PadRight(DotnetupVersionMetadataReader.PayloadLength, '\0') + "END-VER\0");
    }

    internal static string Source(string version, string runtimeIdentifier)
    {
        var record = CreateRecord(version, runtimeIdentifier);
        var literal = string.Concat(record.Select(value => "\\u" + value.ToString("x4", System.Globalization.CultureInfo.InvariantCulture)));
        return "namespace Microsoft.Dotnet.Installation.Internal;\n" +
            "internal static partial class DotnetupVersionMetadata\n{\n" +
            "    private static System.ReadOnlySpan<byte> Record\n    {\n" +
            "        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]\n" +
            "        get => \"" + literal + "\"u8;\n    }\n}\n";
    }

    internal static string Validate(Stream artifact, string version, string runtimeIdentifier)
    {
        var expected = DotnetupVersionMetadataReader.Format(version, runtimeIdentifier);
        var actual = DotnetupVersionMetadataReader.Read(artifact);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The artifact metadata does not match its product version and RID.");
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