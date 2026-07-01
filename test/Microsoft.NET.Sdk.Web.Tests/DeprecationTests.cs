// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Microsoft.NET.TestFramework.Utilities;

namespace Microsoft.NET.Sdk.Web.Tests;

[TestClass]
public class DeprecationTests : SdkTest
{
    [TestMethod]
    public void It_does_not_show_deprecation_warning_when_IncludeOpenAPIAnalyzers_is_not_set()
    {
        var testProject = new TestProject()
        {
            Name = "WebAppWithoutOpenAPIAnalyzers",
            TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            ProjectSdk = "Microsoft.NET.Sdk.Web"
        };

        var testAsset = TestAssetsManager.CreateTestProject(testProject);

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

    [TestMethod]
    public void It_does_not_show_deprecation_warning_when_IncludeOpenAPIAnalyzers_is_false()
    {
        var testProject = new TestProject()
        {
            Name = "WebAppWithOpenAPIAnalyzersFalse",
            TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            ProjectSdk = "Microsoft.NET.Sdk.Web"
        };

        testProject.AdditionalProperties["IncludeOpenAPIAnalyzers"] = "false";

        var testAsset = TestAssetsManager.CreateTestProject(testProject);

        var buildCommand = new BuildCommand(testAsset);
        buildCommand
            .Execute()
            .Should()
            .Pass()
            .And
            .NotHaveStdOutContaining("ASPDEPR007");
    }

    [TestMethod]
    [DataRow(ToolsetInfo.CurrentTargetFramework)]
    [DataRow("net8.0")]
    [DataRow("net9.0")]
    public void It_shows_deprecation_warning_across_target_frameworks(string targetFramework)
    {
        var testProject = new TestProject()
        {
            Name = $"WebApp_{targetFramework.Replace(".", "_")}",
            TargetFrameworks = targetFramework,
            ProjectSdk = "Microsoft.NET.Sdk.Web"
        };

        testProject.AdditionalProperties["IncludeOpenAPIAnalyzers"] = "true";

        var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

        var buildCommand = new BuildCommand(testAsset);
        buildCommand
            .Execute()
            .Should()
            .Pass()
            .And
            .HaveStdOutContaining("ASPDEPR007");
    }
}
