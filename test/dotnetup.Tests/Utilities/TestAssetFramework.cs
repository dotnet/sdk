// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.Versioning;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

internal static class TestAssetFramework
{
    public static string Current { get; } = Resolve();

    private static string Resolve()
    {
        var assembly = typeof(TestAssetFramework).Assembly;
        return assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(attribute => attribute.Key == "TestAssetTargetFramework")?.Value
            ?? "net" + new FrameworkName(
                assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName).Version.ToString(2);
    }
}
