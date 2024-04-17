// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Enumeration;
using System.Linq;

namespace Microsoft.DotNet.UnifiedBuild.Tests;

public class Exclusions
{
    public const string UbPrefix = "ub";
    public const string MsftPrefix = "msft";

    public Exclusions(string rid)
    {
        _rid = rid;
    }

    string _rid;

    string[] GetRidSpecificExclusionFileNames(string path)
    {
        string filename = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        Debug.Assert(path == $"{filename}{extension}", $"{path} != {filename}{extension}");
        string[] parts = _rid.Split('-');
        string[] fileNames = new string[parts.Length+1];
        fileNames[0] = $"{filename}{extension}";
        for (int i = 1; i < parts.Length; i++)
        {
            fileNames[i] = $"{filename}-{string.Join('-', parts[..i])}-any{extension}";
        }
        fileNames[parts.Length] = $"{filename}-{_rid}{extension}";
        return fileNames;
    }

    public IEnumerable<string> RemoveContentDiffFileExclusions(IEnumerable<string> files, string? prefix = null)
    {
        var exclusions = GetFileExclusions(prefix);
        Func<string, bool> condition = f => !IsFileExcluded(f, exclusions, prefix);
        return files.Where(condition);
    }

    public IEnumerable<string> RemoveAssemblyVersionFileExclusions(IEnumerable<string> files, string? prefix = null)
    {
        var exclusions = GetFileExclusions(prefix).Concat(GetNativeDllExclusions(prefix)).Concat(GetAssemblyVersionExclusions(prefix));
        Func<string, bool> condition = f => !IsFileExcluded(f, exclusions, prefix);
        return files.Where(condition);
    }

    public List<string> GetFileExclusions(string? prefix = null) => GetRidSpecificExclusionFileNames("SdkFileDiffExclusions.txt").SelectMany(f => Utilities.TryParseExclusionsFile(f, prefix)).ToList();
    public List<string> GetAssemblyVersionExclusions(string? prefix = null) => GetRidSpecificExclusionFileNames("SdkAssemblyVersionDiffExclusions.txt").SelectMany(f => Utilities.TryParseExclusionsFile(f, prefix)).ToList();
    public List<string> GetNativeDllExclusions(string? prefix = null) => GetRidSpecificExclusionFileNames("NativeDlls.txt").SelectMany(f => Utilities.TryParseExclusionsFile(f, prefix)).ToList();
    public string GetBaselineFileDiffFileName() => GetRidSpecificExclusionFileNames("MsftToUbSdkFiles.diff").Last();

    static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    public bool IsFileExcluded(string file, IEnumerable<string> exclusions, string? prefix = null)
        => exclusions.Any(exclusion => FileSystemName.MatchesSimpleExpression(exclusion, NormalizePath(file)));
}
