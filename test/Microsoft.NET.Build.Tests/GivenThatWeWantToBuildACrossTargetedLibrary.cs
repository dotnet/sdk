// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Runtime.InteropServices;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;
using System.Xml.Linq;
using System.Linq;
using FluentAssertions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildACrossTargetedLibrary : SdkTest
    {
        [Fact]
        public void It_builds_nondesktop_library_successfully_on_all_platforms()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("CrossTargeting")
                .WithSource()
                .Restore("NetStandardAndNetCoreApp");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "NetStandardAndNetCoreApp");

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework: "");
            outputDirectory.Should().OnlyHaveFiles(new[] {
                "netcoreapp1.1/NetStandardAndNetCoreApp.dll",
                "netcoreapp1.1/NetStandardAndNetCoreApp.pdb",
                "netcoreapp1.1/NetStandardAndNetCoreApp.runtimeconfig.json",
                "netcoreapp1.1/NetStandardAndNetCoreApp.runtimeconfig.dev.json",
                "netcoreapp1.1/NetStandardAndNetCoreApp.deps.json",
                "netstandard1.5/NetStandardAndNetCoreApp.dll",
                "netstandard1.5/NetStandardAndNetCoreApp.pdb",
                "netstandard1.5/NetStandardAndNetCoreApp.deps.json"
            });
        }

        [Fact]
        public void It_builds_desktop_library_successfully_on_windows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testAsset = _testAssetsManager
                .CopyTestAsset("CrossTargeting")
                .WithSource()
                .Restore("DesktopAndNetStandard");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "DesktopAndNetStandard");

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework: "");
            outputDirectory.Should().OnlyHaveFiles(new[] {
                "net40/DesktopAndNetStandard.dll",
                "net40/DesktopAndNetStandard.pdb",
                "net40/Newtonsoft.Json.dll",
                "net40-client/DesktopAndNetStandard.dll",
                "net40-client/DesktopAndNetStandard.pdb",
                "net40-client/Newtonsoft.Json.dll",
                "net45/DesktopAndNetStandard.dll",
                "net45/DesktopAndNetStandard.pdb",
                "net45/Newtonsoft.Json.dll",
                "netstandard1.5/DesktopAndNetStandard.dll",
                "netstandard1.5/DesktopAndNetStandard.pdb",
                "netstandard1.5/DesktopAndNetStandard.deps.json"
            });
        }

        [Fact]
        public void It_builds_all_targets_library_successfully_on_windows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testAsset = _testAssetsManager
                .CopyTestAsset("CrossTargeting")
                .WithSource()
                .Restore("AllSupported");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "AllSupported");

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework: "");
            outputDirectory.Should().HaveFiles(new[] {

                "monoandroid/AllSupported.dll",
                "monoandroid/AllSupported.pdb",

                "net40/AllSupported.dll",
                "net40/AllSupported.pdb",
                "net40/Newtonsoft.Json.dll",

                "net40-client/AllSupported.dll",
                "net40-client/AllSupported.pdb",
                "net40-client/Newtonsoft.Json.dll",

                "net45/AllSupported.dll",
                "net45/AllSupported.pdb",
                "net45/Newtonsoft.Json.dll",

                "netstandard1.5/AllSupported.dll",
                "netstandard1.5/AllSupported.pdb",
                "netstandard1.5/AllSupported.deps.json",

                "portable-net451+wpa81+win81/AllSupported.dll",
                "portable-net451+wpa81+win81/AllSupported.pdb",
                "portable-net451+wpa81+win81/Newtonsoft.Json.dll",

                "portable-net4+sl50+win8+wpa81+wp8/AllSupported.dll",
                "portable-net4+sl50+win8+wpa81+wp8/AllSupported.pdb",
                "portable-net4+sl50+win8+wpa81+wp8/Newtonsoft.Json.dll",

                "portable-net45+win8+wp8+wpa81/AllSupported.dll",
                "portable-net45+win8+wp8+wpa81/AllSupported.pdb",
                "portable-net45+win8+wp8+wpa81/Newtonsoft.Json.dll",

                "portable-win81+wpa81/AllSupported.dll",
                "portable-win81+wpa81/AllSupported.pri",
                "portable-win81+wpa81/AllSupported.pdb",
                "portable-win81+wpa81/Newtonsoft.Json.dll",

                "sl5/AllSupported.dll",
                "sl5/AllSupported.pdb",
                "sl5/Newtonsoft.Json.dll",

                "win8/AllSupported.dll",
                "win8/AllSupported.pdb",
                "win8/AllSupported.pri",
                "win8/Newtonsoft.Json.dll",

                "win81/AllSupported.dll",
                "win81/AllSupported.pdb",
                "win81/AllSupported.pri",
                "win81/Newtonsoft.Json.dll",
                
                "wp8/AllSupported.dll",
                "wp8/AllSupported.pdb",
                "wp8/Newtonsoft.Json.dll",
                
                "wp81/AllSupported.dll",
                "wp81/AllSupported.pdb",
                "wp81/Newtonsoft.Json.dll",
                
                "wpa81/AllSupported.dll",
                "wpa81/AllSupported.pdb",
                "wpa81/AllSupported.pri",
                "wpa81/Newtonsoft.Json.dll",

                "uap10.0/AllSupported.dll",
                "uap10.0/AllSupported.pri",
                "uap10.0/AllSupported.pdb",

                "xamarinios/AllSupported.dll",
                "xamarinios/AllSupported.pdb",
                "xamarinios/AllSupported.dll.mdb",

                "xamarinmac/AllSupported.dll",
                "xamarinmac/AllSupported.pdb",

                "xamarintvos/AllSupported.dll",
                "xamarintvos/AllSupported.dll.mdb",
                "xamarintvos/AllSupported.pdb",

                "xamarinwatchos/AllSupported.dll",
                "xamarinwatchos/AllSupported.dll.mdb",
                "xamarinwatchos/AllSupported.pdb",
            });
        }

        [Theory]
        [InlineData("1", "win7-x86", "win7-x86;win7-x64", "win10-arm", "win7-x86;linux;WIN7-X86;unix", "osx-10.12", "win8-arm;win8-arm-aot",
            "win7-x86;win7-x64;win10-arm;linux;unix;osx-10.12;win8-arm;win8-arm-aot")]
        public void It_combines_inner_rids_for_restore(
            string identifier,
            string outerRid,
            string outerRids,
            string firstFrameworkRid,
            string firstFrameworkRids,
            string secondFrameworkRid,
            string secondFrameworkRids,
            string expectedCombination)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset(Path.Combine("CrossTargeting", "NetStandardAndNetCoreApp"), identifier: identifier)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();

                    propertyGroup.Add(
                        new XElement(ns + "RuntimeIdentifier", outerRid),
                        new XElement(ns + "RuntimeIdentifiers", outerRids));

                    propertyGroup.AddAfterSelf(
                        new XElement(ns + "PropertyGroup",
                            new XAttribute(ns + "Condition", "'$(TargetFramework)' == 'netstandard1.5'"),
                            new XElement(ns + "RuntimeIdentifier", firstFrameworkRid),
                            new XElement(ns + "RuntimeIdentifiers", firstFrameworkRids)),
                        new XElement(ns + "PropertyGroup",
                            new XAttribute(ns + "Condition", "'$(TargetFramework)' == 'netcoreapp1.1'"),
                            new XElement(ns + "RuntimeIdentifier", secondFrameworkRid),
                            new XElement(ns + "RuntimeIdentifiers", secondFrameworkRids)));
                });

            var command = new GetValuesCommand(Stage0MSBuild, testAsset.TestRoot, "", valueName: "RuntimeIdentifiers");
            command.DependsOnTargets = "GetAllRuntimeIdentifiers";
            command.Execute().Should().Pass();
            command.GetValues().Should().BeEquivalentTo(expectedCombination.Split(';'));
        }
    }
}
