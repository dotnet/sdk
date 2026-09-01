// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.NativeWrapper;

/// <summary>
///  Common status codes returned by hostfxr APIs.
/// </summary>
/// <remarks>
///  <para>
///   Success is indicated by 0. Positive values may indicate partial success or informational
///   status. Negative values (shown as hex) indicate errors. These values match the native
///   <c>StatusCode</c> enum in the hosting layer.
///  </para>
/// </remarks>
internal enum StatusCode : uint
{
    /// <summary>Operation completed successfully.</summary>
    Success = 0,

    /// <summary>One or more arguments are invalid.</summary>
    InvalidArgFailure = 0x80008081,

    /// <summary>Failed to load a required native library (coreclr, hostpolicy).</summary>
    CoreHostLibLoadFailure = 0x80008082,

    /// <summary>A required native library was not found.</summary>
    CoreHostLibMissingFailure = 0x80008083,

    /// <summary>Failed to find a required entry point in a native library.</summary>
    CoreHostEntryPointFailure = 0x80008084,

    /// <summary>Failed to determine the path of the current host executable.</summary>
    CoreHostCurHostFindFailure = 0x80008085,

    /// <summary>Failed to resolve the path to coreclr.</summary>
    CoreClrResolveFailure = 0x80008087,

    /// <summary>Failed to bind to coreclr.</summary>
    CoreClrBindFailure = 0x80008088,

    /// <summary>Failed to initialize the CoreCLR runtime.</summary>
    CoreClrInitFailure = 0x80008089,

    /// <summary>Failed to execute the managed application entry point.</summary>
    CoreClrExeFailure = 0x8000808a,

    /// <summary>Failed to initialize the dependency resolver.</summary>
    ResolverInitFailure = 0x8000808b,

    /// <summary>Dependency resolution failed.</summary>
    ResolverResolveFailure = 0x8000808c,

    /// <summary>Failed to find the current executable path.</summary>
    LibHostCurExeFindFailure = 0x8000808d,

    /// <summary>Host initialization failed.</summary>
    LibHostInitFailure = 0x8000808e,

    /// <summary>Failed to find a compatible SDK.</summary>
    LibHostSdkFindFailure = 0x80008091,

    /// <summary>Invalid arguments passed to the host library.</summary>
    LibHostInvalidArgs = 0x80008092,

    /// <summary>The runtime configuration file is invalid or malformed.</summary>
    InvalidConfigFile = 0x80008093,

    /// <summary>The application argument is not runnable.</summary>
    AppArgNotRunnable = 0x80008094,

    /// <summary>The apphost executable is not bound to an application.</summary>
    AppHostExeNotBoundFailure = 0x80008095,

    /// <summary>A required framework was not found.</summary>
    FrameworkMissingFailure = 0x80008096,

    /// <summary>The host API version is not supported.</summary>
    HostApiUnsupportedVersion = 0x80008097,

    /// <summary>The provided buffer is too small for the result.</summary>
    HostApiBufferTooSmall = 0x80008098,

    /// <summary>An unknown command was passed to the host library.</summary>
    LibHostUnknownCommand = 0x80008099,

    /// <summary>SDK resolution failed.</summary>
    SdkResolveFailure = 0x8000809b,

    /// <summary>Incompatible framework versions were requested.</summary>
    FrameworkCompatFailure = 0x8000809c,

    /// <summary>Framework resolution should be retried (internal use).</summary>
    FrameworkCompatRetry = 0x8000809d,

    /// <summary>Failed to extract files from a single-file bundle.</summary>
    BundleExtractionFailure = 0x8000809e,

    /// <summary>I/O error during bundle extraction.</summary>
    BundleExtractionIOError = 0x8000809f,

    /// <summary>A duplicate property was specified.</summary>
    LibHostDuplicateProperty = 0x800080a0,

    /// <summary>The requested API scenario is not supported.</summary>
    HostApiUnsupportedScenario = 0x800080a1,

    /// <summary>A required host feature is disabled.</summary>
    HostFeatureDisabled = 0x800080a2,

    /// <summary>Failed to determine the current host path.</summary>
    CurrentHostFindFailure = 0x800080a3,

    /// <summary>The host is in an invalid state for the requested operation.</summary>
    HostInvalidState = 0x800080a4,

    /// <summary>The requested runtime property was not found.</summary>
    HostPropertyNotFound = 0x800080a5
}
