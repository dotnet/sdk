// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands;

namespace Microsoft.DotNet.Cli.Sdk.Add.Tests;

public sealed class GivenDotnetAddSdk(ITestOutputHelper log) : SdkTest(log)
{
    [Fact]
    public void WhenValidSdkIsPassedItGetsAddedToProject()
    {
        const string testAsset = "TestAppSimple";
        var projectDirectory = TestAssetsManager
            .CopyTestAsset(testAsset)
            .WithSource()
            .Path;

        const string sdkName = "Cake.Sdk";
        const string sdkVersion = "6.2.0";
        var projectFilePath = Path.Combine(projectDirectory, $"{testAsset}.csproj");

        new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute("add", "sdk", sdkName, "--version", sdkVersion, "--no-restore")
            .Should().Pass()
            .And.HaveStdOutContaining($"SDK reference '{sdkName}' version '{sdkVersion}' added to project '{projectFilePath}'");

        File.ReadAllText(projectFilePath).Should().Contain($"""<Sdk Name="{sdkName}" Version="{sdkVersion}" />""");
    }

    [Fact]
    public void WhenValidSdkIsPassedWithVersionSuffixItGetsAddedToProject()
    {
        const string testAsset = "TestAppSimple";
        var projectDirectory = TestAssetsManager
            .CopyTestAsset(testAsset)
            .WithSource()
            .Path;

        const string sdkName = "Cake.Sdk";
        const string sdkVersion = "6.2.0";
        var projectFilePath = Path.Combine(projectDirectory, $"{testAsset}.csproj");

        new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute("add", "sdk", $"{sdkName}@{sdkVersion}", "--no-restore")
            .Should().Pass()
            .And.HaveStdOutContaining($"SDK reference '{sdkName}' version '{sdkVersion}' added to project '{projectFilePath}'");

        File.ReadAllText(projectFilePath).Should().Contain($"""<Sdk Name="{sdkName}" Version="{sdkVersion}" />""");
    }

    [Fact]
    public void WhenSdkInSemicolonDelimitedAttributeIsUpdatedOtherSdksArePreserved()
    {
        const string testAsset = "TestAppSimple";
        var projectDirectory = TestAssetsManager
            .CopyTestAsset(testAsset)
            .WithSource()
            .Path;

        var projectFilePath = Path.Combine(projectDirectory, $"{testAsset}.csproj");
        File.WriteAllText(projectFilePath, $"""
            <Project Sdk="Microsoft.NET.Sdk/9.0.100;Aspire.AppHost.Sdk/9.1.0">

              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
              </PropertyGroup>

            </Project>
            """);

        new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute("sdk", "add", "Microsoft.NET.Sdk@9.0.200", "--no-restore")
            .Should().Pass()
            .And.HaveStdOutContaining("SDK reference 'Microsoft.NET.Sdk' version '9.0.200' updated");

        File.ReadAllText(projectFilePath).Should().Contain("Microsoft.NET.Sdk/9.0.200;Aspire.AppHost.Sdk/9.1.0");
    }

    [Fact]
    public void WhenProjectUsesTopLevelPrimarySdkNewSdkIsInsertedAfterIt()
    {
        const string testAsset = "TestAppSimple";
        var projectDirectory = TestAssetsManager
            .CopyTestAsset(testAsset)
            .WithSource()
            .Path;

        var projectFilePath = Path.Combine(projectDirectory, $"{testAsset}.csproj");
        File.WriteAllText(projectFilePath, $"""
            <Project>

              <Sdk Name="Microsoft.NET.Sdk" />

              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
              </PropertyGroup>

            </Project>
            """);

        new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute("sdk", "add", "Cake.Sdk@6.2.0", "--no-restore")
            .Should().Pass();

        var contents = File.ReadAllText(projectFilePath);
        contents.Should().Contain($"""<Sdk Name="Cake.Sdk" Version="6.2.0" />""");
        contents.IndexOf("Microsoft.NET.Sdk", StringComparison.Ordinal)
            .Should().BeLessThan(contents.IndexOf("Cake.Sdk", StringComparison.Ordinal));
    }

