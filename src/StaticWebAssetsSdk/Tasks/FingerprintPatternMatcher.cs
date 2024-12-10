// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

internal class FingerprintPatternMatcher
{
    private const string DefaultFingerprintExpression = "#[.{fingerprint}]?";

    private readonly TaskLoggingHelper _log;
    private readonly Dictionary<string, string> _tokensByPattern;
    private readonly StaticWebAssetGlobMatcher _matcher;

    public FingerprintPatternMatcher(
        TaskLoggingHelper log,
        ITaskItem[] fingerprintPatterns)
    {
        var tokensByPattern = fingerprintPatterns
            .ToDictionary(
                p => p.GetMetadata("Pattern"),
                p => p.GetMetadata("Expression") is string expr and not "" ? expr : DefaultFingerprintExpression);

        _log = log;
        _tokensByPattern = tokensByPattern;

        var builder = new StaticWebAssetGlobMatcherBuilder();
        foreach (var pattern in fingerprintPatterns)
        {
            builder.AddIncludePatterns(pattern.GetMetadata("Pattern"));
        }

        _matcher = builder.Build();
    }

    public string AppendFingerprintPattern(StaticWebAssetGlobMatcher.MatchContext context, string identity)
    {
        var relativePathCandidateMemory = context.PathString.AsMemory();
        if (AlreadyContainsFingerprint(relativePathCandidateMemory, identity))
        {
            return relativePathCandidateMemory.ToString();
        }

        var (directoryName, fileName, fileNamePrefix, extension) =
#if NET9_0_OR_GREATER
            ComputeFingerprintFragments(relativePathCandidateMemory);
#else
            ComputeFingerprintFragments(context.PathString);
#endif

        context.SetPathAndReinitialize(fileName);
        var matchResult = _matcher.Match(context);
        if (!matchResult.IsMatch)
        {
#if NET9_0_OR_GREATER
            var result = Path.Combine(directoryName.ToString(), $"{fileNamePrefix}{DefaultFingerprintExpression}{extension}");
#else
            var result = Path.Combine(directoryName, $"{fileNamePrefix}{DefaultFingerprintExpression}{extension}");
#endif
            _log.LogMessage(MessageImportance.Low, "Fingerprinting asset '{0}' as '{1}' because it didn't match any pattern", relativePathCandidateMemory, result);

            return result;
        }
        else
        {
            if (!_tokensByPattern.TryGetValue(matchResult.Pattern, out var expression))
            {
                throw new InvalidOperationException($"No expression found for pattern '{matchResult.Pattern}'");
            }
            else
            {
                var stem = GetMatchStem(fileName, matchResult.Pattern.AsMemory().Slice(2));
                var matchExtension = GetMatchExtension(fileName, stem);

                var simpleExtensionResult = Path.Combine(directoryName.ToString(), $"{stem}{expression}{matchExtension}");
                _log.LogMessage(MessageImportance.Low, "Fingerprinting asset '{0}' as '{1}'", relativePathCandidateMemory, simpleExtensionResult);
                return simpleExtensionResult;
            }
        }

        static bool AlreadyContainsFingerprint(ReadOnlyMemory<char> relativePathCandidate, string identity)
        {
            if (MemoryExtensions.Contains(relativePathCandidate.Span, "#[".AsSpan(), StringComparison.Ordinal))
            {
                var pattern = StaticWebAssetPathPattern.Parse(relativePathCandidate, identity);
                foreach (var segment in pattern.Segments)
                {
                    foreach (var part in segment.Parts)
                    {
                        foreach (var name in segment.GetTokenNames())
                        {
                            if (MemoryExtensions.Equals(name.Span, "fingerprint".AsSpan(), StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

#if NET9_0_OR_GREATER
        static ReadOnlySpan<char> GetMatchExtension(ReadOnlySpan<char> relativePathCandidateMemory, ReadOnlySpan<char> stem) =>
            relativePathCandidateMemory.Slice(stem.Length);
        static ReadOnlySpan<char> GetMatchStem(ReadOnlySpan<char> relativePathCandidateMemory, ReadOnlyMemory<char> pattern) =>
            relativePathCandidateMemory.Slice(0, relativePathCandidateMemory.Length - pattern.Length - 1);
#else
        static ReadOnlyMemory<char> GetMatchExtension(ReadOnlyMemory<char> relativePathCandidateMemory, ReadOnlyMemory<char> stem) =>
            relativePathCandidateMemory.Slice(stem.Length);
        static ReadOnlyMemory<char> GetMatchStem(ReadOnlyMemory<char> relativePathCandidateMemory, ReadOnlyMemory<char> pattern) =>
            relativePathCandidateMemory.Slice(0, relativePathCandidateMemory.Length - pattern.Length - 1);
#endif
    }

#if NET9_0_OR_GREATER
    private static FingerprintFragments ComputeFingerprintFragments(
        ReadOnlyMemory<char> relativePathCandidate)
    {
        var fileName = Path.GetFileName(relativePathCandidate.Span);
        var directoryName = Path.GetDirectoryName(relativePathCandidate.Span);
        var stem = Path.GetFileNameWithoutExtension(relativePathCandidate.Span);
        var extension = Path.GetExtension(relativePathCandidate.Span);

        return new(directoryName, fileName, stem, extension);
    }
#else
    private static (string directoryName, ReadOnlyMemory<char> fileName, ReadOnlyMemory<char> fileNamePrefix, ReadOnlyMemory<char> extension) ComputeFingerprintFragments(
        string relativePathCandidate)
    {
        var fileName = Path.GetFileName(relativePathCandidate).AsMemory();
        var directoryName = Path.GetDirectoryName(relativePathCandidate);
        var stem = Path.GetFileNameWithoutExtension(relativePathCandidate).AsMemory();
        var extension = Path.GetExtension(relativePathCandidate).AsMemory();

        return (directoryName, fileName, stem, extension);
    }
#endif

    private ref struct FingerprintFragments
    {
        public ReadOnlySpan<char> DirectoryName;
        public ReadOnlySpan<char> FileName;
        public ReadOnlySpan<char> FileNamePrefix;
        public ReadOnlySpan<char> Extension;

        public FingerprintFragments(ReadOnlySpan<char> directoryName, ReadOnlySpan<char> fileName, ReadOnlySpan<char> fileNamePrefix, ReadOnlySpan<char> extension)
        {
            DirectoryName = directoryName;
            FileName = fileName;
            FileNamePrefix = fileNamePrefix;
            Extension = extension;
        }

        public void Deconstruct(out ReadOnlySpan<char> directoryName, out ReadOnlySpan<char> fileName, out ReadOnlySpan<char> fileNamePrefix, out ReadOnlySpan<char> extension)
        {
            directoryName = DirectoryName;
            fileName = FileName;
            fileNamePrefix = FileNamePrefix;
            extension = Extension;
        }
    }

    private class FingerprintPattern(ITaskItem pattern)
    {
        StaticWebAssetGlobMatcher _matcher;
        public string Name { get; set; } = pattern.ItemSpec;

        public string Pattern { get; set; } = pattern.GetMetadata(nameof(Pattern));

        public string Expression { get; set; } = pattern.GetMetadata(nameof(Expression));

        public StaticWebAssetGlobMatcher Matcher => _matcher ??= new StaticWebAssetGlobMatcherBuilder().AddIncludePatterns(Pattern).Build();
    }
}
