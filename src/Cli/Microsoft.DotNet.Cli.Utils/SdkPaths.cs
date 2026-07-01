// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using System.IO;

namespace Microsoft.DotNet.Cli.Utils;

/// <summary>
///  Canonical SDK filesystem locations for both the managed CLI and the Native AOT CLI. The first is
///  <see cref="SdkDirectory"/> - the versioned SDK directory (the folder that contains <c>dotnet.dll</c>,
///  <c>MSBuild.dll</c>, <c>Sdks\</c>, <c>DotnetTools\</c>, and the SDK targets). Prefer these over BCL
///  "where am I" APIs so paths resolve correctly in both hosts.
/// </summary>
/// <remarks>
///  <para>
///   Under the Native AOT CLI (<c>dotnet-aot</c> loaded directly by the muxer) the BCL location APIs do
///   NOT point at the versioned SDK directory: <see cref="System.AppContext.BaseDirectory"/> and
///   <see cref="System.Environment.ProcessPath"/> resolve to the install root (the muxer's own
///   directory) and <c>Assembly.Location</c> is empty. The AOT entry point therefore publishes the
///   resolved SDK directory in the <c>DOTNET_SDK_ROOT</c> environment variable, which this helper reads
///   first. Code that needs an SDK-relative path (MSBuild, tasks, tools, forwarders) should use this
///   helper rather than probing a dll path.
///  </para>
///  <para>
///   Resolution order (resolved once and cached): the <c>DOTNET_SDK_ROOT</c> environment variable, then
///   the directory of the SDK assembly (empty under single-file / NativeAOT), then
///   <see cref="System.AppContext.BaseDirectory"/>. See <c>src/Cli/dotnet-aot/SdkRootResolution.md</c>.
///  </para>
/// </remarks>
internal static class SdkPaths
{
    /// <summary>
    ///  The environment variable the Native AOT bridge uses to publish the resolved versioned SDK
    ///  directory for the assemblies compiled into it.
    /// </summary>
    public const string EnvironmentVariableName = "DOTNET_SDK_ROOT";

    private static string? s_sdkDirectory;

    /// <summary>
    ///  The versioned SDK directory, resolved once and cached. Prefers the <c>DOTNET_SDK_ROOT</c>
    ///  environment variable (published by the AOT bridge); otherwise the directory of the SDK assembly,
    ///  else <see cref="System.AppContext.BaseDirectory"/>.
    /// </summary>
    public static string SdkDirectory => s_sdkDirectory ??= ResolveSdkDirectory();

#if NET
    [UnconditionalSuppressMessage("AOT", "IL3000",
        Justification = "Assembly.Location is empty under single-file / NativeAOT; the empty result is " +
            "handled by falling back to AppContext.BaseDirectory, and the AOT bridge sets DOTNET_SDK_ROOT " +
            "(preferred above), so this path is only reached by the JIT-compiled managed CLI where " +
            "Assembly.Location is the versioned SDK directory.")]
#endif
    internal static string ResolveSdkDirectory()
    {
        // The AOT bridge publishes the resolved SDK directory in DOTNET_SDK_ROOT; prefer it.
        if (Environment.GetEnvironmentVariable(EnvironmentVariableName) is { Length: > 0 } sdkRoot)
        {
            return sdkRoot;
        }

        // The SDK assemblies ship in the versioned SDK directory, so the location of this assembly is that
        // directory for the JIT-compiled managed CLI. Under a single-file / NativeAOT deployment
        // Assembly.Location is empty (which is what the IL3000 analyzer flags); fall through to
        // AppContext.BaseDirectory in that case - the AOT bridge sets DOTNET_SDK_ROOT (preferred above).
        string? assemblyDirectory = Path.GetDirectoryName(typeof(SdkPaths).Assembly.Location);
        return string.IsNullOrEmpty(assemblyDirectory) ? AppContext.BaseDirectory : assemblyDirectory;
    }
}
