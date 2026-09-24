// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.InternalMicrosoft;

/// <summary>
/// Checks supported GitHub tokens from environment variables.
/// The detector reuses this stateless provider for process-wide detection.
/// </summary>
internal sealed class EnvironmentGitHubTokenDetectionProvider : IInternalMicrosoftDetectionProvider
{
    public string Name => "Environment GitHub token membership";
    public int Stage => 2;

    public bool IsSupported(InternalMicrosoftDetectionContext context) => !context.IsCIEnvironment;

    public Task<InternalMicrosoftProbeResult> DetectAsync(
        InternalMicrosoftDetectionContext context,
        CancellationToken cancellationToken) =>
        new GitHubMembershipClient(context).CheckAnyAsync(GetCandidates(context), cancellationToken);

    private static IEnumerable<GitHubTokenCandidate> GetCandidates(InternalMicrosoftDetectionContext context)
    {
        foreach (var variableName in new[]
                 {
                     "GH_TOKEN",
                     "GITHUB_TOKEN",
                     "GITHUB_PAT",
                     "GITHUB_OAUTH_TOKEN",
                     "GITHUB_ACCESS_TOKEN"
                 })
        {
            var token = context.GetEnvironmentVariable(variableName);
            if (InternalMicrosoftDetectionUtilities.IsGitHubToken(token))
            {
                yield return new(variableName, token!);
            }
        }
    }
}
