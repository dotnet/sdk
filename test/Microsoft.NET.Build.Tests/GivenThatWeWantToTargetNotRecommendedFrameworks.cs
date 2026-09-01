// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.NET.Build.Tests
{
    [TestClass]
    public class GivenThatWeWantToTargetNotRecommendedFrameworks : SdkTest
    {

        [TestMethod]
        [DataRow("NetStandard1.0")]
        [DataRow("NetStandard1.1")]
        [DataRow("NetStandard1.2")]
        [DataRow("NetStandard1.3")]
        [DataRow("NetStandard1.4")]
        [DataRow("NetStandard1.5")]
        [DataRow("NetStandard1.6")]
        public void It_warns_that_framework_is_not_recommended(string targetFrameworks)
        {
            var testProject = new TestProject()
            {
                Name = $"NotRecommended{targetFrameworks}",
                TargetFrameworks = targetFrameworks,
                IsExe = false
            };

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            var result = buildCommand
                .Execute();

            result
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("NETSDK1215");
        }

        [TestMethod]
        [DataRow("NetStandard2.0")]
        [DataRow("NetStandard2.1")]
        public void It_should_not_warn_when_framework_not_recommended(string targetFrameworks)
        {
            var testProject = new TestProject()
            {
                Name = $"NotRecommended{targetFrameworks}",
                TargetFrameworks = targetFrameworks,
                IsExe = false
            };

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            var result = buildCommand
                .Execute();

            result
                .Should()
                .Pass()
                .And
                .NotHaveStdOutContaining("NETSDK1215");
        }

        [TestMethod]
        public void It_only_checks_for_netcoreapp_not_recommended_frameworks()
        {
            var testProject = new TestProject()
            {
                Name = $"NotRecommendedOnlyNetCore",
                TargetFrameworks = $"netstandard1.6;{ToolsetInfo.CurrentTargetFramework};net472",
                IsExe = false,
            };

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            var result = buildCommand
                .Execute();

            var lines = (result.StdOut.Split(Environment.NewLine)).Where(line => line.Contains("NETSDK1215"));

            Assert.IsNotNull(lines.FirstOrDefault(line => line.IndexOf("netstandard1.6") >= 0));
            foreach (var line in lines)
            {
                Assert.DoesNotContain(ToolsetInfo.CurrentTargetFramework, line);
            }
            foreach (var line in lines)
            {
                Assert.DoesNotContain("net472", line);
            }
        }

        [TestMethod]
        public void It_does_not_warn_when_deactivating_check()
        {
            var testProject = new TestProject()
            {
                Name = $"NotRecommendedNoWarning",
                TargetFrameworks = "netstandard1.6",
                IsExe = false
            };

            testProject.AdditionalProperties["CheckNotRecommendedTargetFramework"] = "false";

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            var result = buildCommand
                .Execute();

            result
                .Should()
                .Pass()
                .And
                .NotHaveStdOutContaining("NETSDK1215");
        }
    }
}