    [Fact]
    public void WhenExistingSdkIsPassedWithoutVersionPinnedVersionIsPreserved()
    {
        const string testAsset = "TestAppSimple";
        var projectDirectory = TestAssetsManager
            .CopyTestAsset(testAsset)
            .WithSource()
            .Path;

        const string sdkName = "Cake.Sdk";
        var projectFilePath = Path.Combine(projectDirectory, $"{testAsset}.csproj");
        File.WriteAllText(projectFilePath, $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <Sdk Name="{sdkName}" Version="6.2.0" />

              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
              </PropertyGroup>

            </Project>
            """);

        new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute("sdk", "add", sdkName, "--no-restore")
            .Should().Pass()
            .And.HaveStdOutContaining($"SDK reference '{sdkName}' is already present");

        File.ReadAllText(projectFilePath).Should().Contain($"""<Sdk Name="{sdkName}" Version="6.2.0" />""");
    }

    [Fact]
    public void WhenGlobalJsonSpecifiesSdkVersionProjectOmitsVersion()
    {
        const string testAsset = "TestAppSimple";
        var projectDirectory = TestAssetsManager
            .CopyTestAsset(testAsset)
            .WithSource()
            .Path;

        File.WriteAllText(Path.Combine(projectDirectory, "global.json"), """
            {
              "msbuild-sdks": {
                "Cake.Sdk": "6.2.0"
              }
            }
            """);

        var projectFilePath = Path.Combine(projectDirectory, $"{testAsset}.csproj");
        new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute("sdk", "add", "Cake.Sdk", "--no-restore")
            .Should().Pass();

        File.ReadAllText(projectFilePath).Should().Contain("""<Sdk Name="Cake.Sdk" />""");
    }

    [Fact]
    public void FileBasedApp_WithoutVersionPreservesPinnedVersion()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var file = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(file, """
            #:sdk Cake.Sdk@6.2.0
            Console.WriteLine();
            """);

        new DotnetCommand(Log)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("add", "sdk", "Cake.Sdk", "--file", "Program.cs", "--no-restore")
            .Should().Pass()
            .And.HaveStdOutContaining("SDK reference 'Cake.Sdk' is already present");

        File.ReadAllText(file).Should().Be("""
            #:sdk Cake.Sdk@6.2.0
            Console.WriteLine();
            """);
    }

    [Fact]
    public void WhenExistingAdditiveSdkIsPassedItGetsUpdated()
    {
        const string testAsset = "TestAppSimple";
        var projectDirectory = TestAssetsManager
            .CopyTestAsset(testAsset)
            .WithSource()
            .Path;

        const string sdkName = "Cake.Sdk";
        var projectFilePath = Path.Combine(projectDirectory, $"{testAsset}.csproj");
        File.WriteAllText(projectFilePath, $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <Sdk Name="{sdkName}" Version="1.0.0" />

              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
              </PropertyGroup>

            </Project>
            """);

        new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute("add", "sdk", $"{sdkName}@6.2.0", "--no-restore")
            .Should().Pass()
            .And.HaveStdOutContaining($"SDK reference '{sdkName}' version '6.2.0' updated in project '{projectFilePath}'");

        File.ReadAllText(projectFilePath).Should().Contain($"""<Sdk Name="{sdkName}" Version="6.2.0" />""");
    }

    [Fact]
    public void FileBasedApp()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var file = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(file, """
            Console.WriteLine();
            """);

        new DotnetCommand(Log)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("add", "sdk", "Cake.Sdk@6.2.0", "--file", "Program.cs", "--no-restore")
            .Should().Pass()
            .And.HaveStdOutContaining("SDK reference 'Cake.Sdk' version '6.2.0' added to file");

