// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;

namespace Microsoft.DotNet.Cli.Package.Update.Tests;

public sealed class GivenDotnetPackageUpdate(ITestOutputHelper log) : SdkTest(log)
{
    [Fact]
    public void FileBasedApp()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();

        var packageName = "Newtonsoft.Json";
        var oldVersion = "12.0.1";

        var searchCommand = new DotnetCommand(Log, "package", "search", packageName, "--format", "json")
            .WithWorkingDirectory(testInstance.Path)
            .Execute();

        searchCommand.Should().Pass();

        var newVersion = JsonNode.Parse(searchCommand.StdOut!)!["searchResult"]!.AsArray()
            .First(r => r!["packages"]!.AsArray().Count != 0)!
            ["packages"]![0]!["latestVersion"]!.GetValue<string>();

        var file = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(file, $"""
            #:package {packageName}@{oldVersion}

            Console.WriteLine();
            """);

        new DotnetCommand(Log, "package", "update", packageName, "--project", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining($"Updating {packageName} {oldVersion} to {newVersion}");

        File.ReadAllText(file).Should().Be($"""
            #:package {packageName}@{newVersion}

            Console.WriteLine();
            """);
    }
}
