// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Package.Remove.Tests
{
    public class GivenDotnetPackageRemove : SdkTest
    {
        public GivenDotnetPackageRemove(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void WhenPackageIsRemovedWithoutProjectArgument()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppSimple")
                .WithSource().Path;

            var packageName = "Newtonsoft.Json";
            var add = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("add", "package", packageName);
            add.Should().Pass();

            // Test the new 'dotnet package remove' command without specifying project
            var remove = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("package", "remove", packageName);

            remove.Should().Pass();
            remove.StdOut.Should().Contain($"Removing PackageReference for package '{packageName}' from project 'TestAppSimple.csproj'.");
            remove.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void WhenPackageIsRemovedWithProjectOption()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppSimple")
                .WithSource().Path;

            var packageName = "Newtonsoft.Json";
            var add = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("add", "package", packageName);
            add.Should().Pass();

            // Test the new 'dotnet package remove' command with --project option
            var remove = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("package", "remove", packageName, "--project", "TestAppSimple.csproj");

            remove.Should().Pass();
            remove.StdOut.Should().Contain($"Removing PackageReference for package '{packageName}' from project 'TestAppSimple.csproj'.");
            remove.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void WhenNoPackageIsPassedCommandFails()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppSimple")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
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

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("package", "remove", "package1", "package2")
                .Should()
                .Fail();
        }
    }
}