        File.ReadAllText(file).Should().Be("""
            #:sdk Cake.Sdk@6.2.0

            Console.WriteLine();
            """);
    }

    [Fact]
    public void FileBasedApp_AdditiveSdkPreservesPrimary()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var file = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(file, """
            #:sdk Microsoft.NET.Sdk.Web
            Console.WriteLine();
            """);

        new DotnetCommand(Log)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("add", "sdk", "Cake.Sdk@6.2.0", "--file", "Program.cs", "--no-restore")
            .Should().Pass()
            .And.HaveStdOutContaining("SDK reference 'Cake.Sdk' version '6.2.0' added to file");

        File.ReadAllText(file).Should().Be("""
            #:sdk Microsoft.NET.Sdk.Web
            #:sdk Cake.Sdk@6.2.0
            Console.WriteLine();
            """);
    }

    [Fact]
    public void WhenBundledSdkIsAddedWithoutVersionVersionIsOmitted()
    {
        const string testAsset = "TestAppSimple";
        var projectDirectory = TestAssetsManager
            .CopyTestAsset(testAsset)
            .WithSource()
            .Path;

        const string sdkName = "Microsoft.NET.Sdk.Web";
        var projectFilePath = Path.Combine(projectDirectory, $"{testAsset}.csproj");

        new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute("sdk", "add", sdkName, "--no-restore")
            .Should().Pass()
            .And.HaveStdOutContaining($"SDK reference '{sdkName}' added to project");

        File.ReadAllText(projectFilePath).Should().Contain($"""<Sdk Name="{sdkName}" />""");
    }

    [Fact]
    public void WhenGlobalJsonIsMalformedAddingNewSdkFailsGracefully()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var appDirectory = Path.Join(testInstance.Path, "app");
        Directory.CreateDirectory(appDirectory);
        File.WriteAllText(Path.Combine(appDirectory, "global.json"), "{ invalid json");

        var file = Path.Join(appDirectory, "Program.cs");
        File.WriteAllText(file, """
            Console.WriteLine();
            """);

        new DotnetCommand(Log)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("add", "sdk", "Cake.Sdk", "--file", "app/Program.cs", "--no-restore")
            .Should().Fail()
            .And.HaveStdErrContaining("Could not parse global.json file");
    }

    [Fact]
    public void WhenExistingSdkIsReAddedWithoutVersionMalformedGlobalJsonIsIgnored()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var appDirectory = Path.Join(testInstance.Path, "app");
        Directory.CreateDirectory(appDirectory);
        File.WriteAllText(Path.Combine(appDirectory, "global.json"), "{ invalid json");

        var file = Path.Join(appDirectory, "Program.cs");
        File.WriteAllText(file, """
            #:sdk Cake.Sdk@6.2.0
            Console.WriteLine();
            """);

        new DotnetCommand(Log)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("add", "sdk", "Cake.Sdk", "--file", "app/Program.cs", "--no-restore")
            .Should().Pass()
            .And.HaveStdOutContaining("SDK reference 'Cake.Sdk' is already present");

        File.ReadAllText(file).Should().Be("""
            #:sdk Cake.Sdk@6.2.0
            Console.WriteLine();
            """);
    }

    [Fact]
    public void FileBasedApp_ReplaceExisting()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var file = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(file, """
            #:sdk Cake.Sdk@1.0.0
            Console.WriteLine();
            """);

        new DotnetCommand(Log)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("add", "sdk", "Cake.Sdk@6.2.0", "--file", "Program.cs", "--no-restore")
            .Should().Pass();

        File.ReadAllText(file).Should().Be("""
            #:sdk Cake.Sdk@6.2.0
            Console.WriteLine();
            """);
    }

    [Fact]
    public void WhenSdkIdentityHasEmptyVersionSuffixItFails()
    {
        new DotnetCommand(Log)
            .Execute("sdk", "add", "Cake.Sdk@")
            .Should().Fail()
            .And.HaveStdErrContaining("SDK version must not be empty");
    }

    [Fact]
    public void WhenRestoreFailsProjectFileEncodingIsPreserved()
    {
        const string testAsset = "TestAppSimple";
        var projectDirectory = TestAssetsManager
            .CopyTestAsset(testAsset)
            .WithSource()
            .Path;

        var projectFilePath = Path.Combine(projectDirectory, $"{testAsset}.csproj");
        var projectContents = File.ReadAllText(projectFilePath);
        File.WriteAllText(projectFilePath, projectContents, Encoding.Unicode);
        var originalBytes = File.ReadAllBytes(projectFilePath);

        new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute("sdk", "add", "Cake.Sdk@99999.0.0")
            .Should().Fail();

        File.ReadAllBytes(projectFilePath).Should().Equal(originalBytes);
        File.ReadAllText(projectFilePath, Encoding.Unicode).Should().NotContain("Cake.Sdk");
    }
}
