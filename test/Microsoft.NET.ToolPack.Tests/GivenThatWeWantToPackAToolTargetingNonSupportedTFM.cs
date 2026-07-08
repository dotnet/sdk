// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Build.Tasks;

namespace Microsoft.NET.ToolPack.Tests
{
    [TestClass]
    public class GivenThatWeWantToPackAToolTargetingNonSupportedTFM : SdkTest
    {
        [TestMethod]
        // lower than netcoreapp2.0
        [DataRow("TargetFramework", "netcoreapp2.0", "DotnetToolDoesNotSupportTFMLowerThanNetcoreapp21")]
        [DataRow("TargetFramework", "netcoreapp1.1", "DotnetToolDoesNotSupportTFMLowerThanNetcoreapp21")]
        [DataRow("TargetFrameworks", "netcoreapp2.0;netcoreapp2.1", "DotnetToolDoesNotSupportTFMLowerThanNetcoreapp21")]
        // non netcoreapp
        [DataRow("TargetFramework", "netstandard2.0", "DotnetToolOnlySupportNetcoreapp")]
        public void It_should_fail_with_error_message(string targetFrameworkProperty,
            string targetFramework,
            string expectedErrorResourceName)
        {
            TestAsset helloWorldAsset = TestAssetsManager
                                        .CopyTestAsset("PortableTool", "PackNonSupportedTFM", identifier: targetFrameworkProperty + targetFramework)
                                        .WithSource()
                                        .WithProjectChanges(project =>
                                        {
                                            XNamespace ns = project.Root.Name.Namespace;
                                            XElement propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();

                                            propertyGroup.Element(ns + "TargetFramework").Remove();
                                            propertyGroup.Add(new XElement(ns + targetFrameworkProperty, targetFramework));
                                        });

            var packCommand = new PackCommand(Log, helloWorldAsset.TestRoot);

            CommandResult result = packCommand.Execute();
            result.ExitCode.Should().NotBe(0);

            // walk around attribute requires static
            string expectedErrorMessage = Strings.ResourceManager.GetString(expectedErrorResourceName);

            result.StdOut.Should().Contain(expectedErrorMessage);
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void It_should_fail_with_error_message_on_fullframework()
        {
            It_should_fail_with_error_message("TargetFramework", "net46", "DotnetToolOnlySupportNetcoreapp");
        }
    }
}
