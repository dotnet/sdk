// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using EndToEnd.Tests.Utilities;

namespace EndToEnd.Tests
{
    public class GivenWeWantToRequireWindowsForDesktopApps(ITestOutputHelper log) : SdkTest(log)
    {
        [PlatformSpecificFact(TestPlatforms.Linux | TestPlatforms.OSX | TestPlatforms.FreeBSD, Skip = "https://github.com/dotnet/sdk/issues/42230")]
        public void It_does_not_download_desktop_targeting_packs_on_unix()
        {
            var testProjectCreator = new TestProjectCreator()
            {
                MinorVersion = "9.0"
            };

            testProjectCreator.AdditionalProperties["RestorePackagesPath"] = @"$(MSBuildProjectDirectory)\packages";
            testProjectCreator.AdditionalProperties["OutputType"] = "exe";

            var testInstance = testProjectCreator.Create(_testAssetsManager);

            new DotnetBuildCommand(testInstance)
                .Execute().Should().Pass();

            string packagesPath = Path.Combine(testInstance.TestRoot, "packages");
            Directory.Exists(packagesPath).Should().BeFalse(packagesPath + " should not exist");
        }

        [PlatformSpecificFact(TestPlatforms.Linux | TestPlatforms.OSX | TestPlatforms.FreeBSD)]
        public void It_does_not_download_desktop_runtime_packs_on_unix()
        {
            const string Rid = "win-x64";

            var testProjectCreator = new TestProjectCreator()
            {
                MinorVersion = "3.1"
            };

            testProjectCreator.AdditionalProperties["RestorePackagesPath"] = @"$(MSBuildProjectDirectory)\packages";
            testProjectCreator.AdditionalProperties["OutputType"] = "exe";
            testProjectCreator.AdditionalProperties["RuntimeIdentifier"] = Rid;

            // At certain point of the release cycle LatestRuntimeFrameworkVersion in eng folder may not exist on the nuget feed
            static void overrideLastRuntimeFrameworkVersionToExistingOlderVersion(XDocument project)
            {
                XNamespace ns = project.Root.Name.Namespace;
                var target = XElement.Parse(@"  <ItemGroup>
    <KnownFrameworkReference Update=""@(KnownFrameworkReference)"">
      <LatestRuntimeFrameworkVersion Condition=""'%(TargetFramework)' == 'netcoreapp3.1'"">3.1.10</LatestRuntimeFrameworkVersion>
    </KnownFrameworkReference>

    <KnownAppHostPack Update=""@(KnownAppHostPack)"">
      <AppHostPackVersion Condition=""'%(TargetFramework)' == 'netcoreapp3.1'"">3.1.10</AppHostPackVersion>
    </KnownAppHostPack>
  </ItemGroup>");
                target.Name = ns + target.Name.LocalName;
                project.Root.Add(target);
            }
            var testInstance = testProjectCreator.Create(_testAssetsManager)
                .WithProjectChanges(overrideLastRuntimeFrameworkVersionToExistingOlderVersion);

            new PublishCommand(testInstance)
                .Execute().Should().Pass();

            string packagesPath = Path.Combine(testInstance.TestRoot, "packages", $"runtime.{Rid}.microsoft.windowsdesktop.app");
            Directory.Exists(packagesPath).Should().BeFalse(packagesPath + " should not exist");
        }
    }
}
