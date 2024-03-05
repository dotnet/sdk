// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// Copied from https://github.com/dotnet/arcade/blob/db45698020f58f88eef75b23b2598a59872918f6/src/Microsoft.DotNet.VersionTools/lib/src/BuildManifest/VersionIdentifier.cs
// Conflicting MSBuild versions and some customizations make it difficult to use the Arcade assembly.
public static class VersionIdentifier
{
    private static readonly HashSet<string> _knownTags = new HashSet<string>
        {
            "alpha",
            "beta",
            "preview",
            "prerelease",
            "servicing",
            "rtm",
            "rc"
        };

    private static readonly SortedDictionary<string, string> _sequencesToReplace =
        new SortedDictionary<string, string>
        {
                { "-.", "." },
                { "..", "." },
                { "--", "-" },
                { "//", "/" },
                { "_.", "." }
        };

    private const string _finalSuffix = "final";

    private static readonly char[] _delimiters = new char[] { '.', '-', '_' };

    /// <summary>
    /// Identify the version of an asset.
    ///
    /// Asset names can come in two forms:
    /// - Blobs that include the full path
    /// - Packages that do not include any path elements.
    ///
    /// There may be multiple different version numbers in a blob path.
    /// This method starts at the last segment of the path and works backward to find a version number.
    /// </summary>
    /// <param name="assetName">Asset name</param>
    /// <returns>Version, or null if none is found.</returns>
    public static string? GetVersion(string assetName)
    {
        string[] pathSegments = assetName.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        string? potentialVersion = null;
        for (int i = pathSegments.Length - 1; i >= 0; i--)
        {
            potentialVersion = GetVersionForSingleSegment(pathSegments[i]);
            if (potentialVersion != null)
            {
                return potentialVersion;
            }
        }

        return potentialVersion;
    }

