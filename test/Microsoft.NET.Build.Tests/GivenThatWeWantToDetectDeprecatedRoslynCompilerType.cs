// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.NET.Build.Tests;

public sealed class GivenThatWeWantToDetectDeprecatedRoslynCompilerType(ITestOutputHelper log) : SdkTest(log)
{
    [Fact]
    public void It_warns_when_RoslynCompilerType_is_Framework()
    {
        var testProject = new TestProject()
        {
            Name = "DeprecatedRoslynCompilerType",
            TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            IsExe = true
        };

        testProject.AdditionalProperties["RoslynCompilerType"] = "Framework";

        var testAsset = TestAssetsManager.CreateTestProject(testProject);

        var buildCommand = new BuildCommand(testAsset);

        var result = buildCommand
            .Execute();

        result
            .Should()
            .Pass()
            .And
            .HaveStdOutContaining(" NETSDK1234");
    }

    [Fact]
    public void It_does_not_warn_when_RoslynCompilerType_is_Core()
    {
        var testProject = new TestProject()
        {
            Name = "RoslynCompilerTypeCore",
            TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            IsExe = true
        };

        testProject.AdditionalProperties["RoslynCompilerType"] = "Core";

        var testAsset = TestAssetsManager.CreateTestProject(testProject);

        var buildCommand = new BuildCommand(testAsset);

        var result = buildCommand
            .Execute();

        result
            .Should()
            .Pass()
            .And
            .NotHaveStdOutContaining(" NETSDK1234");
    }

    [Fact]
    public void It_does_not_warn_when_RoslynCompilerType_is_FrameworkPackage()
    {
        var testProject = new TestProject()
        {
            Name = "RoslynCompilerTypeFrameworkPackage",
            TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            IsExe = true
        };

        testProject.AdditionalProperties["RoslynCompilerType"] = "FrameworkPackage";

        var testAsset = TestAssetsManager.CreateTestProject(testProject);

        var buildCommand = new BuildCommand(testAsset);

        var result = buildCommand
            .Execute();

        result
            .Should()
            .Pass()
            .And
            .NotHaveStdOutContaining(" NETSDK1234");
    }

    [Fact]
    public void It_does_not_warn_when_RoslynCompilerType_is_not_set()
    {
        var testProject = new TestProject()
        {
            Name = "RoslynCompilerTypeNotSet",
            TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            IsExe = true
        };

        var testAsset = TestAssetsManager.CreateTestProject(testProject);

        var buildCommand = new BuildCommand(testAsset);

        var result = buildCommand
            .Execute();

        result
            .Should()
            .Pass()
            .And
            .NotHaveStdOutContaining(" NETSDK1234");
    }

    [Fact]
    public void It_does_not_warn_when_suppressed_with_NoWarn()
    {
        var testProject = new TestProject()
        {
            Name = "RoslynCompilerTypeNoWarn",
            TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            IsExe = true
        };

        testProject.AdditionalProperties["RoslynCompilerType"] = "Framework";
        testProject.AdditionalProperties["NoWarn"] = "NETSDK1234";

        var testAsset = TestAssetsManager.CreateTestProject(testProject);

        var buildCommand = new BuildCommand(testAsset);

        var result = buildCommand
            .Execute();

        result
            .Should()
            .Pass()
            .And
            .NotHaveStdOutContaining(" NETSDK1234");
    }

    [Fact]
    public void It_can_suppress_warning_via_command_line()
    {
        var testProject = new TestProject()
        {
            Name = "RoslynCompilerTypeNoWarnCmdLine",
            TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            IsExe = true
        };

        testProject.AdditionalProperties["RoslynCompilerType"] = "Framework";

        var testAsset = TestAssetsManager.CreateTestProject(testProject);

        var buildCommand = new BuildCommand(testAsset);

        var result = buildCommand
            .Execute("/p:NoWarn=NETSDK1234");

        result
            .Should()
            .Pass()
            .And
            .NotHaveStdOutContaining(" NETSDK1234");
    }
}
