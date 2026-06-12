// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToTargetNotRecommendedFrameworks : SdkTest
    {
        public GivenThatWeWantToTargetNotRecommendedFrameworks(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("NetStandard1.0")]
        [InlineData("NetStandard1.1")]
        [InlineData("NetStandard1.2")]
        [InlineData("NetStandard1.3")]
        [InlineData("NetStandard1.4")]
        [InlineData("NetStandard1.5")]
        [InlineData("NetStandard1.6")]
        public void It_warns_that_framework_is_not_recommended(string targetFrameworks)
        {
            var testProject = new TestProject()
            {
                Name = $"NotRecommended{targetFrameworks}",
                TargetFrameworks = targetFrameworks,
                IsExe = false
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            var result = buildCommand
                .Execute();

            result
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("NETSDK1215");
        }

        [Theory]
        [InlineData("NetStandard2.0")]
        [InlineData("NetStandard2.1")]
        public void It_should_not_warn_when_framework_not_recommended(string targetFrameworks)
        {
            var testProject = new TestProject()
            {
                Name = $"NotRecommended{targetFrameworks}",
                TargetFrameworks = targetFrameworks,
                IsExe = false
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            var result = buildCommand
                .Execute();

            result
                .Should()
                .Pass()
                .And
                .NotHaveStdOutContaining("NETSDK1215");
        }

        [Fact]
        public void It_only_checks_for_netcoreapp_not_recommended_frameworks()
        {
            var testProject = new TestProject()
            {
                Name = $"NotRecommendedOnlyNetCore",
                TargetFrameworks = $"netstandard1.6;{ToolsetInfo.CurrentTargetFramework};net472",
                IsExe = false,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            var result = buildCommand
                .Execute();

            var lines = (result.StdOut.Split(Environment.NewLine)).Where(line => line.Contains("NETSDK1215"));

            Assert.NotNull(lines.FirstOrDefault(line => line.IndexOf("netstandard1.6") >= 0));
            Assert.All(lines, line => Assert.DoesNotContain(ToolsetInfo.CurrentTargetFramework, line));
            Assert.All(lines, line => Assert.DoesNotContain("net472", line));
        }

        [Fact]
        public void It_does_not_warn_when_deactivating_check()
        {
            var testProject = new TestProject()
            {
                Name = $"NotRecommendedNoWarning",
                TargetFrameworks = "netstandard1.6",
                IsExe = false
            };

            testProject.AdditionalProperties["CheckNotRecommendedTargetFramework"] = "false";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

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
