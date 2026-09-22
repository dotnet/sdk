// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if DOTNET_AOT_NATIVE_RUNTIME
using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Cli.Tests;

internal static class AotTestResourceModuleInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        string? sdkDirectory = Environment.GetEnvironmentVariable("DOTNET_AOT_TEST_SDK_DIRECTORY");
        AotResourceManagerProvider.Configure(sdkDirectory);
    }
}
#endif
