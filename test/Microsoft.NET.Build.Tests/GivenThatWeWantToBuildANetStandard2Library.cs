// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.NET.Build.Tests
{
    [TestClass]
    public class GivenThatWeWantToBuildANetStandard2Library : SdkTest
    {

        [TestMethod]
        [DataRow("netstandard2.0")]
        [DataRow("netstandard2.1")]
        public void It_builds_a_netstandard2_library_successfully(string targetFramework)
        {
            TestProject project = new()
            {
                Name = "NetStandard2Library",
                TargetFrameworks = targetFramework,
            };

            var testAsset = TestAssetsManager.CreateTestProject(project, identifier: targetFramework);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

        }

        [TestMethod]
        public void It_resolves_assembly_conflicts()
        {
            TestProject project = new()
            {
                Name = "NetStandard2Library",
                TargetFrameworks = "netstandard2.0",
            };

            project.SourceFiles[project.Name + ".cs"] = $@"
using System;
public static class {project.Name}
{{
    {ConflictResolutionAssets.ConflictResolutionTestMethod}
}}";

            var testAsset = TestAssetsManager.CreateTestProject(project)
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    p.Root.Add(itemGroup);

                    foreach (var dependency in ConflictResolutionAssets.ConflictResolutionDependencies)
                    {
                        itemGroup.Add(new XElement(ns + "PackageReference",
                            new XAttribute("Include", dependency.Item1),
                            new XAttribute("Version", dependency.Item2)));
                    }

                });

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

        }
    }
}
