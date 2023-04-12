// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public sealed class CompressionConfiguration
{
    private Matcher _matcher;

    public string ItemSpec { get; set; }

    public string IncludePattern { get; set; }

    public string ExcludePattern { get; set; }

    public string Format { get; set; }

    public string Stage { get; set; }

    public static CompressionConfiguration FromTaskItem(ITaskItem taskItem)
    {
        var itemSpec = taskItem.ItemSpec;
        var includePattern = taskItem.GetMetadata(nameof(IncludePattern));
        var excludePattern = taskItem.GetMetadata(nameof(ExcludePattern));
        var format = taskItem.GetMetadata(nameof(Format));
        var stage = taskItem.GetMetadata(nameof(Stage));

        if (!CompressionFormat.IsValidCompressionFormat(format))
        {
            throw new InvalidOperationException($"Unknown compression format '{format}' for the compression configuration '{itemSpec}'.");
        }

        if (!BuildStage.IsValidBuildStage(stage))
        {
            throw new InvalidOperationException($"Unknown build stage '{stage}' for the compression configuration '{itemSpec}'.");
        }

        return new()
        {
            ItemSpec = itemSpec,
            IncludePattern = includePattern,
            ExcludePattern = excludePattern,
            Format = format,
            Stage = stage,
        };
    }

    public bool StageIncludes(string buildStage)
        => BuildStage.IsAll(Stage)
        || string.Equals(Stage, buildStage, StringComparison.OrdinalIgnoreCase);

    public ITaskItem ToTaskItem()
    {
        var taskItem = new TaskItem(ItemSpec);
        taskItem.SetMetadata(nameof(IncludePattern), IncludePattern);
        taskItem.SetMetadata(nameof(ExcludePattern), ExcludePattern);
        taskItem.SetMetadata(nameof(Format), Format);
        taskItem.SetMetadata(nameof(Stage), Stage);
        return taskItem;
    }

    public bool Matches(string relativePath)
    {
        if (_matcher is null)
        {
            _matcher = new Matcher();
            var includePatterns = SplitPattern(IncludePattern);
            var excludePatterns = SplitPattern(ExcludePattern);
            _matcher.AddIncludePatterns(includePatterns);
            _matcher.AddExcludePatterns(excludePatterns);
        }

        return _matcher.Match(relativePath).HasMatches;

        static string[] SplitPattern(string pattern)
            => string.IsNullOrEmpty(pattern) ? Array.Empty<string>() : pattern
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToArray();
    }

    public static class BuildStage
    {
        public const string Build = nameof(Build);
        public const string Publish = nameof(Publish);
        public const string All = nameof(All);

        public static bool IsBuild(string buildStage) => string.Equals(Build, buildStage, StringComparison.OrdinalIgnoreCase);
        public static bool IsPublish(string buildStage) => string.Equals(Publish, buildStage, StringComparison.OrdinalIgnoreCase);
        public static bool IsAll(string buildStage) => string.Equals(All, buildStage, StringComparison.OrdinalIgnoreCase);

        public static bool IsValidBuildStage(string buildStage)
            => IsBuild(buildStage)
            || IsPublish(buildStage)
            || IsAll(buildStage);
    }

    public static class CompressionFormat
    {
        public const string Gzip = nameof(Gzip);
        public const string Brotli = nameof(Brotli);

        public static bool IsGzip(string format) => string.Equals(Gzip, format, StringComparison.OrdinalIgnoreCase);
        public static bool IsBrotli(string format) => string.Equals(Brotli, format, StringComparison.OrdinalIgnoreCase);
        public static bool IsValidCompressionFormat(string format)
            => IsGzip(format)
            || IsBrotli(format);
    }
}
