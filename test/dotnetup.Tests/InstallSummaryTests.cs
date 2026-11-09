// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
[DoNotParallelize]
public class InstallSummaryTests
{
    private const string RootPath = @"D:\sdk\.dotnet";
    private static readonly DotnetInstallRoot s_root = new(RootPath, InstallArchitecture.x64);

    [TestMethod]
    public void FormatGroupedRequests_ListsVersionsUnderEachComponent()
    {
        var requests = new[]
        {
            Request(InstallComponent.Runtime, "6.0.36"),
            Request(InstallComponent.Runtime, "10.0.8"),
            Request(InstallComponent.SDK, "10.0.100"),
        };

        InstallExecutor.FormatGroupedRequests(requests).Should().Be(
            $"{InstallComponent.Runtime.GetDisplayName()} 6.0.36, 10.0.8, {InstallComponent.SDK.GetDisplayName()} 10.0.100");
    }

    [TestMethod]
    public void Summary_UsesRequestOrderAlignmentAndOutcomes()
    {
        var requests = new[]
        {
            Request(InstallComponent.Runtime, "6.0.36"),
            Request(InstallComponent.Runtime, "8.0.27"),
            Request(InstallComponent.Runtime, "9.0.16"),
            Request(InstallComponent.Runtime, "10.0.8"),
        };

        // Results arrive in completion order, not request order.
        var batch = new InstallBatchResult(
            [
                Result(requests[3], downloadedBytes: 31_200_000),
                Result(requests[1], alreadyInstalled: true),
                Result(requests[0], downloadedBytes: 28_300_000, repaired: true),
            ],
            [new InstallFailure(requests[2], new DotnetInstallException(DotnetInstallErrorCode.DownloadFailed, "Download failed."))]);

        string runtime = InstallComponent.Runtime.GetDisplayName();
        CaptureLines(() => InstallExecutor.DisplaySummaryInRequestOrder(requests, batch, RootPath)).Should().Equal(
            $"  {runtime} 6.0.36  installed (27.0 MB, repaired)",
            $"  {runtime} 8.0.27  already present, skipped",
            $"  {runtime} 9.0.16  failed: Download failed.",
            $"  {runtime} 10.0.8  installed (29.8 MB)",
            $"Installed at {RootPath}.");
    }

    [TestMethod]
    public void Summary_OmitsInstalledAtWhenEverythingFailed()
    {
        var request = Request(InstallComponent.SDK, "10.0.100");
        var batch = new InstallBatchResult([], [new InstallFailure(request, new DotnetInstallException(DotnetInstallErrorCode.DownloadFailed, "boom"))]);

        CaptureLines(() => InstallExecutor.DisplaySummaryInRequestOrder([request], batch, RootPath)).Should().Equal(
            $"  {InstallComponent.SDK.GetDisplayName()} 10.0.100  failed: boom");
    }

    private static ResolvedInstallRequest Request(InstallComponent component, string version) =>
        new(new DotnetInstallRequest(s_root, new UpdateChannel(version), component, new InstallRequestOptions()), new ReleaseVersion(version));

    private static InstallResult Result(ResolvedInstallRequest request, bool alreadyInstalled = false, long? downloadedBytes = null, bool repaired = false) =>
        new(new DotnetInstall(s_root, request.ResolvedVersion, request.Request.Component), alreadyInstalled, repaired, downloadedBytes);

    private static string[] CaptureLines(Action action)
    {
        IAnsiConsole previousConsole = AnsiConsole.Console;
        using var output = new StringWriter();
        try
        {
            AnsiConsole.Console = AdminModeTestEnvironment.CreateConsole(output);
            AnsiConsole.Profile.Width = int.MaxValue;
            action();
        }
        finally
        {
            AnsiConsole.Console = previousConsole;
        }

        return output.ToString().Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
    }
}
