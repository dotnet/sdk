// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace EndToEnd.Tests
{
    public class GivenWindowsApp(ITestOutputHelper log) : SdkTest(log)
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
            var testInstance = _testAssetsManager
                .CopyTestAsset("UseCswinrt", identifier: targetPlatformVersion)
                .WithSource();

            var projectPath = Path.Combine(testInstance.Path, "consolecswinrt.csproj");
            var project = XDocument.Load(projectPath);
            var ns = project.Root.Name.Namespace;

            //  Update TargetFramework to the right version of .NET Core
            project.Root.Element(ns + "PropertyGroup")
                .Add(new XElement(ns + "TargetPlatformVersion", targetPlatformVersion));
            project.Root.Element(ns + "PropertyGroup")
                .Element(ns + "TargetFramework").Value = "net9.0";
            project.Save(projectPath);

            new BuildCommand(Log, testInstance.TestRoot)
                .Execute().Should().Pass();

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testInstance.Path)
                .Execute("--no-build").Should().Pass().And.HaveStdOutContaining("Hello");
        }
    }
}
