// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.BuildManifest;
using NuGet.Packaging;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.CommandLine;
using System.Formats.Tar;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
    
public static class Utils
{
    /// <summary>
    ///  Removes version identifiers from a path.
    /// </summary>
    /// <param name="path"></param>
    public static string RemoveVersionsNormalized(this string path)
    {
        string strippedPath = path.Replace("\\", "//");
        string prevPath = path;
        do
        {
            prevPath = strippedPath;
            strippedPath = VersionIdentifier.RemoveVersions(strippedPath);
        } while (prevPath != strippedPath);

        return strippedPath;
    }

    /// <summary>
    /// Removes ignorable package files from the list of files.
    /// </summary>
    /// <param name="files">The list of files to filter.</param>
    public static IEnumerable<string> RemovePackageFilesToIgnore(IEnumerable<string> files)
        => files.Where(f => !IsIgnorablePackageFile(f));

    /// <summary>
    /// Determines if a package file should be ignored.
    /// </summary>
    public static bool IsIgnorablePackageFile(string filePath)
        => filePath.EndsWith(".signature.p7s", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".psmdcp", StringComparison.OrdinalIgnoreCase);
}
