// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Build.Tasks;

namespace Microsoft.NET.ToolPack.Tests
{
    public class GivenThatWeWantToPackAToolWithCustomNuspec : SdkTest
    {
        public GivenThatWeWantToPackAToolWithCustomNuspec(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_should_warn_when_using_custom_nuspec_file()
        {
            const string customNuspecFileName = "custom.nuspec";
            TestAsset helloWorldAsset = TestAssetsManager
                .CopyTestAsset("PortableTool", "PackToolWithCustomNuspec")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    XNamespace ns = project.Root.Name.Namespace;
                    XElement propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "NuspecFile", customNuspecFileName));
                });

            // Create a dummy nuspec file in the test directory
            string nuspecFilePath = Path.Combine(helloWorldAsset.TestRoot, customNuspecFileName);
            File.WriteAllText(nuspecFilePath, @"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
  <metadata>
    <id>consoledemo</id>
    <version>1.0.0</version>
    <authors>Test</authors>
    <description>Test package</description>
  </metadata>
</package>");

            var packCommand = new PackCommand(Log, helloWorldAsset.TestRoot);

            CommandResult result = packCommand.Execute();

            // The command should still succeed (it's just a warning)
            result.ExitCode.Should().Be(0);

            // But it should contain the warning
            string expectedWarningMessage = Strings.ResourceManager.GetString("DotnetToolDoesNotSupportCustomNuspecFile");
            // The warning message contains a format placeholder {0} for the file path
            // We just check that the base warning message is present
            result.StdOut.Should().Contain("NETSDK1235");
            result.StdOut.Should().Contain(".NET Tools do not support using a custom .nuspec file");
            result.StdOut.Should().Contain(customNuspecFileName);
        }

        [Fact]
        public void It_should_not_warn_when_nuspec_file_is_not_specified()
        {
            TestAsset helloWorldAsset = TestAssetsManager
                .CopyTestAsset("PortableTool", "PackToolWithoutCustomNuspec")
                .WithSource();

            var packCommand = new PackCommand(Log, helloWorldAsset.TestRoot);

            CommandResult result = packCommand.Execute();

            result.ExitCode.Should().Be(0);

            // Should not contain the custom nuspec warning
            result.StdOut.Should().NotContain("NETSDK1235");
            result.StdOut.Should().NotContain(".NET Tools do not support using a custom .nuspec file");
        }
    }
}
