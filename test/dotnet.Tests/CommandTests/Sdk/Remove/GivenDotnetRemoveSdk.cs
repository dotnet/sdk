// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands;

namespace Microsoft.DotNet.Cli.Sdk.Remove.Tests;

public sealed class GivenDotnetRemoveSdk(ITestOutputHelper log) : SdkTest(log)
{
    [Fact]
    public void WhenReferencedSdkIsPassedItGetsRemoved()
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
            .Execute("remove", "sdk", sdkName)
            .Should().Pass()
            .And.HaveStdOutContaining($"Removing SDK reference '{sdkName}' from project '{projectFilePath}'");

        File.ReadAllText(projectFilePath).Should().NotContain(sdkName);
    }

    [Fact]
    public void WhenReferencedSdkIsPassedWithVersionSuffixItGetsRemoved()
    {
        const string testAsset = "TestAppSimple";
        var projectDirectory = TestAssetsManager
            .CopyTestAsset(testAsset)
            .WithSource()
            .Path;

        const string sdkName = "Cake.Sdk";
        const string sdkVersion = "6.2.0";
        var projectFilePath = Path.Combine(projectDirectory, $"{testAsset}.csproj");
        File.WriteAllText(projectFilePath, $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <Sdk Name="{sdkName}" Version="{sdkVersion}" />

              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
              </PropertyGroup>

            </Project>
            """);

        new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute("sdk", "remove", $"{sdkName}@{sdkVersion}")
            .Should().Pass()
            .And.HaveStdOutContaining($"Removing SDK reference '{sdkName}' from project '{projectFilePath}'");

        File.ReadAllText(projectFilePath).Should().NotContain(sdkName);
    }

    [Fact]
    public void WhenSdkInSemicolonDelimitedAttributeIsPassedItGetsRemoved()
    {
        const string testAsset = "TestAppSimple";
        var projectDirectory = TestAssetsManager
            .CopyTestAsset(testAsset)
            .WithSource()
            .Path;

        var projectFilePath = Path.Combine(projectDirectory, $"{testAsset}.csproj");
        File.WriteAllText(projectFilePath, $"""
            <Project Sdk="Microsoft.NET.Sdk;Aspire.AppHost.Sdk/9.1.0">

              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
              </PropertyGroup>

            </Project>
            """);

        new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute("sdk", "remove", "Aspire.AppHost.Sdk")
            .Should().Pass()
            .And.HaveStdOutContaining($"Removing SDK reference 'Aspire.AppHost.Sdk' from project '{projectFilePath}'");

        var contents = File.ReadAllText(projectFilePath);
        contents.Should().Contain("""<Project Sdk="Microsoft.NET.Sdk">""");
        contents.Should().NotContain("Aspire.AppHost.Sdk");
    }

    [Fact]
    public void WhenPrimarySdkIsPassedItFails()
    {
        const string testAsset = "TestAppSimple";
        var projectDirectory = TestAssetsManager
            .CopyTestAsset(testAsset)
            .WithSource()
            .Path;

        new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute("remove", "sdk", "Microsoft.NET.Sdk")
            .Should().Fail();
    }

    [Fact]
    public void WhenPrimaryRemovalFailsEarlierAdditiveRemovalIsNotPersisted()
    {
        const string testAsset = "TestAppSimple";
        var projectDirectory = TestAssetsManager
            .CopyTestAsset(testAsset)
            .WithSource()
            .Path;

        const string additiveSdk = "Cake.Sdk";
        var projectFilePath = Path.Combine(projectDirectory, $"{testAsset}.csproj");
        File.WriteAllText(projectFilePath, $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <Sdk Name="{additiveSdk}" Version="6.2.0" />

              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
              </PropertyGroup>

            </Project>
            """);

        new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute("remove", "sdk", additiveSdk, "Microsoft.NET.Sdk")
            .Should().Fail();

        File.ReadAllText(projectFilePath).Should().Contain(additiveSdk);
    }

    [Fact]
    public void WhenPrimaryTopLevelSdkElementIsPassedItFails()
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
            .Execute("sdk", "remove", "Microsoft.NET.Sdk")
            .Should().Fail();

        File.ReadAllText(projectFilePath).Should().Contain("""<Sdk Name="Microsoft.NET.Sdk" />""");
    }

    [Fact]
    public void FileBasedApp()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var file = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(file, """
            #:sdk Microsoft.NET.Sdk.Web
            #:sdk Cake.Sdk@6.2.0

            Console.WriteLine();
            """);

        new DotnetCommand(Log)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("remove", "sdk", "Cake.Sdk", "--file", "Program.cs")
            .Should().Pass()
            .And.HaveStdOut(string.Format(CliCommandStrings.DirectivesRemoved, "#:sdk", 1, "Cake.Sdk", file));

        File.ReadAllText(file).Should().Be("""
            #:sdk Microsoft.NET.Sdk.Web

            Console.WriteLine();
            """);
    }

    [Fact]
    public void FileBasedApp_PrimarySdkFails()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var file = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(file, """
            #:sdk Microsoft.NET.Sdk.Web
            #:sdk Cake.Sdk@6.2.0

            Console.WriteLine();
            """);

        new DotnetCommand(Log)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("remove", "sdk", "Microsoft.NET.Sdk.Web", "--file", "Program.cs")
            .Should().Fail();

        File.ReadAllText(file).Should().Contain("#:sdk Microsoft.NET.Sdk.Web");
        File.ReadAllText(file).Should().Contain("#:sdk Cake.Sdk@6.2.0");
    }

    [Fact]
    public void FileBasedApp_PrimaryRemovalDoesNotRemoveAdditive()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var file = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(file, """
            #:sdk Microsoft.NET.Sdk.Web
            #:sdk Cake.Sdk@6.2.0

            Console.WriteLine();
            """);

        new DotnetCommand(Log)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("remove", "sdk", "Cake.Sdk", "Microsoft.NET.Sdk.Web", "--file", "Program.cs")
            .Should().Fail();

        File.ReadAllText(file).Should().Contain("#:sdk Cake.Sdk@6.2.0");
    }

    [Fact]
    public void FileBasedApp_NotFound()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var file = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(file, """
            #:sdk Microsoft.NET.Sdk.Web

            Console.WriteLine();
            """);

        new DotnetCommand(Log)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("remove", "sdk", "Cake.Sdk", "--file", "Program.cs")
            .Should().Fail()
            .And.HaveStdOut(string.Format(CliCommandStrings.SdkReferenceNotFoundInFile, "Cake.Sdk", file));
    }

    [Fact]
    public void FileBasedApp_PrimaryValidatedBeforeAnyRemoval()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var file = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(file, """
            #:sdk Microsoft.NET.Sdk.Web
            #:sdk Cake.Sdk@6.2.0

            Console.WriteLine();
            """);

        new DotnetCommand(Log)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("remove", "sdk", "Microsoft.NET.Sdk.Web", "Cake.Sdk", "--file", "Program.cs")
            .Should().Fail();

        File.ReadAllText(file).Should().Contain("#:sdk Cake.Sdk@6.2.0");
    }

    [Fact]
    public void FileBasedApp_Multiple()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var file = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(file, """
            #:sdk Microsoft.NET.Sdk.Web
            #:sdk Cake.Sdk@6.2.0
            #:sdk Another.Sdk@1.0.0
            #:sdk Cake.Sdk@2.0.0

            Console.WriteLine();
            """);

        new DotnetCommand(Log)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("remove", "sdk", "Cake.Sdk", "--file", "Program.cs")
            .Should().Pass()
            .And.HaveStdOut(string.Format(CliCommandStrings.DirectivesRemoved, "#:sdk", 2, "Cake.Sdk", file));

        File.ReadAllText(file).Should().Be("""
            #:sdk Microsoft.NET.Sdk.Web
            #:sdk Another.Sdk@1.0.0

            Console.WriteLine();
            """);
    }
}
