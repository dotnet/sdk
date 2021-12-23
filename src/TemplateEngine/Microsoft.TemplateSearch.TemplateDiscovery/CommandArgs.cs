// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateSearch.TemplateDiscovery
{
    internal enum SupportedQueries
    {
        PackageTypeQuery,
        TemplateQuery
    }

    internal class CommandArgs
    {
        internal CommandArgs(DirectoryInfo outputPath)
        {
            OutputPath = outputPath;
        }

        internal DirectoryInfo? LocalPackagePath { get; init; }

        internal DirectoryInfo OutputPath { get; init; }

        internal int PageSize { get; init; }

        internal bool SaveCandidatePacks { get; init; }

        internal bool RunOnlyOnePage { get; init; }

        internal bool IncludePreviewPacks { get; init; }

        internal bool DontFilterOnTemplateJson { get; init; }

        internal bool Verbose { get; init; }

        internal bool TestEnabled { get; init; }

        internal IReadOnlyList<SupportedQueries> Queries { get; init; } = new List<SupportedQueries>();

        internal string? LatestSdkToTest { get; init; }

        internal bool DiffMode { get; init; }

        internal FileInfo? DiffOverrideSearchCacheLocation { get; init; }

        internal FileInfo? DiffOverrideKnownPackagesLocation { get; init; }

    }
}
