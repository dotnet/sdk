// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

public class PackageDiff
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine("Usage: PackageDiff <path-or-url-of-package1> <path-or-url-of-package2>");
            return 1;
        }

        ZipArchive package1 = await GetZipArchiveAsync(args[0]);
        ZipArchive package2 = await GetZipArchiveAsync(args[1]);
        var diff = GetDiffs(package1, package2);
        if (diff is not "")
        {
            Console.WriteLine(diff);
            return 1;
        }
        return 0;
    }

    public static async Task<ZipArchive> GetZipArchiveAsync(string arg)
    {
        if (File.Exists(arg))
        {
            return new ZipArchive(File.OpenRead(arg));
        }
        else if (Uri.TryCreate(arg, UriKind.RelativeOrAbsolute, out var uri))
        {
            var webClient = new HttpClient();
            return new ZipArchive(await webClient.GetStreamAsync(uri));
        }
        else
        {
            throw new ArgumentException($"Invalid path or url to package1: {arg}");
        }
    }

    public static string GetDiffs(ZipArchive package1, ZipArchive package2)
    {
        StringBuilder output = new();

        if (TryGetDiff(package1.Entries.Select(entry => entry.FullName).ToList(), package2.Entries.Select(entry => entry.FullName).ToList(), out var fileDiffs))
        {
            output.AppendLine("File differences:");
            output.AppendLine(string.Join(Environment.NewLine, fileDiffs.Select(d => "  " + d)));
            output.AppendLine();
        }

        if (TryGetDiff(package1.GetNuspec().Lines(), package2.GetNuspec().Lines(), out var editedDiff))
        {
            output.AppendLine("Nuspec differences:");
            output.AppendLine(string.Join(Environment.NewLine, editedDiff.Select(d => "  " + d)));
            output.AppendLine();
        }
        var dlls1 = package1.Entries.Where(entry => entry.FullName.EndsWith(".dll")).ToImmutableDictionary(entry => entry.FullName, entry => entry);
        var dlls2 = package2.Entries.Where(entry => entry.FullName.EndsWith(".dll")).ToImmutableDictionary(entry => entry.FullName, entry => entry);
        foreach (var kvp in dlls1)
        {
            var dllPath = kvp.Key;
            var dll1 = kvp.Value;
            if (dlls2.TryGetValue(dllPath, out ZipArchiveEntry? dll2))
            {
                try
                {
                    var version1 = new PEReader(dll1.Open().ReadToEnd().ToImmutableArray()).GetMetadataReader().GetAssemblyDefinition().Version.ToString();
                    var version2 = new PEReader(dll2.Open().ReadToEnd().ToImmutableArray()).GetMetadataReader().GetAssemblyDefinition().Version.ToString();
                    if (version1 != version2)
                    {
                        output.AppendLine($"Assembly {dllPath} has different versions: {version1} and {version2}");
                    }
                }
                catch (InvalidOperationException)
                { }
            }
        }
        return output.ToString();
    }

    public static bool TryGetDiff(List<string> originalLines, List<string> modifiedLines, out List<string> formattedDiff)
    {
        // Edit distance algorithm: https://en.wikipedia.org/wiki/Longest_common_subsequence

        int[,] dp = new int[originalLines.Count + 1, modifiedLines.Count + 1];

        // Initialize first row and column
        for (int i = 0; i <= originalLines.Count; i++)
        {
            dp[i, 0] = i;
        }
        for (int j = 0; j <= modifiedLines.Count; j++)
        {
            dp[0, j] = j;
        }

        // Compute edit distance
        for (int i = 1; i <= originalLines.Count; i++)
        {
            for (int j = 1; j <= modifiedLines.Count; j++)
            {
                if (string.Compare(originalLines[i - 1], modifiedLines[j - 1]) == 0)
                {
                    dp[i, j] = dp[i - 1, j - 1];
                }
                else
                {
                    dp[i, j] = 1 + Math.Min(dp[i - 1, j], dp[i, j - 1]);
                }
            }
        }

        // Trace back the edits
        int row = originalLines.Count;
        int col = modifiedLines.Count;

        formattedDiff = [];
        while (row > 0 || col > 0)
        {
            if (row > 0 && col > 0 && string.Compare(originalLines[row - 1], modifiedLines[col - 1]) == 0)
            {
                formattedDiff.Add("  " + originalLines[row - 1]);
                row--;
                col--;
            }
            else if (col > 0 && (row == 0 || dp[row, col - 1] <= dp[row - 1, col]))
            {
                formattedDiff.Add("+ " + modifiedLines[col - 1]);
                col--;
            }
            else if (row > 0 && (col == 0 || dp[row, col - 1] > dp[row - 1, col]))
            {
                formattedDiff.Add("- " + originalLines[row - 1]);
                row--;
            }
            else
            {
                throw new Exception("Unreachable code");
            }
        }
        formattedDiff.Reverse();
        return dp[originalLines.Count, modifiedLines.Count] != 0;
    }

}
