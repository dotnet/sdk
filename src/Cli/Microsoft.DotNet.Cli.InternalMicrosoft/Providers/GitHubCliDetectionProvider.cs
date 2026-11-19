// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.InternalMicrosoft;

/// <summary>
/// Gets a token from a configured GitHub CLI executable and checks its organization membership.
/// The detector reuses each configured provider for process-wide detection.
/// </summary>
internal sealed class GitHubCliDetectionProvider : IInternalMicrosoftDetectionProvider
{
    private readonly string _executable;
    private readonly Func<InternalMicrosoftDetectionContext, bool> _isSupported;

    internal GitHubCliDetectionProvider(
        string name,
        string executable,
        Func<InternalMicrosoftDetectionContext, bool> isSupported)
    {
        Name = name;
        _executable = executable;
        _isSupported = isSupported;
    }

    public string Name { get; }
    public int Stage => 2;

    public bool IsSupported(InternalMicrosoftDetectionContext context) =>
        _isSupported(context) && context.CommandExists(_executable);

    public async Task<InternalMicrosoftProbeResult> DetectAsync(
        InternalMicrosoftDetectionContext context,
        CancellationToken cancellationToken)
    {
        var processResult = await context.RunProcessProbeAsync(
            _executable,
            ["auth", "token", "--hostname", "github.com"],
            cancellationToken).ConfigureAwait(false);
        if (processResult.Failure is
            {
                Code: InternalMicrosoftProbeFailureCode.ProcessExit,
                ProcessExitCode: 4
            })
        {
            return InternalMicrosoftProbeResult.NotDetected;
        }

        if (processResult.Failure is not null)
        {
            return InternalMicrosoftProbeResult.Failed(processResult.Failure);
        }

        var token = processResult.StandardOutput.Trim();
        return InternalMicrosoftDetectionUtilities.IsGitHubToken(token)
            ? await new GitHubMembershipClient(context).CheckAsync(token, cancellationToken).ConfigureAwait(false)
            : InternalMicrosoftProbeResult.NotDetected;
    }
}
