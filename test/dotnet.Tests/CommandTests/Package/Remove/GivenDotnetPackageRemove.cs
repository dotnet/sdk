
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Package.Remove.Tests;

public sealed class GivenDotnetPackageRemove(ITestOutputHelper log) : SdkTest(log)
{
    [Fact]
    public void WhenPackageIsRemovedWithoutProjectArgument()
    {
        var projectDirectory = _testAssetsManager
            .CopyTestAsset("TestAppSimple")
            .WithSource().Path;

        const string packageName = "Newtonsoft.Json";
        new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute("add", "package", packageName)
            .Should().Pass();

        // Test the new 'dotnet package remove' command without specifying project
        new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute("package", "remove", packageName)
            .Should().Pass()
            .And.HaveStdOutContaining($"Removing PackageReference for package '{packageName}' from project '{projectDirectory + Path.DirectorySeparatorChar}TestAppSimple.csproj'.")
            .And.NotHaveStdErr();
    }

    [Fact]
    public void WhenPackageIsRemovedWithProjectOption()
    {
        var projectDirectory = _testAssetsManager
            .CopyTestAsset("TestAppSimple")
            .WithSource().Path;

        const string packageName = "Newtonsoft.Json";
        new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute("add", "package", packageName)
            .Should().Pass();

        // Test the new 'dotnet package remove' command with --project option
        new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute("package", "remove", packageName, "--project", "TestAppSimple.csproj")
            .Should().Pass()
            .And.HaveStdOutContaining($"Removing PackageReference for package '{packageName}' from project 'TestAppSimple.csproj'.")
            .And.NotHaveStdErr();
    }

    [Fact]
    public void WhenNoPackageIsPassedCommandFails()
    {
        var projectDirectory = _testAssetsManager
            .CopyTestAsset("TestAppSimple")
            .WithSource()
            .Path;

        new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute("package", "remove")
            .Should()
            .Fail();
    }

    [Fact]
    public void WhenMultiplePackagesArePassedCommandFails()
    {
        var projectDirectory = _testAssetsManager
            .CopyTestAsset("TestAppSimple")
            .WithSource()
            .Path;

        new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute("package", "remove", "package1", "package2")
            .Should()
            .Fail();
    }
}
