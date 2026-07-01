// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli;

namespace dotnet.Tests;

[TestClass]
public class EnvironmentVariableNamesTests
{
    [TestMethod]
    [DataRow("os.1.2-x86", Architecture.X86)]
    [DataRow("os.1.2-x64", Architecture.X64)]
    [DataRow("os.1.2-arm", Architecture.Arm)]
    [DataRow("os.1.2-arm64", Architecture.Arm64)]
    [DataRow("os.1.2-wasm", Architecture.Wasm)]
    [DataRow("os.1.2-s390x", Architecture.S390x)]
    [DataRow("os.1.2-loongarch64", Architecture.LoongArch64)]
    [DataRow("os.1.2-armv6", Architecture.Armv6)]
    [DataRow("os.1.2-ppc64le", Architecture.Ppc64le)]
    [DataRow("os.1.2-lOOngaRch64", Architecture.LoongArch64)] // case-insensitive
    [DataRow("os-x86", Architecture.X86)]
    [DataRow("-x86", Architecture.X86)]
    [DataRow("-x86-", Architecture.X86)]
    public void TryParseArchitecture(string rid, Architecture expected)
    {
        Assert.IsTrue(EnvironmentVariableNames.TryParseArchitecture(rid, out var actual));
        Assert.AreEqual(expected, actual);

        Assert.IsTrue(EnvironmentVariableNames.TryParseArchitecture(rid + "-xyz", out actual));
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("-")]
    [DataRow("--")]
    [DataRow("---")]
    [DataRow("x86")]
    [DataRow("os")]
    [DataRow("os.")]
    [DataRow("os.1")]
    [DataRow("os.1.2")]
    [DataRow("os.1.2-")]
    [DataRow("os.1.2--")]
    [DataRow("os.1.2-unknown")]
    [DataRow("os.1.2-unknown-")]
    [DataRow("os.1.2-unknown-x")]
    [DataRow("os.1.2-armel")] // currently not defined
    public void TryParseArchitecture_Invalid(string rid)
    {
        Assert.IsFalse(EnvironmentVariableNames.TryParseArchitecture(rid, out _));
    }

    [TestMethod]
    [DataRow("os-x86", null, Architecture.X86, true, "DOTNET_ROOT")]
    [DataRow("os-x86", null, Architecture.X86, false, "DOTNET_ROOT(x86)")]
    [DataRow("os-x86", "v5.0", Architecture.X86, true, "DOTNET_ROOT")]
    [DataRow("os-x86", "v5.0", Architecture.X86, false, "DOTNET_ROOT(x86)")]
    [DataRow("os-x86", "v6.0", Architecture.X86, true, "DOTNET_ROOT_X86")]
    [DataRow("os-x86", "v6.0", Architecture.X86, false, "DOTNET_ROOT_X86")]
    [DataRow("os-x64", "v5.0", Architecture.X64, true, "DOTNET_ROOT")]
    [DataRow("os-x64", "v6.0", Architecture.X64, true, "DOTNET_ROOT_X64")]
    [DataRow("os-arm64", "v6.0", Architecture.Arm64, true, "DOTNET_ROOT_ARM64")]
    [DataRow("os-armv6", "v6.0", Architecture.Armv6, true, "DOTNET_ROOT_ARMV6")]
    [DataRow("os-armv6", "v6.0", Architecture.Arm64, true, null)]
    [DataRow("os-x64", "v6.0", Architecture.X86, false, null)]
    public void TryGetDotNetRootVariableName_KnownArchitecture(string rid, string frameworkVersion, Architecture currentArchitecture, bool is64bit, string expected)
    {
        var parsedVersion = EnvironmentVariableNames.TryParseTargetFrameworkVersion(frameworkVersion);
        Assert.AreEqual(expected, EnvironmentVariableNames.TryGetDotNetRootVariableNameImpl(rid, "os-unknown", parsedVersion, currentArchitecture, is64bit));
        Assert.AreEqual(expected, EnvironmentVariableNames.TryGetDotNetRootVariableNameImpl(rid, "os-armv6", parsedVersion, currentArchitecture, is64bit));
        Assert.AreEqual(expected, EnvironmentVariableNames.TryGetDotNetRootVariableNameImpl("os-unknown", rid, parsedVersion, currentArchitecture, is64bit));
    }

    [TestMethod]
    [DataRow(null, Architecture.X86, true, "DOTNET_ROOT")]
    [DataRow(null, Architecture.X86, false, "DOTNET_ROOT(x86)")]
    [DataRow("v5.0", Architecture.X86, true, "DOTNET_ROOT")]
    [DataRow("v5.0", Architecture.X86, false, "DOTNET_ROOT(x86)")]
    [DataRow("v6.0", Architecture.X86, true, "DOTNET_ROOT_X86")]
    [DataRow("v6.0", Architecture.X86, false, "DOTNET_ROOT_X86")]
    [DataRow("v5.0", Architecture.X64, true, "DOTNET_ROOT")]
    [DataRow("v6.0", Architecture.X64, true, "DOTNET_ROOT_X64")]
    [DataRow("v6.0", Architecture.Arm64, true, "DOTNET_ROOT_ARM64")]
    [DataRow("v6.0", Architecture.Armv6, true, "DOTNET_ROOT_ARMV6")]
    [DataRow("v6.0", Architecture.Wasm, true, "DOTNET_ROOT_WASM")]
    [DataRow("v6.0", Architecture.Wasm, false, "DOTNET_ROOT_WASM")]
    public void TryGetDotNetRootVariableName_UnknownArchitecture(string frameworkVersion, Architecture currentArchitecture, bool is64bit, string expected)
    {
        var parsedVersion = EnvironmentVariableNames.TryParseTargetFrameworkVersion(frameworkVersion);
        Assert.AreEqual(expected, EnvironmentVariableNames.TryGetDotNetRootVariableNameImpl("os-unknown", "os-unknown", parsedVersion, currentArchitecture, is64bit));
    }
}
