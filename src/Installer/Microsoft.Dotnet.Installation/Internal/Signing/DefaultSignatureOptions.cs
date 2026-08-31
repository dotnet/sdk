// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal.Signing;

/// <summary>
/// Singleton holding the dnup-default <see cref="SignatureVerificationOptions"/>. Lets
/// <see cref="ReleaseManifest"/> stay agnostic about PEM loading — it just asks here for the
/// process-wide options.
/// </summary>
internal static class DefaultSignatureOptions
{
    public static SignatureVerificationOptions Instance { get; } = new(
        TrustedRootsLoader.CodeSigningRoots,
        TrustedRootsLoader.TimestampRoots);
}
