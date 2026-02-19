// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using LibraryUrlSanitizer = Microsoft.Dotnet.Installation.Internal.UrlSanitizer;

namespace Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

/// <summary>
/// Thin wrapper over <see cref="LibraryUrlSanitizer"/> for backward compatibility.
/// The canonical implementation lives in the installation library so that
/// sanitization happens at the source (closest to where tags are emitted).
/// </summary>
public static class UrlSanitizer
{
    public static IReadOnlyList<string> KnownDownloadDomains => LibraryUrlSanitizer.KnownDownloadDomains;

    public static string SanitizeDomain(string? url) => LibraryUrlSanitizer.SanitizeDomain(url);
}
