// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Test;

namespace dotnet.Tests.CommandTests.Test;

public class TrxReportArgumentsRewriterTests
{
    private static TestModule CreateModule(string targetPath = "/repo/bin/Debug/net9.0/MyTest.dll", string? targetFramework = "net9.0")
        => new(
            RunProperties: new RunProperties("dotnet", $"exec \"{targetPath}\"", "/repo"),
            ProjectFullPath: "/repo/MyTest.csproj",
            TargetFramework: targetFramework,
            IsTestingPlatformApplication: true,
            LaunchSettings: null,
            TargetPath: targetPath,
            DotnetRootArchVariableName: null);

    [Fact]
    public void RewriteIfNeeded_SingleModule_DoesNotRewrite()
    {
        var args = new List<string> { "--report-trx", "--report-trx-filename", "test_results.trx" };

        var result = TrxReportArgumentsRewriter.RewriteIfNeeded(args, CreateModule(), isMultiTestModule: false);

        result.Should().Equal(args);
    }

    [Fact]
    public void RewriteIfNeeded_NoTrxOptions_DoesNotRewrite()
    {
        var args = new List<string> { "--filter", "MyTest" };

        var result = TrxReportArgumentsRewriter.RewriteIfNeeded(args, CreateModule(), isMultiTestModule: true);

        result.Should().Equal(args);
    }

    [Fact]
    public void RewriteIfNeeded_MultiModuleWithExplicitFilename_AppendsAsmAndTfm()
    {
        var args = new List<string> { "--report-trx", "--report-trx-filename", "test_results.trx" };

        var result = TrxReportArgumentsRewriter.RewriteIfNeeded(args, CreateModule(), isMultiTestModule: true);

        result.Should().Equal("--report-trx", "--report-trx-filename", "test_results_MyTest_net9.0.trx");
    }

    [Fact]
    public void RewriteIfNeeded_MultiModuleWithExplicitFilename_EqualsForm_AppendsAsmAndTfm()
    {
        var args = new List<string> { "--report-trx", "--report-trx-filename=test_results.trx" };

        var result = TrxReportArgumentsRewriter.RewriteIfNeeded(args, CreateModule(), isMultiTestModule: true);

        result.Should().Equal("--report-trx", "--report-trx-filename=test_results_MyTest_net9.0.trx");
    }

    [Fact]
    public void RewriteIfNeeded_MultiModuleOnlyTrxFlag_InjectsUniqueFilenameWithTimestamp()
    {
        var args = new List<string> { "--report-trx" };
        var now = new DateTimeOffset(2024, 6, 2, 14, 7, 6, TimeSpan.Zero);

        var result = TrxReportArgumentsRewriter.RewriteIfNeeded(args, CreateModule(), isMultiTestModule: true, utcNow: now);

        result.Should().Equal("--report-trx", "--report-trx-filename", "MyTest_net9.0_2024-06-02_14_07_06.trx");
    }

    [Fact]
    public void RewriteIfNeeded_MultiModuleOnlyTrxFlag_InjectedFilenameAvoidsOverwriteWarningOnRerun()
    {
        // Re-running `dotnet test` a second later should yield a different injected filename so MTP
        // doesn't emit "Trx file '...' already exists and will be overwritten." per module.
        var args = new List<string> { "--report-trx" };
        var t1 = new DateTimeOffset(2024, 6, 2, 14, 7, 6, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2024, 6, 2, 14, 7, 7, TimeSpan.Zero);

        var firstRun = TrxReportArgumentsRewriter.RewriteIfNeeded(args, CreateModule(), isMultiTestModule: true, utcNow: t1);
        var secondRun = TrxReportArgumentsRewriter.RewriteIfNeeded(args, CreateModule(), isMultiTestModule: true, utcNow: t2);

        firstRun[2].Should().NotBe(secondRun[2]);
    }

    [Fact]
    public void RewriteIfNeeded_MultiModuleOnlyTrxFilename_AppendsAsmAndTfm()
    {
        // --report-trx-filename alone is enough to enable TRX reporting in MTP.
        var args = new List<string> { "--report-trx-filename", "test_results.trx" };

        var result = TrxReportArgumentsRewriter.RewriteIfNeeded(args, CreateModule(), isMultiTestModule: true);

        result.Should().Equal("--report-trx-filename", "test_results_MyTest_net9.0.trx");
    }

