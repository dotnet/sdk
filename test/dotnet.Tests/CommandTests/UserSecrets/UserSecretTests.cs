// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.UserSecrets.Tests;

public sealed class UserSecretTests(ITestOutputHelper log) : SdkTest(log)
{
    [Fact]
    public void Implicit()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "App.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var result = new DotnetCommand(Log, "msbuild", "-getProperty:UserSecretsId")
            .WithWorkingDirectory(testInstance.Path)
            .Execute();
        result.Should().Pass();
        result.StdOut.Should().MatchRegex("^[a-f0-9]{64}$"); // sha 256

        // Renaming the project should change the hash.
        File.Move(Path.Join(testInstance.Path, "App.csproj"), Path.Join(testInstance.Path, "Renamed.csproj"));

        var result2 = new DotnetCommand(Log, "msbuild", "-getProperty:UserSecretsId")
            .WithWorkingDirectory(testInstance.Path)
            .Execute();
        result2.Should().Pass();
        result2.StdOut.Should().MatchRegex("^[a-f0-9]{64}$"); // sha 256
        result2.StdOut.Should().NotBeEquivalentTo(result.StdOut, static o => o.IgnoringCase());
    }

    [Fact]
    public void Explicit()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "App.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                <UserSecretsId>Custom</UserSecretsId>
              </PropertyGroup>
            </Project>
            """);

        new DotnetCommand(Log, "msbuild", "-getProperty:UserSecretsId")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Custom");
    }

    [Fact]
    public void DirectoryBuildProps()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <UserSecretsId>Custom</UserSecretsId>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Join(testInstance.Path, "App.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        new DotnetCommand(Log, "msbuild", "-getProperty:UserSecretsId")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Custom");
    }
}
