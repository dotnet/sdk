// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli;
using Xunit;

namespace dotnet.Tests;

public class EnvironmentVariableNamesTests
{
    [Theory]
    [InlineData("os.1.2-x86", Architecture.X86)]
    [InlineData("os.1.2-x64", Architecture.X64)]
    [InlineData("os.1.2-arm", Architecture.Arm)]
    [InlineData("os.1.2-arm64", Architecture.Arm64)]
    [InlineData("os.1.2-wasm", Architecture.Wasm)]
    [InlineData("os.1.2-s390x", Architecture.S390x)]
    [InlineData("os.1.2-loongarch64", Architecture.LoongArch64)]
    [InlineData("os.1.2-armv6", Architecture.Armv6)]
    [InlineData("os.1.2-ppc64le", Architecture.Ppc64le)]
    [InlineData("os.1.2-lOOngaRch64", Architecture.LoongArch64)] // case-insensitive
    [InlineData("os-x86", Architecture.X86)]
    [InlineData("-x86", Architecture.X86)]
    [InlineData("-x86-", Architecture.X86)]
    public static void TryParseArchitecture(string rid, Architecture expected)
    {
        Assert.True(EnvironmentVariableNames.TryParseArchitecture(rid, out var actual));
        Assert.Equal(expected, actual);

        Assert.True(EnvironmentVariableNames.TryParseArchitecture(rid + "-xyz", out actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-")]
    [InlineData("--")]
    [InlineData("---")]
    [InlineData("x86")]
    [InlineData("os")]
    [InlineData("os.")]
    [InlineData("os.1")]
    [InlineData("os.1.2")]
    [InlineData("os.1.2-")]
    [InlineData("os.1.2--")]
    [InlineData("os.1.2-unknown")]
    [InlineData("os.1.2-unknown-")]
    [InlineData("os.1.2-unknown-x")]
    [InlineData("os.1.2-armel")] // currently not defined
    public static void TryParseArchitecture_Invalid(string rid)
    {
        Assert.False(EnvironmentVariableNames.TryParseArchitecture(rid, out _));
    }

    [Theory]
    [InlineData("os-unknown", null, Architecture.X86, true, "DOTNET_ROOT")]
    [InlineData("os-unknown", null, Architecture.X86, false, "DOTNET_ROOT(x86)")]
    [InlineData("os-unknown", "v5.0", Architecture.X86, true, "DOTNET_ROOT")]
    [InlineData("os-unknown", "v5.0", Architecture.X86, false, "DOTNET_ROOT(x86)")]
    [InlineData("os-unknown", "v6.0", Architecture.Wasm, true, "DOTNET_ROOT_WASM")]
    [InlineData("os-unknown", "v6.0", Architecture.Wasm, false, "DOTNET_ROOT_WASM")]
    [InlineData("os-x86", null, Architecture.X86, true, "DOTNET_ROOT")]
    [InlineData("os-x86", null, Architecture.X86, false, "DOTNET_ROOT(x86)")]
    [InlineData("os-x86", "v5.0", Architecture.X86, true, "DOTNET_ROOT")]
    [InlineData("os-x86", "v5.0", Architecture.X86, false, "DOTNET_ROOT(x86)")]
    [InlineData("os-x86", "v6.0", Architecture.X86, true, "DOTNET_ROOT_X86")]
    [InlineData("os-x86", "v6.0", Architecture.X86, false, "DOTNET_ROOT_X86")]
    [InlineData("os-x64", "v6.0", Architecture.X86, false, null)]
    public static void TryGetDotNetRootVariableName(string rid, string frameworkVersion, Architecture currentArchitecture, bool is64bit, string expected)
    {
        var parsedVersion = EnvironmentVariableNames.TryParseTargetFrameworkVersion(frameworkVersion);
        Assert.Equal(expected, EnvironmentVariableNames.TryGetDotNetRootVariableNameImpl(rid, "", parsedVersion, currentArchitecture, is64bit));
        Assert.Equal(expected, EnvironmentVariableNames.TryGetDotNetRootVariableNameImpl("", rid, parsedVersion, currentArchitecture, is64bit));
    }
}
