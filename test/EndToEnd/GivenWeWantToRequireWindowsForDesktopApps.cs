// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using EndToEnd;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tests.EndToEnd
{
    public class GivenWeWantToRequireWindowsForDesktopApps
    {
        [Fact]
        public void It_does_not_download_desktop_targeting_packs_on_unix()
        {
            var testProjectCreator = new TestProjectCreator()
            {
                MinorVersion = "5.0"
            };

            testProjectCreator.AdditionalProperties["RestorePackagesPath"] = @"$(MSBuildProjectDirectory)\packages";
            testProjectCreator.AdditionalProperties["OutputType"] = "exe";

            var testInstance = testProjectCreator.Create();

            new BuildCommand()
                    .WithWorkingDirectory(testInstance.Root.FullName)
                    .Execute()
                    .Should().Pass();

            string packagesPath = Path.Combine(testInstance.Root.FullName, "packages");
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

            var testInstance = testProjectCreator.Create();

            new PublishCommand()
                    .WithWorkingDirectory(testInstance.Root.FullName)
                    .Execute()
                    .Should().Pass();

            string packagesPath = Path.Combine(testInstance.Root.FullName, "packages", $"runtime.{Rid}.microsoft.windowsdesktop.app"); 
            Directory.Exists(packagesPath).Should().BeFalse(packagesPath + " should not exist");
        }
    }
}
