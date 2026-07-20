// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.DotNet.Cli.Commands.Test;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class TestApplicationTests
{
    // Microsoft.Testing.Platform server mode ("--server dotnettestcli --dotnet-test-pipe") makes
    // the test host connect back to a named pipe, which wasm runtimes (browser/wasi) cannot open.
    // TestApplication uses IsWasmRuntimeIdentifier to decide whether to launch the host standalone
    // (skipping the server-mode options) so the host runs and reports via exit code + stdout
    // instead of throwing PlatformNotSupportedException. This guards that decision.
    [TestMethod]
    [DataRow("browser-wasm", true)]
    [DataRow("wasi-wasm", true)]
    [DataRow("browser", true)]
    [DataRow("wasi", true)]
    [DataRow("BROWSER-WASM", true)]
    [DataRow("win-x64", false)]
    [DataRow("linux-x64", false)]
    [DataRow("osx-arm64", false)]
    [DataRow("linux-musl-arm64", false)]
    [DataRow("", false)]
    [DataRow(null, false)]
    public void IsWasmRuntimeIdentifier_DetectsWasmRuntimes(string? runtimeIdentifier, bool expected)
    {
        TestApplication.IsWasmRuntimeIdentifier(runtimeIdentifier).Should().Be(expected);
    }

    // Wasm (standalone) hosts can't open a named pipe, so they must NOT get server mode; instead
    // they're asked to write an on-disk TRX. Non-wasm hosts keep the server-mode pipe options.
    [TestMethod]
    public void GetHostModeArguments_Standalone_RequestsTrxAndSkipsServerMode()
    {
        var args = TestApplication.GetHostModeArguments(launchStandalone: true, pipeName: "/tmp/abc", trxFileName: "blazor-wasm.trx");

        args.Should().Contain("--report-trx");
        args.Should().Contain("--report-trx-filename");
        args.Should().Contain("blazor-wasm.trx");
        args.Should().NotContain("--server");
        args.Should().NotContain("--dotnet-test-pipe");
    }

    [TestMethod]
    public void GetHostModeArguments_NonStandalone_UsesServerModeAndSkipsTrx()
    {
        var args = TestApplication.GetHostModeArguments(launchStandalone: false, pipeName: "/tmp/abc", trxFileName: "blazor-wasm.trx");

        args.Should().Contain("--server");
        args.Should().Contain("dotnettestcli");
        args.Should().Contain("--dotnet-test-pipe");
        args.Should().Contain("/tmp/abc");
        args.Should().NotContain("--report-trx");
    }

    // Wasm hosts always need a results directory (they report via TRX), so it's defaulted when the
    // user didn't pass one; non-wasm runs are unchanged (null unless explicitly requested).
    [TestMethod]
    public void GetStandaloneResultsDirectory_DefaultsForWasmWhenUnset()
    {
        TestApplication.GetStandaloneResultsDirectory(userResultsDirectory: null, launchStandalone: true, currentDirectory: "/work")
            .Should().Be(Path.Combine("/work", "TestResults"));
    }

    [TestMethod]
    public void GetStandaloneResultsDirectory_KeepsUserValue()
    {
        TestApplication.GetStandaloneResultsDirectory(userResultsDirectory: "/custom", launchStandalone: true, currentDirectory: "/work")
            .Should().Be("/custom");
        TestApplication.GetStandaloneResultsDirectory(userResultsDirectory: "/custom", launchStandalone: false, currentDirectory: "/work")
            .Should().Be("/custom");
    }

    [TestMethod]
    public void GetStandaloneResultsDirectory_NullForNonWasmWhenUnset()
    {
        TestApplication.GetStandaloneResultsDirectory(userResultsDirectory: null, launchStandalone: false, currentDirectory: "/work")
            .Should().BeNull();
    }
}
