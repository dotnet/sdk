// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tests.BuildServerTests;

public sealed class UnifiedBuildServerTests(ITestOutputHelper output) : SdkTest(output)
{
    [Fact]
    public void Shutdown_Roslyn()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "app.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Join(testInstance.Path, "app.cs"), """
            Console.WriteLine();
            """);

        // Ensure there is no build server running from other tests.
        new DotnetCommand(Log, "build-server", "shutdown", "--unified")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.NotHaveStdOutContaining("VBCSCompiler")
            .And.NotHaveStdErr();

        // Build.
        new DotnetCommand(Log, "build")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("app.dll");

        // Shutdown the build server.
        new DotnetCommand(Log, "build-server", "shutdown", "--unified")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("VBCSCompiler")
            .And.NotHaveStdErr();
    }
}
