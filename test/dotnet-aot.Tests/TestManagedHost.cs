// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli;

/// <summary>
///  Test stub for ManagedHost. The real ManagedHost uses internal NativeWrapper APIs
///  (hostfxr interop) that aren't accessible from the test assembly.
///  NativeEntryPoint.ExecuteCore references ManagedHost.RunApp but never reaches it
///  in unit tests because the fallback files (dotnet.dll, runtimeconfig.json) don't exist.
/// </summary>
internal sealed class ManagedHost : IDisposable
{
    public ManagedHost(string runtimeConfigPath, string dotnetRoot)
    {
        throw new NotImplementedException("ManagedHost is stubbed for testing. This should not be called in unit tests.");
    }

    public static int RunApp(string hostPath, string dotnetRoot, string hostfxrPath, string[] args)
    {
        throw new NotImplementedException("ManagedHost.RunApp is stubbed for testing. This should not be called in unit tests.");
    }

    public void Dispose() { }
}
