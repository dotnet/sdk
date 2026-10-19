// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class SelfUpdateManagedHostProcessTests
{
    private static string s_dotnetPath = null!;
    private static string s_assemblyPath = null!;

    [ClassInitialize]
    public static void Initialize(TestContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        s_dotnetPath = SelfUpdateTestFiles.ResolveDotnetHostPath();
        s_assemblyPath = DotnetupTestUtilities.GetDotnetupManagedAssemblyPath();
    }

    [TestMethod]
    public void DotnetHostedDllCannotSelfUpdate()
    {
        using var environment = new TestEnvironment();
        var result = DotnetupTestUtilities.RunDotnetupProcess(
            [s_assemblyPath, "self", "update"],
            captureOutput: true,
            workingDirectory: environment.TempRoot,
            executablePath: s_dotnetPath,
            timeout: TimeSpan.FromSeconds(30),
            environmentVariables: new()
            {
                ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
                ["DOTNET_CLI_UI_LANGUAGE"] = "en-US",
                ["DOTNET_DOTNETUP_DATA_DIR"] = Path.Combine(environment.TempRoot, "data"),
                ["DOTNET_CLI_HOME"] = Path.Combine(environment.TempRoot, "home"),
            });

        Assert.AreEqual(1, result.exitCode, result.output);
        Assert.Contains(Strings.SelfUpdateUnsupportedHost, result.output);
    }
}
