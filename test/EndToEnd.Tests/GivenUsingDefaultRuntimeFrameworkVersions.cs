// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using EndToEnd.Tests.Utilities;
using NuGet.Versioning;

namespace EndToEnd.Tests
{
    public partial class GivenUsingDefaultRuntimeFrameworkVersions(ITestOutputHelper log) : SdkTest(log)
    {
        private static readonly IEnumerable<string> frameworks = new string[] {"Microsoft.NETCore.App", "Microsoft.WindowsDesktop.App",
            "Microsoft.WindowsDesktop.App.WPF", "Microsoft.WindowsDesktop.App.WindowsForms", "Microsoft.AspNetCore.App" };

        private static readonly IEnumerable<string> versions = SupportedNetCoreAppVersions.Versions.Where(version => NuGetVersion.Parse(version).Major >= 3);

        [Fact]
        public void DefaultRuntimeVersionsAreUpToDate()
        {
            var outputFile = "resolvedVersions.txt";
            var testProjectCreator = new TestProjectCreator()
            {
                PackageName = "DefaultRuntimeVersionsAreUpToDate",
                MinorVersion = "3.0"
            };
            var testProject = testProjectCreator.Create(_testAssetsManager);

            var projectFile = new DirectoryInfo(testProject.TestRoot).GetFiles("*.csproj").First().FullName;
            var project = XDocument.Load(projectFile);
            string writeResolvedVersionsTarget = @$"
    <Target Name=`WriteResolvedVersions` AfterTargets=`PrepareForBuild;ProcessFrameworkReferences`>
        <ItemGroup>
            <LinesToWrite Include=`%(KnownFrameworkReference.Identity) %(KnownFrameworkReference.DefaultRuntimeFrameworkVersion) %(KnownFrameworkReference.LatestRuntimeFrameworkVersion)`/>
        </ItemGroup>
        <WriteLinesToFile File=`$(OutputPath){ outputFile }`
                          Lines=`@(LinesToWrite)`
                          Overwrite=`true`
                          Encoding=`Unicode`/>

      </Target>";
            writeResolvedVersionsTarget = writeResolvedVersionsTarget.Replace('`', '"');
            var targetElement = XElement.Parse(writeResolvedVersionsTarget);
            var ns = project.Root.Name.Namespace;
            foreach (var elem in targetElement.Descendants())
                elem.Name = ns + elem.Name.LocalName;
            targetElement.Name = ns + targetElement.Name.LocalName;
            project.Root.Add(targetElement);
            using (var file = File.CreateText(projectFile))
            {
                project.Save(file);
            }

            new RestoreCommand(testProject)
                .Execute().Should().Pass();

            var binDirectory = new DirectoryInfo(testProject.TestRoot).Sub("bin").Sub("Debug").GetDirectories().FirstOrDefault();
            binDirectory.Should().HaveFilesMatching(outputFile, SearchOption.TopDirectoryOnly);
            var resolvedVersionsFile = File.ReadAllLines(Path.Combine(binDirectory.FullName, outputFile));
            foreach (var framework in frameworks)
            {
                foreach (var version in versions)
                {
                    var frameworkVersionLine = resolvedVersionsFile.Where(line => line.Contains(framework) && line.Contains(version)).FirstOrDefault();
                    if (!string.IsNullOrEmpty(frameworkVersionLine))
                    {
                        var defaultVersion = NuGetVersion.Parse(frameworkVersionLine.Split(" ")[1]);
                        var latestVersion = NuGetVersion.Parse(frameworkVersionLine.Split(" ")[2]);

                        if (latestVersion.Patch == 0 && latestVersion.IsPrerelease)
                        {
                            defaultVersion.Should().Be(latestVersion,
                                $"the DefaultRuntimeFrameworkVersion for { framework } { version } in Microsoft.NETCoreSdk.BundledVersions.props does not match latest prerelease version { latestVersion }");
                        }
                        else
                        {
                            defaultVersion.Should().Be(new NuGetVersion(latestVersion.Major, latestVersion.Minor, 0),
                                $"the DefaultRuntimeFrameworkVersion for { framework } { version } in Microsoft.NETCoreSdk.BundledVersions.props needs to be updated to { version }.0");
                        }
                    }
                }
            }
        }
    }
}
