// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Dotnet.Installation.Internal.Signing;

/// <summary>
/// Loads Microsoft trusted root certificates from embedded PEM resources. The PEM files come
/// directly from <c>src/Layout/redist/trustedroots/</c> via &lt;EmbeddedResource&gt; entries in
/// <c>Microsoft.Dotnet.Installation.csproj</c> — the same files the SDK ships for NuGet
/// package signing validation. Single source of truth, zero drift.
/// </summary>
internal static class TrustedRootsLoader
{
    private static readonly Lazy<X509Certificate2Collection> s_codeSignRoots =
        new(() => LoadPem("codesignctl.pem"), LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<X509Certificate2Collection> s_timestampRoots =
        new(() => LoadPem("timestampctl.pem"), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Trusted roots for code-signing chain validation.</summary>
    public static X509Certificate2Collection CodeSigningRoots => s_codeSignRoots.Value;

    /// <summary>Trusted roots for RFC 3161 timestamp chain validation.</summary>
    public static X509Certificate2Collection TimestampRoots => s_timestampRoots.Value;

    private static X509Certificate2Collection LoadPem(string resourceName)
    {
        var assembly = typeof(TrustedRootsLoader).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found in {assembly.GetName().Name}. " +
                $"Ensure {resourceName} is included as an EmbeddedResource in Microsoft.Dotnet.Installation.csproj.");

        using var reader = new StreamReader(stream);
        string pem = reader.ReadToEnd();

        var collection = new X509Certificate2Collection();
        collection.ImportFromPem(pem);
        return collection;
    }
}
