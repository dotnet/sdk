// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal sealed record ArtifactPostProcessingApplication(
    TestModule Module,
    string? TargetFramework,
    string? Architecture,
    IReadOnlySet<string> SupportedKinds,
    IReadOnlySet<string> SupportedExtensions);

internal sealed record ArtifactPostProcessingArtifact(
    string Path,
    string? Kind,
    string ProducingTestModule,
    string? TargetFramework,
    string? Architecture,
    string ExecutionId);

internal sealed record ArtifactPostProcessingGroup(
    string Key,
    bool IsKind,
    IReadOnlyList<ArtifactPostProcessingArtifact> Artifacts,
    IReadOnlyList<ArtifactPostProcessingApplication> Candidates);

internal sealed record ArtifactPostProcessingJob(
    ArtifactPostProcessingApplication Application,
    IReadOnlyList<ArtifactPostProcessingGroup> Groups);

internal sealed record ArtifactPostProcessingPlan(IReadOnlyList<ArtifactPostProcessingJob> Jobs);

internal static class ArtifactPostProcessingPlanner
{
    private const string MicrosoftCodeCoverageKind = "microsoft.codecoverage";
    private const string MicrosoftCodeCoverageExtension = ".coverage";

    public static ArtifactPostProcessingPlan Plan(
        IReadOnlyList<ArtifactPostProcessingApplication> applications,
        IReadOnlyList<ArtifactPostProcessingArtifact> artifacts)
    {
        ArtifactPostProcessingArtifact[] distinctArtifacts =
            [.. artifacts.DistinctBy(artifact => artifact.Path, FileUtilities.PathComparer)];
        List<ArtifactPostProcessingGroup> groups = [];

        foreach (IGrouping<string, ArtifactPostProcessingArtifact> group in distinctArtifacts
            .Where(artifact => artifact.Kind is not null)
            .GroupBy(artifact => artifact.Kind!, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            AddGroup(group.Key, isKind: true, [.. group]);
        }

        foreach (IGrouping<string, ArtifactPostProcessingArtifact> group in distinctArtifacts
            .Where(artifact => artifact.Kind is null)
            .GroupBy(artifact => Path.GetExtension(artifact.Path).ToLowerInvariant(), StringComparer.Ordinal)
            .Where(group => group.Key.Length > 0)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            AddGroup(group.Key, isKind: false, [.. group]);
        }

        ArtifactPostProcessingGroup[] mergeableGroups =
        [
            .. groups.Where(group =>
                group.Artifacts.Count >= 2
                || groups.Any(other => CanCombineKindAndExtensionGroups(group, other)))
        ];

        var uncoveredGroups = new HashSet<ArtifactPostProcessingGroup>(mergeableGroups);
        List<ArtifactPostProcessingJob> jobs = [];
        while (uncoveredGroups.Count > 0)
        {
            ArtifactPostProcessingApplication? winner = applications
                .Select(application => new
                {
                    Application = application,
                    Groups = uncoveredGroups.Where(group => group.Candidates.Contains(application)).ToArray(),
                })
                .Where(candidate => candidate.Groups.Length > 0)
                .OrderByDescending(candidate => candidate.Groups.Length)
                .ThenByDescending(candidate => candidate.Groups.Sum(group =>
                    group.Artifacts.Count(artifact => FileUtilities.PathComparer.Equals(
                        artifact.ProducingTestModule,
                        candidate.Application.Module.TargetPath))))
                .ThenByDescending(candidate => GetFrameworkVersion(candidate.Application.TargetFramework))
                .ThenBy(candidate => candidate.Application.Module.TargetPath, FileUtilities.PathComparer)
                .Select(candidate => candidate.Application)
                .FirstOrDefault();

            if (winner is null)
            {
                break;
            }

            ArtifactPostProcessingGroup[] coveredGroups =
                [.. uncoveredGroups.Where(group => group.Candidates.Contains(winner))];
            jobs.Add(new ArtifactPostProcessingJob(winner, coveredGroups));
            uncoveredGroups.ExceptWith(coveredGroups);
        }

        return new ArtifactPostProcessingPlan(jobs);

        void AddGroup(string key, bool isKind, ArtifactPostProcessingArtifact[] inputs)
        {
            ArtifactPostProcessingApplication[] candidates =
            [
                .. applications.Where(application =>
                    (isKind
                        ? application.SupportedKinds.Contains(key)
                        : application.SupportedExtensions.Contains(key))
                    && IsArchitectureCompatible(key, isKind, application, inputs))
            ];

            if (candidates.Length > 0)
            {
                groups.Add(new ArtifactPostProcessingGroup(key, isKind, inputs, candidates));
            }
        }
    }

    private static bool CanCombineKindAndExtensionGroups(
        ArtifactPostProcessingGroup first,
        ArtifactPostProcessingGroup second)
    {
        ArtifactPostProcessingGroup kindGroup = first.IsKind ? first : second;
        ArtifactPostProcessingGroup extensionGroup = first.IsKind ? second : first;

        return first.IsKind != second.IsKind
            && kindGroup.Artifacts.Any(artifact =>
                string.Equals(
                    Path.GetExtension(artifact.Path),
                    extensionGroup.Key,
                    StringComparison.OrdinalIgnoreCase))
            && kindGroup.Candidates.Intersect(extensionGroup.Candidates).Any()
            && kindGroup.Artifacts.Count + extensionGroup.Artifacts.Count >= 2;
    }

    private static bool IsArchitectureCompatible(
        string key,
        bool isKind,
        ArtifactPostProcessingApplication application,
        IReadOnlyList<ArtifactPostProcessingArtifact> artifacts)
    {
        bool isArchitectureSensitive = isKind
            ? string.Equals(key, MicrosoftCodeCoverageKind, StringComparison.Ordinal)
            : string.Equals(key, MicrosoftCodeCoverageExtension, StringComparison.Ordinal);

        return !isArchitectureSensitive
            || artifacts.All(artifact =>
                artifact.Architecture is null
                || string.Equals(artifact.Architecture, application.Architecture, StringComparison.OrdinalIgnoreCase));
    }

    private static Version GetFrameworkVersion(string? targetFramework)
        => string.IsNullOrEmpty(targetFramework)
            ? new Version()
            : NuGetFramework.ParseFolder(targetFramework).Version;
}
