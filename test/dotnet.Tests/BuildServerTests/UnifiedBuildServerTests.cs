// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tests.BuildServerTests;

[CollectionDefinition(nameof(BuildServerTestCollection), DisableParallelization = true)]
public sealed class BuildServerTestCollection : ICollectionFixture<BuildServerTestCollection>;

[Collection(nameof(BuildServerTestCollection))]
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

        var roslynLog = Path.Join(testInstance.Path, "roslyn-log.txt");

        // Ensure there is no build server running from other tests.
        new DotnetCommand(Log, "build-server", "shutdown", "--unified")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.NotHaveStdErr();

        // Build.
        new DotnetCommand(Log, "build")
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("RoslynCommandLineLogFile", roslynLog)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("app.dll");

        // Shutdown the build server.
        var result = new DotnetCommand(Log, "build-server", "shutdown", "--unified")
            .WithWorkingDirectory(testInstance.Path)
            .Execute();

        Log.WriteLine(roslynLog);
        string roslynLogText = File.ReadAllText(roslynLog);
        Log.WriteLine(roslynLogText);

        result.Should().Pass()
            .And.HaveStdOutContaining("VBCSCompiler")
            .And.NotHaveStdErr();
    }
}
