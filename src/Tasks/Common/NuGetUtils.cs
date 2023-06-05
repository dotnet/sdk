﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//Microsoft.NET.Build.Extensions.Tasks (net7.0) has nullables disabled
#pragma warning disable IDE0240 // Remove redundant nullable directive
#nullable disable
#pragma warning restore IDE0240 // Remove redundant nullable directive

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.NET.Build.Tasks
{
    internal static partial class NuGetUtils
    {
        /// <summary>
        /// Gets PackageId from sourcePath.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public static string GetPackageIdFromSourcePath(string sourcePath)
        {
            string packageId, unused;
            GetPackageParts(sourcePath, out packageId, out unused);
            return packageId;
        }

        /// <summary>
        /// Gets PackageId and package subpath from source path
        /// </summary>
        /// <param name="fullPath">full path to package file</param>
        /// <param name="packageId">package ID</param>
        /// <param name="packageSubPath">subpath of asset within package</param>
        public static void GetPackageParts(string fullPath, out string packageId, out string packageSubPath)
        {
            packageId = null;
            packageSubPath = null;
            try
            {
                // this method is just a temporary heuristic until we flow the NuGet metadata through the right items
                // https://github.com/dotnet/sdk/issues/1091

                // Don't try to recurse a relative path.
                if (!Path.IsPathRooted(fullPath))
                {
                    return;
                }

                for (var dir = Directory.GetParent(fullPath); dir != null; dir = dir.Parent)
                {
                    var nuspecs = dir.GetFiles("*.nuspec");

                    if (nuspecs.Length > 0)
                    {
                        packageId = Path.GetFileNameWithoutExtension(nuspecs[0].Name);
                        packageSubPath = fullPath.Substring(dir.FullName.Length + 1).Replace('\\', '/');
                        break;
                    }
                }
            }
            catch (Exception)
            { }

            return;

        }
    }
}