    [Theory]
    [InlineData("{asm}")]
    [InlineData("{pname}")]
    [InlineData("{pid}")]
    public void RewriteIfNeeded_FilenameContainsUniquePlaceholder_DoesNotRewrite(string placeholder)
    {
        var fileName = $"test_{placeholder}.trx";
        var args = new List<string> { "--report-trx", "--report-trx-filename", fileName };

        var result = TrxReportArgumentsRewriter.RewriteIfNeeded(args, CreateModule(), isMultiTestModule: true);

        result.Should().Equal(args);
    }

    [Fact]
    public void RewriteIfNeeded_FilenameContainsOnlyTfmPlaceholder_StillRewrites()
    {
        // {tfm} alone is NOT a unique-per-module placeholder (two modules can share a TFM).
        var args = new List<string> { "--report-trx", "--report-trx-filename", "test_{tfm}.trx" };

        var result = TrxReportArgumentsRewriter.RewriteIfNeeded(args, CreateModule(), isMultiTestModule: true);

        result.Should().Equal("--report-trx", "--report-trx-filename", "test_{tfm}_MyTest_net9.0.trx");
    }

    [Fact]
    public void RewriteIfNeeded_FilenameWithDirectoryComponent_PreservesDirectory()
    {
        var args = new List<string> { "--report-trx-filename", Path.Combine("subdir", "results.trx") };

        var result = TrxReportArgumentsRewriter.RewriteIfNeeded(args, CreateModule(), isMultiTestModule: true);

        result.Should().Equal("--report-trx-filename", Path.Combine("subdir", "results_MyTest_net9.0.trx"));
    }

    [Fact]
    public void RewriteIfNeeded_TargetFrameworkNull_InfersFromTargetPath()
    {
        var module = CreateModule(targetPath: "/repo/bin/Debug/net9.0/MyTest.dll", targetFramework: null);

        var args = new List<string> { "--report-trx-filename", "results.trx" };

        var result = TrxReportArgumentsRewriter.RewriteIfNeeded(args, module, isMultiTestModule: true);

        result.Should().Equal("--report-trx-filename", "results_MyTest_net9.0.trx");
    }

    [Fact]
    public void RewriteIfNeeded_TargetFrameworkNullWithRidPath_InfersTfmFromParentSegment()
    {
        var module = CreateModule(targetPath: "/repo/bin/Debug/net8.0/win-x64/MyTest.dll", targetFramework: null);

        var args = new List<string> { "--report-trx-filename", "results.trx" };

        var result = TrxReportArgumentsRewriter.RewriteIfNeeded(args, module, isMultiTestModule: true);

        result.Should().Equal("--report-trx-filename", "results_MyTest_net8.0.trx");
    }

    [Fact]
    public void RewriteIfNeeded_TargetFrameworkNullAndNoTfmInPath_OmitsTfm()
    {
        var module = CreateModule(targetPath: "/repo/somewhere/MyTest.exe", targetFramework: null);

        var args = new List<string> { "--report-trx-filename", "results.trx" };

        var result = TrxReportArgumentsRewriter.RewriteIfNeeded(args, module, isMultiTestModule: true);

        result.Should().Equal("--report-trx-filename", "results_MyTest.trx");
    }

    [Fact]
    public void RewriteIfNeeded_FilenameWithoutExtension_AddsTrxExtension()
    {
        var args = new List<string> { "--report-trx-filename", "results" };

        var result = TrxReportArgumentsRewriter.RewriteIfNeeded(args, CreateModule(), isMultiTestModule: true);

        result.Should().Equal("--report-trx-filename", "results_MyTest_net9.0.trx");
    }

    [Fact]
    public void RewriteIfNeeded_LeavesUnrelatedArgsUntouched()
    {
        var args = new List<string> { "--filter", "MyTest", "--report-trx", "--report-trx-filename", "foo.trx", "--other", "value" };

        var result = TrxReportArgumentsRewriter.RewriteIfNeeded(args, CreateModule(), isMultiTestModule: true);

        result.Should().Equal("--filter", "MyTest", "--report-trx", "--report-trx-filename", "foo_MyTest_net9.0.trx", "--other", "value");
    }
}
