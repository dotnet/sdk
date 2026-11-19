// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Cli.Tests;

public partial class AotParserTests
{
    [TestMethod]
    public async Task ToolSearchServerShutsDownWhileWaitingForRequests()
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            await using var server = new ToolSearchTestServer();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task InvokeAotToolSearchCommand_FiltersMappedOriginsOverV3(bool detail)
    {
        await using var server = new ToolSearchTestServer();
        string configPath = Path.GetTempFileName();
        server.WriteConfig(configPath);
        try
        {
            var (exitCode, stdout, stderr) = InvokeWithCapture(
                ["tool", "search", "arbitrary text", "--configfile", configPath, .. detail ? new[] { "--detail" } : []]);

            Assert.AreEqual(0, exitCode);
            stdout.Should().Contain("other.tool").And.Contain("contoso.tool")
                .And.NotContain("contoso.private.tool").And.NotContain("contoso.disabled.tool").And.NotContain("9.0.0");
            stderr.Should().BeEmpty();
        }
        finally
        {
            File.Delete(configPath);
        }
    }
}
