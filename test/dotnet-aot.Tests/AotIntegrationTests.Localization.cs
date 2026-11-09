// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

namespace Microsoft.DotNet.Cli.Tests;

public partial class AotIntegrationTests
{
    [TestMethod]
    [DataRow("de-DE", ".NET-Generator", false)]
    [DataRow("de-DE", ".NET-Generator", true)]
    [DataRow("fr-FR", "G\u00e9n\u00e9rateur .NET", false)]
    [DataRow("fr-FR", "G\u00e9n\u00e9rateur .NET", true)]
    [DataRow("zh-CN", ".NET \u751f\u6210\u5668", false)]
    [DataRow("zh-CN", ".NET \u751f\u6210\u5668", true)]
    public void Build_help_uses_the_requested_language(string language, string description, bool enableAot)
    {
        ReadLocalizedHelp(language, enableAot, "build").Should().Contain(description).And.NotContain(".NET Builder");
    }

    [TestMethod]
    [CombinatorialData]
    public void Native_help_matches_managed_help(
        [CombinatorialValues("cs-CZ", "de-DE", "es-ES", "fr-FR", "it-IT", "ja-JP", "ko-KR", "pl-PL", "pt-BR", "ru-RU", "tr-TR", "zh-CN", "zh-TW")] string language,
        [CombinatorialValues("build", "restore", "sln")] string command)
    {
        ReadLocalizedHelp(language, enableAot: true, command).Should().Be(ReadLocalizedHelp(language, enableAot: false, command));
    }

    private string ReadLocalizedHelp(string language, bool enableAot, string command)
    {
        SkipIfDnUnavailable();
        string? managedDirectory = Environment.GetEnvironmentVariable("DOTNET_AOT_TEST_MANAGED_CLI_DIRECTORY");
        Assert.IsNotNull(managedDirectory, "Run the native test harness to build the matching managed CLI baseline.");
        Assert.IsTrue(File.Exists(Path.Combine(managedDirectory, "dotnet.dll")));
        Assert.IsTrue(File.Exists(Path.Combine(managedDirectory, "dotnet.runtimeconfig.json")));

        string? libraryDirectory = Environment.GetEnvironmentVariable("DOTNET_AOT_LIBRARY_DIR");
        Assert.IsNotNull(libraryDirectory, "The native test harness must select the candidate native CLI.");
        string libraryName = OperatingSystem.IsWindows() ? "dotnet-aot.dll"
            : OperatingSystem.IsMacOS() ? "libdotnet-aot.dylib" : "libdotnet-aot.so";
        Assert.IsTrue(File.Exists(Path.Combine(libraryDirectory, libraryName)));

        var environment = new Dictionary<string, string>
        {
            ["DOTNET_AOT_SDK_DIR"] = managedDirectory,
            ["DOTNET_AOT_BLANK_SDKDIR"] = "0",
            ["DOTNET_CLI_UI_LANGUAGE"] = language,
            ["DOTNET_NOLOGO"] = "1",
            ["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0",
        };
        var (exitCode, stdout, stderr) = RunDn(
            [command, "--help"],
            enableAot: enableAot,
            extraEnv: environment,
            workingDirectory: managedDirectory);

        Assert.AreEqual(0, exitCode, stderr);
        stderr.Should().BeEmpty();
        return stdout;
    }
}
