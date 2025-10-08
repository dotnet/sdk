// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToSuppressCS8002Warning : SdkTest
    {
        public GivenThatWeWantToSuppressCS8002Warning(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("net6.0")]
        [InlineData("net7.0")]
        [InlineData("net8.0")]
        [InlineData("netstandard2.0")]
        [InlineData("netstandard2.1")]
        public void CS8002_is_suppressed_for_modern_net_tfms(string targetFramework)
        {
            var testProject = new TestProject
            {
                Name = "TestProject",
                TargetFrameworks = targetFramework,
                IsExe = false,
                SourceFiles =
                {
                    ["Class1.cs"] = @"
                        public class TestClass
                        {
                            public void TestMethod() { }
                        }
                    ",
                }
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject, identifier: targetFramework);

            var buildCommand = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.TestRoot, testProject.Name),
                targetFramework, "NoWarn")
            {
                DependsOnTargets = "Build"
            };

            var buildResult = buildCommand.Execute();
            buildResult.Should().Pass();

            var noWarnValues = buildCommand.GetValues();
            noWarnValues.Should().Contain("CS8002", $"CS8002 should be suppressed for {targetFramework}");
        }

        [Theory]
        [InlineData("net472")]
        [InlineData("net48")]
        public void CS8002_is_not_suppressed_for_net_framework_tfms(string targetFramework)
        {
            var testProject = new TestProject
            {
                Name = "TestProject",
                TargetFrameworks = targetFramework,
                IsExe = false,
                SourceFiles =
                {
                    ["Class1.cs"] = @"
                        public class TestClass
                        {
                            public void TestMethod() { }
                        }
                    ",
                }
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject, identifier: targetFramework);

            var buildCommand = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.TestRoot, testProject.Name),
                targetFramework, "NoWarn")
            {
                DependsOnTargets = "Build"
            };

            var buildResult = buildCommand.Execute();
            buildResult.Should().Pass();

            var noWarnValues = buildCommand.GetValues();
            noWarnValues.Should().NotContain("CS8002", $"CS8002 should NOT be suppressed for {targetFramework}");
        }

        [Fact]
        public void CS8002_suppression_works_with_existing_nowarn()
        {
            var testProject = new TestProject
            {
                Name = "TestProject",
                TargetFrameworks = "net8.0",
                IsExe = false,
                SourceFiles =
                {
                    ["Class1.cs"] = @"
                        public class TestClass
                        {
                            public void TestMethod() { }
                        }
                    ",
                }
            };

            testProject.AdditionalProperties["NoWarn"] = "CS1591";

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject);

            var buildCommand = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.TestRoot, testProject.Name),
                "net8.0", "NoWarn")
            {
                DependsOnTargets = "Build"
            };

            var buildResult = buildCommand.Execute();
            buildResult.Should().Pass();

            var noWarnValues = buildCommand.GetValues();
            noWarnValues.Should().Contain("CS1591", "Existing NoWarn should be preserved");
            noWarnValues.Should().Contain("CS8002", "CS8002 should be added to NoWarn");
        }
    }
}
