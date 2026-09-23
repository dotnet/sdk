// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.InternalMicrosoft;

/// <summary>
/// Reads Microsoft corporate identity from the current Windows environment.
/// The detector reuses this stateless provider for process-wide detection.
/// </summary>
internal sealed class WindowsUserDnsDomainDetectionProvider : IInternalMicrosoftDetectionProvider
{
    public string Name => "Windows USERDNSDOMAIN";
    public int Stage => 1;

    public bool IsSupported(InternalMicrosoftDetectionContext context) => context.IsWindows;

    public Task<InternalMicrosoftProbeResult> DetectAsync(
        InternalMicrosoftDetectionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var domain = context.GetEnvironmentVariable("USERDNSDOMAIN");
        if (!InternalMicrosoftDetectionUtilities.TryGetCorporateDomain(domain, out var corporateDomain))
        {
            return Task.FromResult(InternalMicrosoftProbeResult.NotDetected);
        }

        return Task.FromResult(new InternalMicrosoftProbeResult(
            true,
            InternalMicrosoftDetectionUtilities.NormalizeAlias(context.GetEnvironmentVariable("USERNAME")),
            corporateDomain));
    }
}

/// <summary>
/// Reads Microsoft corporate identity from the Windows environment visible through WSL.
/// The detector reuses this stateless provider for process-wide detection.
/// </summary>
internal sealed class WslWindowsUserDnsDomainDetectionProvider : IInternalMicrosoftDetectionProvider
{
    public string Name => "WSL Windows USERDNSDOMAIN";
    public int Stage => 1;

    public bool IsSupported(InternalMicrosoftDetectionContext context) => context.IsWsl;

    public async Task<InternalMicrosoftProbeResult> DetectAsync(
        InternalMicrosoftDetectionContext context,
        CancellationToken cancellationToken)
    {
        var output = await context.RunProcessProbeAsync(
            "cmd.exe",
            ["/d", "/s", "/c", "echo %USERDNSDOMAIN%&echo %USERNAME%"],
            cancellationToken).ConfigureAwait(false);
        if (output.Failure is not null)
        {
            return InternalMicrosoftProbeResult.Failed(output.Failure);
        }

        var lines = output.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0)
        {
            return InternalMicrosoftProbeResult.NotDetected;
        }

        return InternalMicrosoftDetectionUtilities.TryGetCorporateDomain(lines[0], out var corporateDomain)
            ? new InternalMicrosoftProbeResult(
                true,
                InternalMicrosoftDetectionUtilities.NormalizeAlias(lines.ElementAtOrDefault(1)),
                corporateDomain)
            : InternalMicrosoftProbeResult.NotDetected;
    }
}
