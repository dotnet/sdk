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
        public CommandArgs(
                DirectoryInfo basePath,
                bool allowPreviewPacks,
                int pageSize,
                bool onePage,
                bool savePacks,
                bool noTemplateJsonFilter,
                IEnumerable<SupportedQueries>? queries,
                DirectoryInfo? packagesPath
            )
        {
            OutputPath = basePath ?? throw new ArgumentNullException(nameof(basePath));
            IncludePreviewPacks = allowPreviewPacks;
            PageSize = pageSize;
            RunOnlyOnePage = onePage;
            SaveCandidatePacks = savePacks;
            DontFilterOnTemplateJson = noTemplateJsonFilter;
            Queries = queries?.ToArray() ?? Array.Empty<SupportedQueries>();
            LocalPackagePath = packagesPath;
        }

        internal DirectoryInfo? LocalPackagePath { get; }

        internal DirectoryInfo OutputPath { get; }

        internal int PageSize { get; }

        internal bool SaveCandidatePacks { get; }

        internal bool RunOnlyOnePage { get; }

        internal bool IncludePreviewPacks { get; }

        internal bool DontFilterOnTemplateJson { get; }

        internal IReadOnlyList<SupportedQueries> Queries { get; }
    }
}
