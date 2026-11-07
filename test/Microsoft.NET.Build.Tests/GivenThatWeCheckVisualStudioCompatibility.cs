// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests;

[TestClass]
public class GivenThatWeCheckVisualStudioCompatibility : SdkTest
{
    [TestMethod]
    [DataRow("17.14.0", ".NETCoreApp", "9.0", false, false)]
    [DataRow("17.14.0", ".NETCoreApp", "10.0", true, false)]
    [DataRow("17.14.0", ".NETCoreApp", "11.0", true, true)]
    [DataRow("17.15.0", ".NETCoreApp", "10.0", false, false)]
    [DataRow("18.0.0", ".NETCoreApp", "11.0", false, true)]
    [DataRow("18.10.0", ".NETCoreApp", "11.0", false, true)]
    [DataRow("18.11.0", ".NETCoreApp", "10.0", false, false)]
    [DataRow("18.11.0", ".NETCoreApp", "11.0", false, true)]
    [DataRow("18.11.123", ".NETCoreApp", "11.0", false, true)]
    [DataRow("18.11.0", ".NETCoreApp", "12.0", false, true)]
    [DataRow("18.12.0", ".NETCoreApp", "11.0", false, false)]
    [DataRow("18.12.1", ".NETCoreApp", "11.0", false, false)]
    [DataRow("19.0.0", ".NETCoreApp", "11.0", false, false)]
    [DataRow("18.11.0", ".NETStandard", "2.0", false, false)]
    [DataRow("18.11.0", ".NETFramework", "4.8", false, false)]
    public void It_warns_for_unsupported_target_framework_and_visual_studio_combinations(
        string msbuildVersion, string frameworkIdentifier, string frameworkVersion,
        bool expectNet10Warning, bool expectNet11Warning)
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory(
            identifier: $"{msbuildVersion}_{frameworkIdentifier}_{frameworkVersion}").Path;
        var targetsDirectory = Path.Combine(
            SdkTestContext.Current.ToolsetUnderTest.SdksPath, "Microsoft.NET.Sdk", "targets");
        var defaultItems = XDocument.Load(Path.Combine(targetsDirectory, "Microsoft.NET.Sdk.DefaultItems.targets"));
        var ns = defaultItems.Root!.Name.Namespace;
        var project = new XElement(ns + "Project",
            new XElement(ns + "Import",
                new XAttribute("Project", Path.Combine(targetsDirectory, "Microsoft.NET.Sdk.Common.targets"))),
            new XElement(ns + "PropertyGroup",
                new XElement(ns + "TestMSBuildVersion", msbuildVersion),
                new XElement(ns + "TargetFrameworkIdentifier", frameworkIdentifier),
                new XElement(ns + "_TargetFrameworkVersionWithoutV", frameworkVersion)),
            new XElement(ns + "Target", new XAttribute("Name", "_CheckForInvalidConfigurationAndPlatform")));

        foreach (var targetName in new[] { "_WarnWhenUsingNET10AndVSPriorTo18", "_WarnWhenUsingNET11AndVSPriorTo1812" })
        {
            var target = new XElement(defaultItems.Root.Elements(ns + "Target")
                .Single(element => element.Attribute("Name")?.Value == targetName));
            // MSBuildVersion is reserved, so substitute a test property to exercise older engines.
            var condition = target.Attribute("Condition")!;
            condition.Value = condition.Value.Replace("$(MSBuildVersion)", "$(TestMSBuildVersion)");
            project.Add(target);
        }

        new XDocument(project).Save(Path.Combine(testDirectory, "Compatibility.proj"));

        var result = new MSBuildCommand(Log, "_CheckForInvalidConfigurationAndPlatform", testDirectory)
            .ExecuteWithoutRestore();
        result.Should().Pass();
        var output = result.StdOut;
        Assert.IsNotNull(output);
        output.Contains("NETSDK1233").Should().Be(expectNet10Warning);
        output.Contains("NETSDK1245").Should().Be(expectNet11Warning);
        if (expectNet11Warning)
        {
            output.Should().Contain(
                "Targeting .NET 11.0 or higher in Visual Studio 2026 18.11 or earlier is not supported.");
        }
    }
}
