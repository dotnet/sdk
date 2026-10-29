// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

namespace Microsoft.DotNet.Cli.New.IntegrationTests;

[TestClass]
[ResourceLock(nameof(DotnetBuildCommand))]
public sealed class BundledWebTemplatesTests : BaseIntegrationTest
{
    [TestMethod]
    [CombinatorialData]
    public void BundledWebProjectsTargetCurrentFrameworkAndBuild(
        [CombinatorialValues("blazor", "blazorwasm", "worker", "web", "webapi", "mvc", "webapp", "razorclasslib", "grpc", "mcpserver")]
        string template,
        [CombinatorialValues(false, true)] bool explicitFramework)
    {
        string workingDirectory = CreateTemporaryFolder(folderName: $"{template}-{explicitFramework}");
        string home = Path.Combine(workingDirectory, "home");
        string projectDirectory = Path.Combine(workingDirectory, "project");
        string framework = ToolsetInfo.SdkTargetFramework;
        string[] frameworkArguments = explicitFramework ? ["--framework", framework] : [];

        // A fresh hive, without installed packages, must select the templates shipped in the SDK.
        new DotnetNewCommand(Log, [template, "--name", "TemplateApp", "--output", projectDirectory, "--no-restore", .. frameworkArguments])
            .WithCustomHive(home)
            .WithWorkingDirectory(workingDirectory)
            .Execute()
            .Should()
            .Pass()
            .And.NotHaveStdErr();

        string projectFile = Path.Combine(projectDirectory, "TemplateApp.csproj");
        XDocument.Load(projectFile).Descendants("TargetFramework").Select(element => element.Value)
            .Should().Equal(framework);

        new DotnetRestoreCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute()
            .Should()
            .Pass()
            .And.NotHaveStdErr();

        new DotnetBuildCommand(Log, "--no-restore")
            .WithWorkingDirectory(projectDirectory)
            .Execute()
            .Should()
            .Pass()
            .And.NotHaveStdErr();
    }

    [TestMethod]
    [DataRow("razorcomponent", "Item.razor")]
    [DataRow("proto", "Item.proto")]
    public void BundledWebItemsCanBeCreatedInACleanHive(string template, string fileName)
    {
        string workingDirectory = CreateTemporaryFolder(folderName: template);
        new DotnetNewCommand(Log, template, "--name", "Item")
            .WithCustomHive(Path.Combine(workingDirectory, "home"))
            .WithWorkingDirectory(workingDirectory)
            .Execute()
            .Should()
            .Pass()
            .And.NotHaveStdErr();

        File.Exists(Path.Combine(workingDirectory, fileName)).Should().BeTrue();
    }
}
