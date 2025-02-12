// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using EndToEnd.Tests.Utilities;

namespace EndToEnd.Tests
{
    public partial class GivenSelfContainedAppsRollForward(ITestOutputHelper log) : SdkTest(log)
    {
        [Fact]
        public void WeCoverLatestNetCoreAppRollForward()
        {
            //  Run "dotnet new console", get TargetFramework property, and make sure it's covered in SupportedNetCoreAppVersions
            var directory = _testAssetsManager.CreateTestDirectory();
            string projectDirectory = directory.Path;

            new DotnetNewCommand(Log, "web", "--no-restore")
                .WithVirtualHive()
                .WithWorkingDirectory(projectDirectory)
                .Execute().Should().Pass();

            string projectPath = Path.Combine(projectDirectory, Path.GetFileName(projectDirectory) + ".csproj");

            var project = XDocument.Load(projectPath);
            var ns = project.Root.Name.Namespace;

            string targetFramework = project.Root.Element(ns + "PropertyGroup")
                .Element(ns + "TargetFramework")
                .Value;

            SupportedNetCoreAppVersions.TargetFrameworkShortFolderVersion
                .Should().Contain(targetFramework, $"the {nameof(SupportedNetCoreAppVersions)}.{nameof(SupportedNetCoreAppVersions.Versions)} property should include the default version " +
                "of .NET Core created by \"dotnet new\"");
        }

        [Fact]
        public void WeCoverLatestAspNetCoreAppRollForward()
        {
            var directory = _testAssetsManager.CreateTestDirectory();
            string projectDirectory = directory.Path;

            //  Run "dotnet new web", get TargetFramework property, and make sure it's covered in SupportedAspNetCoreAppVersions

            new DotnetNewCommand(Log, "web", "--no-restore")
                .WithVirtualHive()
                .WithWorkingDirectory(projectDirectory)
                .Execute().Should().Pass();

            string projectPath = Path.Combine(projectDirectory, Path.GetFileName(projectDirectory) + ".csproj");

            var project = XDocument.Load(projectPath);
            var ns = project.Root.Name.Namespace;

            string targetFramework = project.Root.Element(ns + "PropertyGroup")
                .Element(ns + "TargetFramework")
                .Value;

            TargetFrameworkHelper.GetNetAppTargetFrameworks(SupportedAspNetCoreVersions.Versions)
                .Should().Contain(targetFramework, $"the {nameof(SupportedAspNetCoreVersions)} should include the default version " +
                "of Microsoft.AspNetCore.App used by the templates created by \"dotnet new web\"");
        }
    }
}
