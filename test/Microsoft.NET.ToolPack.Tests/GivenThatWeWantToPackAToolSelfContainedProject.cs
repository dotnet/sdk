// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Build.Tasks;
using NuGet.Packaging;

namespace Microsoft.NET.ToolPack.Tests
{
    [TestClass]
    public class GivenThatWeWantToPackAToolSelfContainedProject : SdkTest
    {
        [TestMethod]
        [DataRow(true, true)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        public void Windows_targeted_tool_requires_a_self_contained_RID_specific_implementation(bool selfContained, bool hasRidSpecificImplementation)
        {
            TestAsset asset = TestAssetsManager
                .CopyTestAsset("PortableTool", nameof(Windows_targeted_tool_requires_a_self_contained_RID_specific_implementation), identifier: $"{selfContained}-{hasRidSpecificImplementation}")
                .WithSource()
                .WithTargetFramework($"{ToolsetInfo.CurrentTargetFramework}-windows")
                .WithProjectChanges(project =>
                {
                    XNamespace ns = project.Root.Name.Namespace;
                    XElement properties = project.Root.Elements(ns + "PropertyGroup").First();
                    properties.Add(new XElement(ns + "SelfContained", selfContained.ToString().ToLowerInvariant()));
                    properties.Add(new XElement(ns + "EnableWindowsTargeting", "true"));
                    if (hasRidSpecificImplementation)
                    {
                        properties.Add(new XElement(ns + "RuntimeIdentifiers", "win-x64"));
                    }
                });

            var packCommand = new PackCommand(asset);
            CommandResult result = packCommand.Execute();

            if (selfContained && hasRidSpecificImplementation)
            {
                result.Should().Pass();
                string packagePath = Directory.GetFiles(packCommand.GetPackageDirectory().FullName, "*.win-x64.*.nupkg").Single();
                using var package = new PackageArchiveReader(packagePath);
                package.GetFiles().Should().Contain("tools/any/win-x64/consoledemo.exe");

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    && RuntimeInformation.ProcessArchitecture == Architecture.X64)
                {
                    CommandResult toolResult = new DotnetToolCommand(Log, "exec", "consoledemo", "--yes", "--source", packCommand.GetPackageDirectory().FullName)
                        .WithWorkingDirectory(asset.TestRoot)
                        .Execute();
                    toolResult.Should().Pass();
                    toolResult.StdOut.Should().Contain("Hello World from Global Tool");
                }
            }
            else
            {
                result.Should().Fail().And.HaveStdOutContaining("NETSDK1146");
            }
        }

//  TODO: Add tests for AOT tools, which are now supported

        //[TestMethod]
        //public void It_should_fail_with_error_message()
        //{
        //    TestAsset helloWorldAsset = CreateAsset();

        //    var packCommand = new PackCommand(Log, helloWorldAsset.TestRoot);

        //    CommandResult result = packCommand.Execute("--property:SelfContained=true");
        //    result.ExitCode.Should().NotBe(0);
        //    result.StdOut.Should().Contain(Strings.PackAsToolCannotSupportSelfContained);
        //}

        // Reproduce of https://github.com/dotnet/cli/issues/10607
        [TestMethod]
        public void It_should_not_fail_on_build()
        {
            TestAsset helloWorldAsset = CreateAsset();

            var packCommand = new BuildCommand(helloWorldAsset);

            CommandResult result = packCommand.Execute("--property:SelfContained=true");
            result.ExitCode.Should().Be(0);
        }

        private TestAsset CreateAsset([CallerMemberName] string callingMethod = "")
        {
            TestAsset helloWorldAsset = TestAssetsManager
                                                    .CopyTestAsset("PortableTool", callingMethod)
                                                    .WithSource()
                                                    .WithProjectChanges(project =>
                                                    {
                                                        XNamespace ns = project.Root.Name.Namespace;
                                                        XElement propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                                                        propertyGroup.Add(new XElement("RuntimeIdentifier", "win-x64"));
                                                    });

            return helloWorldAsset;
        }
    }
}
