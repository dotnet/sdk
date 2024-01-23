// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.ProjectModel;
using NuGet.Versioning;
using Xunit;

namespace EndToEnd
{
    public class GivenFrameworkDependentApps : TestBase
    {
        [Theory]
        [ClassData(typeof(SupportedNetCoreAppVersions))]
        public void ItDoesNotRollForwardToTheLatestVersionOfNetCore(string minorVersion)
        {
            if (minorVersion == "3.0" || minorVersion == "3.1" || minorVersion == "5.0" || minorVersion == "6.0" || minorVersion == "7.0" || minorVersion == "8.0" || minorVersion == "9.0")
            {
                //  https://github.com/dotnet/core-sdk/issues/621
                return;
            }
            ItDoesNotRollForwardToTheLatestVersion(TestProjectCreator.NETCorePackageName, minorVersion);
        }

        [Theory]
        [ClassData(typeof(SupportedAspNetCoreVersions))]
        public void ItDoesNotRollForwardToTheLatestVersionOfAspNetCoreApp(string minorVersion)
        {
            if (minorVersion == "3.0" || minorVersion == "3.1" || minorVersion == "5.0" || minorVersion == "6.0" || minorVersion == "7.0" || minorVersion == "8.0" || minorVersion == "9.0")
            {
                //  https://github.com/dotnet/core-sdk/issues/621
                return;
            }
            ItDoesNotRollForwardToTheLatestVersion(TestProjectCreator.AspNetCoreAppPackageName, minorVersion);
        }

        [Theory]
        [ClassData(typeof(SupportedAspNetCoreAllVersions))]
        public void ItDoesNotRollForwardToTheLatestVersionOfAspNetCoreAll(string minorVersion)
        {
            ItDoesNotRollForwardToTheLatestVersion(TestProjectCreator.AspNetCoreAllPackageName, minorVersion);
        }

        internal void ItDoesNotRollForwardToTheLatestVersion(string packageName, string minorVersion)
        {
            // https://github.com/NuGet/Home/issues/8571
            #if LINUX_PORTABLE
                return;
            #else
                var testProjectCreator = new TestProjectCreator()
                {
                    PackageName = packageName,
                    MinorVersion = minorVersion,
                };

                var _testInstance = testProjectCreator.Create();

                string projectDirectory = _testInstance.Root.FullName;

                string projectPath = Path.Combine(projectDirectory, "TestAppSimple.csproj");

                //  Get the resolved version of .NET Core
                new RestoreCommand()
                        .WithWorkingDirectory(projectDirectory)
                        .Execute()
                        .Should().Pass();

                string assetsFilePath = Path.Combine(projectDirectory, "obj", "project.assets.json");
                var assetsFile = new LockFileFormat().Read(assetsFilePath);

                var versionInAssertsJson = GetPackageVersion(assetsFile, packageName);
                versionInAssertsJson.Should().NotBeNull();

                if (versionInAssertsJson.IsPrerelease && versionInAssertsJson.Patch == 0)
                {
                    // if the bundled version is, for example, a prerelease of
                    // .NET Core 2.1.1, that we don't roll forward to that prerelease
                    // version for framework-dependent deployments.
                    return;
                }

                versionInAssertsJson.ToNormalizedString().Should().BeEquivalentTo(GetExpectedVersion(packageName, minorVersion));
            #endif
        }

        private static NuGetVersion GetPackageVersion(LockFile lockFile, string packageName)
        {
            return lockFile?.Targets?.SingleOrDefault(t => t.RuntimeIdentifier == null)
                ?.Libraries?.SingleOrDefault(l =>
                    string.Compare(l.Name, packageName, StringComparison.CurrentCultureIgnoreCase) == 0)
                ?.Version;
        }

        public string GetExpectedVersion(string packageName, string minorVersion)
        {
            if (minorVersion.StartsWith("1.0"))
            {
                return "1.0.5";  // special case for 1.0
            }
            else if (minorVersion.StartsWith("1.1"))
            {
                return "1.1.2";  // special case for 1.1
            }
            else
            {
                //  ASP.NET 2.1.0 packages had exact version dependencies, which was problematic,
                //  so the default version for 2.1 apps is 2.1.1.
                if (packageName == TestProjectCreator.AspNetCoreAppPackageName ||
                    packageName == TestProjectCreator.AspNetCoreAllPackageName)
                {
                    if (minorVersion == "2.1")
                    {
                        return "2.1.1";
                    }
                }
                var parsed = NuGetVersion.Parse(minorVersion);
                return new NuGetVersion(parsed.Major, parsed.Minor, 0).ToNormalizedString();
            }
        }
    }
}
