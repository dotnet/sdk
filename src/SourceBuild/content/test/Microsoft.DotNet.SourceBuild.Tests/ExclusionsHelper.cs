// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileSystemGlobbing;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.Tests;

internal class ExclusionsHelper
{
    private const string NullSuffix = "NULL_SUFFIX";

    private readonly string _exclusionsFileName;

    private readonly string _baselineSubDir;

    // Use this to narrow down the scope of exclusions to a specific category.
    // For instance, setting this to "test-templates" will consider 
    // "src/test-templates/exclusions.txt" but not "src/arcade/exclusions.txt".
    private readonly Regex? _exclusionRegex;

    private readonly Dictionary<string, HashSet<string>> _suffixToExclusions;

    private readonly Dictionary<string, HashSet<string>> _suffixToUnusedExclusions;

    public ExclusionsHelper(string exclusionsFileName, string baselineSubDir = "", string? exclusionRegexString = null)
    {
        if (exclusionsFileName is null)
        {
            throw new ArgumentNullException(nameof(exclusionsFileName));
        }

        _exclusionsFileName = exclusionsFileName;
        _baselineSubDir = baselineSubDir;
        _exclusionRegex = string.IsNullOrWhiteSpace(exclusionRegexString) ? null : new Regex(exclusionRegexString);
        _suffixToExclusions = ParseExclusionsFile();
        _suffixToUnusedExclusions = new Dictionary<string, HashSet<string>>(
            _suffixToExclusions.ToDictionary(pair => pair.Key, pair => new HashSet<string>(pair.Value)));
    }

    internal bool IsFileExcluded(string filePath, string suffix = NullSuffix)
    {
        if (suffix is null)
        {
            throw new ArgumentNullException(nameof(suffix));
        }

        // If a specific suffix is provided, check that first. If it is not found, check the default suffix.
        return CheckAndRemoveIfExcluded(filePath, suffix) ||
            (suffix != NullSuffix && CheckAndRemoveIfExcluded(filePath, NullSuffix));
    }

    /// <summary>
    /// Generates a new baseline file with the exclusions that were used during the test run.
    /// <param name="updatedFileTag">Optional tag to append to the updated file name.</param>
    /// <param name="additionalLines">Optional additional lines to append to the updated file.</param>
    /// </summary>
    internal void GenerateNewBaselineFile(string? updatedFileTag = null, List<string>? additionalLines = null)
    {
        string exclusionsFilePath = BaselineHelper.GetBaselineFilePath(_exclusionsFileName, _baselineSubDir);

        string[] lines = File.ReadAllLines(exclusionsFilePath);

        var newLines = lines
            .Select(line => UpdateExclusionsLine(line))
            .Where(line => line is not null);

        if (additionalLines is not null)
        {
            newLines = newLines.Concat(additionalLines);
        }

        string updatedFileName = updatedFileTag is null
            ? $"Updated{_exclusionsFileName}"
            : $"Updated{Path.GetFileNameWithoutExtension(_exclusionsFileName)}.{updatedFileTag}{Path.GetExtension(_exclusionsFileName)}";
        string actualFilePath = Path.Combine(Config.LogsDirectory, updatedFileName);
        File.WriteAllLines(actualFilePath, newLines!);
    }

    private bool CheckAndRemoveIfExcluded(string filePath, string suffix = NullSuffix)
    {
        if (_suffixToExclusions.TryGetValue(suffix, out HashSet<string>? suffixExclusionList))
        {
            foreach (string exclusion in suffixExclusionList)
            {
                Matcher matcher = new();
                matcher.AddInclude(exclusion);
                if (matcher.Match(filePath).HasMatches)
                {
                    RemoveUsedExclusion(exclusion, suffix);
                    return true;
                }
            }
        }
        return false;
    }

    private Dictionary<string, HashSet<string>> ParseExclusionsFile()
    {
        string exclusionsFilePath = BaselineHelper.GetBaselineFilePath(_exclusionsFileName, _baselineSubDir);
        return File.ReadAllLines(exclusionsFilePath)
            .Select(line =>
            {
                // Ignore comments
                var index = line.IndexOf('#');
                return index >= 0 ? line[..index].TrimEnd() : line;
            })
            .Where(line => !string.IsNullOrEmpty(line))
            .Select(line => line.Split('|'))
            .Where(parts =>
            {
                // Only include exclusions that match the exclusion regex
                return _exclusionRegex is null || _exclusionRegex.IsMatch(parts[0]);
            })
            .SelectMany(parts =>
            {
                // Create a new object for each suffix
                return parts.Length == 1
                    ? new[] { new { Exclusion = parts[0], Suffix = NullSuffix } }
                    : parts[1].Split(',').Select(suffix => new { Exclusion = parts[0], Suffix = suffix.Trim() });
            })
            .GroupBy(
                parts => parts.Suffix,
                parts => parts.Exclusion
            )
            .ToDictionary(
                group => group.Key,
                group => new HashSet<string>(group)
            );
    }

    private void RemoveUsedExclusion(string exclusion, string suffix)
    {
        if (_suffixToUnusedExclusions.TryGetValue(suffix, out HashSet<string>? exclusions))
        {
            exclusions.Remove(exclusion);
        }
    }

    private string? UpdateExclusionsLine(string line)
    {
        string[] parts = line.Split('|');
        string exclusion = parts[0];
        var unusedSuffixes = _suffixToUnusedExclusions.Where(pair => pair.Value.Contains(exclusion)).Select(pair => pair.Key).ToList();

        if (!unusedSuffixes.Any())
        {
            // Exclusion is used in all suffixes, so we can keep it as is
            return line;
        }

        if (parts.Length == 1)
        {
            if (unusedSuffixes.Contains(NullSuffix))
            {
                // Exclusion is unused in the default suffix, so we can remove it entirely
                return null;
            }
            // Line is duplicated for other suffixes, but null suffix is used so we can keep it as is
            return line;
        }

        string suffixString = parts[1].Split('#')[0];
        var originalSuffixes = suffixString.Split(',').Select(suffix => suffix.Trim()).ToList();
        var newSuffixes = originalSuffixes.Except(unusedSuffixes).ToList();

        if (newSuffixes.Count == 0)
        {
            // All suffixes were unused, so we can remove the line entirely
            return null;
        }

        return line.Replace(suffixString, string.Join(",", newSuffixes));
    }
}