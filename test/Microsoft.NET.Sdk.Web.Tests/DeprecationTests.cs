// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.NET.Sdk.Web.Tests;

public class DeprecationTests(ITestOutputHelper log) : SdkTest(log)
{
    [Fact]
    public void It_does_not_show_deprecation_warning_when_IncludeOpenAPIAnalyzers_is_not_set()
    {
        var testProject = new TestProject()
        {
            Name = "WebAppWithoutOpenAPIAnalyzers",
            TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            ProjectSdk = "Microsoft.NET.Sdk.Web"
        };

        var testAsset = _testAssetsManager.CreateTestProject(testProject);

        var buildCommand = new BuildCommand(testAsset);
        buildCommand
            .Execute()
            .Should()
            .Pass()
            .And
            .NotHaveStdOutContaining("ASPDEPR007")
            .And
            .NotHaveStdOutContaining("IncludeOpenAPIAnalyzers");
    }

    [Fact]
    public void It_does_not_show_deprecation_warning_when_IncludeOpenAPIAnalyzers_is_false()
    {
        var testProject = new TestProject()
        {
            Name = "WebAppWithOpenAPIAnalyzersFalse",
            TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            ProjectSdk = "Microsoft.NET.Sdk.Web"
        };

        testProject.AdditionalProperties["IncludeOpenAPIAnalyzers"] = "false";

        var testAsset = _testAssetsManager.CreateTestProject(testProject);

        var buildCommand = new BuildCommand(testAsset);
        buildCommand
            .Execute()
            .Should()
            .Pass()
            .And
            .NotHaveStdOutContaining("ASPDEPR007");
    }

    [Theory]
    [InlineData(ToolsetInfo.CurrentTargetFramework)]
    [InlineData("net8.0")]
    [InlineData("net9.0")]
    public void It_shows_deprecation_warning_across_target_frameworks(string targetFramework)
    {
        var testProject = new TestProject()
        {
            Name = $"WebApp_{targetFramework.Replace(".", "_")}",
            TargetFrameworks = targetFramework,
            ProjectSdk = "Microsoft.NET.Sdk.Web"
        };

        testProject.AdditionalProperties["IncludeOpenAPIAnalyzers"] = "true";

        var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

        var buildCommand = new BuildCommand(testAsset);
        buildCommand
            .Execute()
            .Should()
            .Pass()
            .And
            .HaveStdOutContaining("ASPDEPR007");
    }
}
