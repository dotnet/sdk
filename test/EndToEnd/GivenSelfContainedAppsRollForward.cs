using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.ProjectModel;
using NuGet.Versioning;
using Xunit;

namespace EndToEnd
{
    public partial class GivenSelfContainedAppsRollForward : TestBase
    {
        internal void ItRollsForwardToTheLatestVersion(string packageName, string minorVersion)
        {
            var testProjectCreator = new TestProjectCreator()
            {
                PackageName = packageName,
                MinorVersion = minorVersion,
                //  Set RuntimeIdentifier to opt in to roll-forward behavior
                RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier
            };

            var testInstance = testProjectCreator.Create();

            string projectDirectory = testInstance.Root.FullName;

            //  Get the version rolled forward to
            new RestoreCommand()
                    .WithWorkingDirectory(projectDirectory)
                    .Execute()
                    .Should().Pass();

            string assetsFilePath = Path.Combine(projectDirectory, "obj", "project.assets.json");
            var assetsFile = new LockFileFormat().Read(assetsFilePath);

            var rolledForwardVersion = GetPackageVersion(assetsFile, packageName);
            rolledForwardVersion.Should().NotBeNull();

            if (rolledForwardVersion.IsPrerelease)
            {
                //  If this version of .NET Core is still prerelease, then:
                //  - Floating the patch by adding ".*" to the major.minor version won't work, but
                //  - There aren't any patches to roll-forward to, so we skip testing this until the version
                //    leaves prerelease.
                return;
            }

            testProjectCreator.Identifier = "floating";

            var floatingProjectInstance = testProjectCreator.Create();

            var floatingProjectPath = Path.Combine(floatingProjectInstance.Root.FullName, "TestAppSimple.csproj");

            var floatingProject = XDocument.Load(floatingProjectPath);
            var ns = floatingProject.Root.Name.Namespace;


            if (packageName == TestProjectCreator.NETCorePackageName)
            {
                //  Float the RuntimeFrameworkVersion to get the latest version of the runtime available from feeds
                floatingProject.Root.Element(ns + "PropertyGroup")
                    .Add(new XElement(ns + "RuntimeFrameworkVersion", $"{minorVersion}.*"));
            }
            else
            {
                floatingProject.Root.Element(ns + "ItemGroup")
                    .Element(ns + "PackageReference")
                    .Add(new XAttribute("Version", $"{minorVersion}.*"),
                        new XAttribute("AllowExplicitVersion", "true"));
            }

            floatingProject.Save(floatingProjectPath);

            new RestoreCommand()
                    .WithWorkingDirectory(floatingProjectInstance.Root.FullName)
                    .Execute()
                    .Should().Pass();

            string floatingAssetsFilePath = Path.Combine(floatingProjectInstance.Root.FullName, "obj", "project.assets.json");

            var floatedAssetsFile = new LockFileFormat().Read(floatingAssetsFilePath);

            var floatedVersion = GetPackageVersion(floatedAssetsFile, packageName);
            floatedVersion.Should().NotBeNull();

            rolledForwardVersion.ToNormalizedString().Should().BeEquivalentTo(floatedVersion.ToNormalizedString(),
                $"the latest patch version for {packageName} {minorVersion} in Microsoft.NETCoreSdk.BundledVersions.props " +
                "needs to be updated (see the ImplicitPackageVariable items in MSBuildExtensions.targets in this repo)");
        }

        private static NuGetVersion GetPackageVersion(LockFile lockFile, string packageName) => lockFile?.Targets?.SingleOrDefault(t => t.RuntimeIdentifier != null)
                ?.Libraries?.SingleOrDefault(l =>
                    string.Compare(l.Name, packageName, StringComparison.CurrentCultureIgnoreCase) == 0)
                ?.Version;

        [Fact]
        public void WeCoverLatestNetCoreAppRollForward()
        {
            //  Run "dotnet new console", get TargetFramework property, and make sure it's covered in SupportedNetCoreAppVersions
            var directory = TestAssets.CreateTestDirectory();
            string projectDirectory = directory.FullName;

            new NewCommandShim()
                .WithWorkingDirectory(projectDirectory)
                .Execute("console --no-restore")
                .Should().Pass();

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
            var directory = TestAssets.CreateTestDirectory();
            string projectDirectory = directory.FullName;

            //  Run "dotnet new web", get TargetFramework property, and make sure it's covered in SupportedAspNetCoreAppVersions

            new NewCommandShim()
                .WithWorkingDirectory(projectDirectory)
                .Execute("web --no-restore")
                .Should().Pass();

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
