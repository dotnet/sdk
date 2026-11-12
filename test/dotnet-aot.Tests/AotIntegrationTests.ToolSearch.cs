// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Cli.Tests;

public partial class AotIntegrationTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ToolSearch_MappedOriginsMatchManagedOutput(bool detail)
    {
        SkipIfDnUnavailable();
        await using var server = new ToolSearchTestServer();
        string directory = Directory.CreateTempSubdirectory("tool-search-").FullName;
        try
        {
            string configPath = Path.Combine(directory, "NuGet.config");
            server.WriteConfig(configPath);
            string[] args = ["tool", "search", "arbitrary text", "--configfile", configPath, .. detail ? new[] { "--detail" } : []];
            Dictionary<string, string> environment = new()
            {
                ["DOTNET_CLI_HOME"] = directory,
                ["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0",
                ["DOTNET_GENERATE_ASPNET_CERTIFICATE"] = "0",
                ["DOTNET_NOLOGO"] = "1",
                ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
                ["NO_COLOR"] = "1",
            };

            var native = RunDn(args, enableAot: true, extraEnv: environment);
            var managed = RunDn(args, enableAot: false, extraEnv: environment);

            Assert.AreEqual(0, native.exitCode, native.stderr);
            Assert.AreEqual(native.exitCode, managed.exitCode, managed.stderr);
            Assert.AreEqual(managed.stdout, native.stdout);
            native.stdout.Should().Contain("other.tool").And.Contain("contoso.tool")
                .And.NotContain("contoso.private.tool").And.NotContain("contoso.disabled.tool").And.NotContain("9.0.0");
            native.stderr.Should().BeEmpty();
            managed.stderr.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
