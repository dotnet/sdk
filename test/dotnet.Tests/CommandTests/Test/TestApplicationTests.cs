// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
}
