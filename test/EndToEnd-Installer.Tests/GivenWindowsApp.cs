﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.TestFramework;
using WindowsOnlyTheoryAttribute = Microsoft.DotNet.Tools.Test.Utilities.WindowsOnlyTheoryAttribute;
using BuildCommand = Microsoft.DotNet.Tools.Test.Utilities.BuildCommand;
using RunCommand = Microsoft.DotNet.Tools.Test.Utilities.RunCommand;
using TestBase = Microsoft.DotNet.Tools.Test.Utilities.TestBase;
using static Microsoft.DotNet.Tools.Test.Utilities.TestCommandExtensions;

namespace EndToEnd
{
    public class GivenWindowsApp : TestBase
    {
        [WindowsOnlyTheory]
        [InlineData("10.0.17763.0")]
        [InlineData("10.0.18362.0")]
        [InlineData("10.0.19041.0")]
        [InlineData("10.0.20348.0")]
        [InlineData("10.0.22000.0")]
        [InlineData("10.0.22621.0")]
        public void ItCanBuildAndRun(string targetPlatformVersion)
        {
            var testInstance = TestAssets.Get(TestAssetKinds.TestProjects, "UseCswinrt")
                .CreateInstance("UseCswinrt" + targetPlatformVersion)
                .WithSourceFiles();

            var projectPath = Path.Combine(testInstance.Root.FullName, "consolecswinrt.csproj");
            var project = XDocument.Load(projectPath);
            var ns = project.Root.Name.Namespace;

            //  Update TargetFramework to the right version of .NET Core
            project.Root.Element(ns + "PropertyGroup")
                .Add(new XElement(ns + "TargetPlatformVersion", targetPlatformVersion));
            project.Root.Element(ns + "PropertyGroup")
                .Element(ns + "TargetFramework").Value = TestAssetInfo.currentTfm;
            project.Save(projectPath);

            new BuildCommand()
                    .WithProjectFile(new FileInfo(testInstance.Root.FullName))
                    .Execute().Should().Pass();

            new RunCommand()
                .WithWorkingDirectory(testInstance.Root.FullName)
                .Execute("--no-build").Should().Pass().And.HaveStdOutContaining("Hello");
        }
    }
}
