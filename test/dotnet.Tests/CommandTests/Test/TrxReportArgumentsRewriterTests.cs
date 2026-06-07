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
    public void RewriteIfNeeded_MultiModuleWithExplicitFilename_DoesNotRewrite()
    {
        // User specified the file name → respect it. MTP is responsible for whatever happens next
        // (including its own overwrite warning when modules collide on the same file).
        var args = new List<string> { "--report-trx", "--report-trx-filename", "test_results.trx" };

        var result = TrxReportArgumentsRewriter.RewriteIfNeeded(args, CreateModule(), isMultiTestModule: true);

        result.Should().Equal(args);
    }

    [Fact]
    public void RewriteIfNeeded_MultiModuleWithExplicitFilename_EqualsForm_DoesNotRewrite()
    {
        var args = new List<string> { "--report-trx", "--report-trx-filename=test_results.trx" };

        var result = TrxReportArgumentsRewriter.RewriteIfNeeded(args, CreateModule(), isMultiTestModule: true);

        result.Should().Equal(args);
    }

    [Fact]
    public void RewriteIfNeeded_MultiModuleWithExplicitFilenameOnly_DoesNotRewrite()
    {
        // --report-trx-filename alone (without --report-trx) is enough to enable TRX reporting in MTP,
        // and the user has named the file → SDK does nothing.
        var args = new List<string> { "--report-trx-filename", "test_results.trx" };

        var result = TrxReportArgumentsRewriter.RewriteIfNeeded(args, CreateModule(), isMultiTestModule: true);

        result.Should().Equal(args);
    }

    [Fact]
    public void RewriteIfNeeded_MultiModuleWithMalformedFilename_DoesNotRewrite()
    {
        // --report-trx-filename with no value is a user error; let MTP report it.
        var args = new List<string> { "--report-trx", "--report-trx-filename" };

        var result = TrxReportArgumentsRewriter.RewriteIfNeeded(args, CreateModule(), isMultiTestModule: true);

        result.Should().Equal(args);
    }

    [Fact]
    public void RewriteIfNeeded_MultiModuleOnlyTrxFlag_InjectsUniqueFilenameWithTimestamp()
    {
        var args = new List<string> { "--report-trx" };
        var now = new DateTimeOffset(2024, 6, 2, 14, 7, 6, TimeSpan.Zero);

        var result = TrxReportArgumentsRewriter.RewriteIfNeeded(args, CreateModule(), isMultiTestModule: true, utcNow: now);

        result.Should().Equal("--report-trx", "--report-trx-filename", "MyTest_net9.0_2024-06-02_14-07-06.0000000.trx");
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
    public void RewriteIfNeeded_MultiModuleOnlyTrxFlag_TargetFrameworkNull_UsesPathHashAsDisambiguator()
    {
        // --test-modules path: TargetFramework isn't populated. The injected name still has to be
        // unique across modules even if two modules share an assembly name across TFM folders. We
        // disambiguate with a short stable hash of the TargetPath rather than inferring the TFM.
        var args = new List<string> { "--report-trx" };
        var now = new DateTimeOffset(2024, 6, 2, 14, 7, 6, TimeSpan.Zero);

        var net8 = CreateModule(targetPath: "/repo/bin/Debug/net8.0/MyTest.dll", targetFramework: null);
        var net9 = CreateModule(targetPath: "/repo/bin/Debug/net9.0/MyTest.dll", targetFramework: null);

        var net8Result = TrxReportArgumentsRewriter.RewriteIfNeeded(args, net8, isMultiTestModule: true, utcNow: now);
        var net9Result = TrxReportArgumentsRewriter.RewriteIfNeeded(args, net9, isMultiTestModule: true, utcNow: now);

        net8Result[2].Should().StartWith("MyTest_").And.EndWith("_2024-06-02_14-07-06.0000000.trx");
        net9Result[2].Should().StartWith("MyTest_").And.EndWith("_2024-06-02_14-07-06.0000000.trx");
        net8Result[2].Should().NotBe(net9Result[2]);
    }

    [Fact]
    public void RewriteIfNeeded_LeavesUnrelatedArgsUntouched_WhenInjecting()
    {
        var args = new List<string> { "--filter", "MyTest", "--report-trx", "--other", "value" };
        var now = new DateTimeOffset(2024, 6, 2, 14, 7, 6, TimeSpan.Zero);

        var result = TrxReportArgumentsRewriter.RewriteIfNeeded(args, CreateModule(), isMultiTestModule: true, utcNow: now);

        result.Should().Equal(
            "--filter", "MyTest",
            "--report-trx",
            "--other", "value",
            "--report-trx-filename", "MyTest_net9.0_2024-06-02_14-07-06.0000000.trx");
    }
}