    /// <summary>
    /// Identify the version number of an asset segment.
    /// </summary>
    /// <param name="assetPathSegment">Asset segment</param>
    /// <returns>Version number, or null if none was found</returns>
    /// <remarks>
    /// Identifying versions is not particularly easy. To constrain the problem,
    /// we apply the following assumptions which are generally valid for .NET Core.
    /// - We always have major.minor.patch, and it always begins the version string.
    /// - The only pre-release or build metadata labels we use begin with the _knownTags shown above.
    /// - We use additional numbers in our version numbers after the initial
    ///   major.minor.patch-prereleaselabel.prereleaseiteration segment,
    ///   but any non-numeric element will end the version string.
    /// - The <see cref="_delimiters"/> we use in versions and file names are ., -, and _.
    /// </remarks>
    private static string? GetVersionForSingleSegment(string assetPathSegment)
    {

        // Find the start of the version number by finding the major.minor.patch.
        // Scan the string forward looking for a digit preceded by one of the delimiters,
        // then look for a minor.patch, completing the major.minor.patch.  Continue to do so until we get
        // to something that is NOT major.minor.patch (this is necessary because we sometimes see things like:
        // VS.Redist.Common.NetCore.Templates.x86.2.2.3.0.101-servicing-014320.nupkg
        // Continue iterating until we find ALL potential versions. Return the one that is the latest in the segment
        // This is to deal with files with multiple major.minor.patchs in the file name, for example:
        // Microsoft.NET.Workload.Mono.ToolChain.Manifest-6.0.100.Msi.x64.6.0.0-rc.1.21380.2.symbols.nupkg

        int currentIndex = 0;
        // Stack of major.minor.patch.
        Stack<(int versionNumber, int index)> majorMinorPatchStack = new Stack<(int, int)>(3);
        string? majorMinorPatch = null;
        int majorMinorPatchIndex = 0;
        StringBuilder versionSuffix = new StringBuilder();
        char prevDelimiterCharacter = char.MinValue;
        char nextDelimiterCharacter = char.MinValue;
        Dictionary<int, string> majorMinorPatchDictionary = new Dictionary<int, string>();
        while (true)
        {
            string nextSegment;
            prevDelimiterCharacter = nextDelimiterCharacter;
            int nextDelimiterIndex = assetPathSegment.IndexOfAny(_delimiters, currentIndex);
            if (nextDelimiterIndex != -1)
            {
                nextDelimiterCharacter = assetPathSegment[nextDelimiterIndex];
                nextSegment = assetPathSegment.Substring(currentIndex, nextDelimiterIndex - currentIndex);
            }
            else
            {
                nextSegment = assetPathSegment.Substring(currentIndex);
            }

            // If we have not yet found the major/minor/patch, then there are four cases:
            // - There have been no potential major/minor/patch numbers found and the current segment is a number. Push onto the majorMinorPatch stack
            //   and continue.
            // - There has been at least one number found, but less than 3, and the current segment not a number or not preceded by '.'. In this case,
            //   we should clear out the stack and continue the search.
            // - There have been at least 2 numbers found and the current segment is a number and preceded by '.'. Push onto the majorMinorPatch stack and continue
            // - There have been at least 3 numbers found and the current segment is not a number or not preceded by '-'. In this case, we can call this the major minor
            //   patch number and no longer need to continue searching
            if (majorMinorPatch == null)
            {
                bool isNumber = int.TryParse(nextSegment, out int potentialVersionSegment);
                if ((majorMinorPatchStack.Count == 0 && isNumber) ||
                    (majorMinorPatchStack.Count > 0 && prevDelimiterCharacter == '.' && isNumber))
                {
                    majorMinorPatchStack.Push((potentialVersionSegment, currentIndex));
                }
                // Check for partial major.minor.patch cases, like: 2.2.bar or 2.2-100.bleh
                else if (majorMinorPatchStack.Count > 0 && majorMinorPatchStack.Count < 3 &&
                         (prevDelimiterCharacter != '.' || !isNumber))
                {
                    majorMinorPatchStack.Clear();
                }

                // Determine whether we are done with major.minor.patch after this update.
                if (majorMinorPatchStack.Count >= 3 && (prevDelimiterCharacter != '.' || !isNumber || nextDelimiterIndex == -1))
                {
                    // Done with major.minor.patch, found. Pop the top 3 elements off the stack.
                    (int patch, int patchIndex) = majorMinorPatchStack.Pop();
                    (int minor, int minorIndex) = majorMinorPatchStack.Pop();
                    (int major, int majorIndex) = majorMinorPatchStack.Pop();
                    majorMinorPatch = $"{major}.{minor}.{patch}";
                    majorMinorPatchIndex = majorIndex;
                }
            }

            // Don't use else, so that we don't miss segments
            // in case we are just deciding that we've finished major minor patch.
            if (majorMinorPatch != null)
            {
                // Now look at the next segment. If it looks like it could be part of a version, append to what we have
                // and continue. If it can't, then we're done.
                //
                // Cases where we should break out and be done:
                // - We have an empty pre-release label and the delimiter is not '-'.
                // - We have an empty pre-release label and the next segment does not start with a known tag.
                // - We have a non-empty pre-release label and the current segment is not a number and also not 'final'
                //      A corner case of versioning uses .final to represent a non-date suffixed final pre-release version:
                //      3.1.0-preview.10.final
                if (versionSuffix.Length == 0 &&
                    (prevDelimiterCharacter != '-' || !_knownTags.Any(tag => nextSegment.StartsWith(tag, StringComparison.OrdinalIgnoreCase))))
                {
                    majorMinorPatchDictionary.Add(majorMinorPatchIndex, majorMinorPatch);
                    majorMinorPatch = null;
                    versionSuffix = new StringBuilder();
                }
                else if (versionSuffix.Length != 0 && !int.TryParse(nextSegment, out int potentialVersionSegment) && nextSegment != _finalSuffix)
                {
                    majorMinorPatchDictionary.Add(majorMinorPatchIndex, $"{majorMinorPatch}{versionSuffix.ToString()}");
                    majorMinorPatch = null;
                    versionSuffix = new StringBuilder();
                }
                else
                {
                    // Append the delimiter character and then the current segment
                    versionSuffix.Append(prevDelimiterCharacter);
                    versionSuffix.Append(nextSegment);
                }
            }

            if (nextDelimiterIndex != -1)
            {
                currentIndex = nextDelimiterIndex + 1;
            }
            else
            {
                break;
            }
        }

        if (majorMinorPatch != null)
        {
            majorMinorPatchDictionary.Add(majorMinorPatchIndex, $"{majorMinorPatch}{versionSuffix.ToString()}");
        }

        if (!majorMinorPatchDictionary.Any())
        {
            return null;
        }

        int maxKey = majorMinorPatchDictionary.Keys.Max();
        return majorMinorPatchDictionary[maxKey];
    }

    /// <summary>
    ///     Given an asset name, remove all .NET Core version numbers (as defined by GetVersionForSingleSegment)
    ///     from the string
    /// </summary>
    /// <param name="assetName">Asset</param>
    /// <returns>Asset name without versions</returns>
    public static string RemoveVersions(string assetName, string replacement = "")
    {
        string[] pathSegments = assetName.Split('/');

        // Remove the version number from each segment, then join back together and
        // remove any useless character sequences.

        for (int i = 0; i < pathSegments.Length; i++)
        {
            if (!string.IsNullOrEmpty(pathSegments[i]))
            {
                string? versionForSegment = GetVersionForSingleSegment(pathSegments[i]);
                if (versionForSegment != null)
                {
                    pathSegments[i] = pathSegments[i].Replace(versionForSegment, replacement);
                }
            }
        }

        // Continue replacing things until there is nothing left to replace.
        string assetWithoutVersions = string.Join("/", pathSegments);
        bool anyReplacements = true;
        while (anyReplacements)
        {
            string replacementIterationResult = assetWithoutVersions;
            foreach (var sequence in _sequencesToReplace)
            {
                replacementIterationResult = replacementIterationResult.Replace(sequence.Key, sequence.Value);
            }
            anyReplacements = replacementIterationResult != assetWithoutVersions;
            assetWithoutVersions = replacementIterationResult;
        }

        return assetWithoutVersions;
    }


    public static bool AreVersionlessEqual(string assetName1, string assetName2)
    {
        return RemoveVersions(assetName1) == RemoveVersions(assetName2);
    }
}
