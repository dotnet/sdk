// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.InternalMicrosoft;

/// <summary>
/// Checks supported GitHub tokens from Copilot CLI settings.
/// The detector reuses this stateless provider for process-wide detection.
/// </summary>
internal sealed class CopilotCliDetectionProvider : IInternalMicrosoftDetectionProvider
{
    private const int MaxTokenCandidates = 5;

    public string Name => "Copilot CLI GitHub org membership";
    public int Stage => 2;

    public bool IsSupported(InternalMicrosoftDetectionContext context) => !context.IsCIEnvironment;

    public Task<InternalMicrosoftProbeResult> DetectAsync(
        InternalMicrosoftDetectionContext context,
        CancellationToken cancellationToken) =>
        new GitHubMembershipClient(context).CheckAnyAsync(GetCandidates(context), cancellationToken);

    private static IEnumerable<GitHubTokenCandidate> GetCandidates(InternalMicrosoftDetectionContext context)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var count = 0;
        foreach (var (name, value) in context.GetEnvironmentVariables()
                     .Where(variable => variable.Name.StartsWith(
                         "COPILOT_GH_ACCOUNT_",
                         StringComparison.OrdinalIgnoreCase))
                     .OrderBy(variable => variable.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (InternalMicrosoftDetectionUtilities.IsGitHubToken(value) &&
                seen.Add(value!) &&
                count++ < MaxTokenCandidates)
            {
                yield return new(name, value!);
            }
        }

        foreach (var fileName in new[] { "config.json", "settings.json" })
        {
            if (count >= MaxTokenCandidates)
            {
                yield break;
            }

            var path = Path.Combine(context.HomeDirectory, ".copilot", fileName);
            foreach (var token in InternalMicrosoftDetectionUtilities.ReadGitHubTokensFromJsonFile(path))
            {
                if (seen.Add(token) && count++ < MaxTokenCandidates)
                {
                    yield return new(fileName, token);
                }
            }
        }
    }
}
