// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.NugetSearch;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.Search;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Microsoft.DotNet.Tools.Tool.Install
{
    internal static class ParseResultExtension
    {
        public static VersionRange GetVersionRange(this ParseResult parseResult)
        {
            string packageId = parseResult.GetValue(ToolInstallCommandParser.PackageIdArgument);
            string packageVersion = parseResult.GetValue(ToolInstallCommandParser.VersionOption);
            bool prerelease = parseResult.GetValue(ToolInstallCommandParser.PrereleaseOption);

            if (!string.IsNullOrEmpty(packageVersion) && prerelease)
            {
                throw new GracefulException(
                    string.Format(
                        LocalizableStrings.PrereleaseAndVersionAreNotSupportedAtTheSameTime,
                        packageVersion));
            }

            if (prerelease)
            {
                packageVersion = "*-*";
            }

            VersionRange versionRange = null;

            // accept 'bare' versions and interpret 'bare' versions as NuGet exact versions
            if (!string.IsNullOrEmpty(packageVersion) && SemanticVersion.TryParse(packageVersion, out SemanticVersion version2))
            {
                packageVersion = "[" + packageVersion + "]";
            }

            if (!string.IsNullOrEmpty(packageVersion) && !VersionRange.TryParse(packageVersion, out versionRange))
            {
                throw new GracefulException(
                    string.Format(
                        LocalizableStrings.InvalidNuGetVersionRange,
                        packageVersion));
            }
            return versionRange;
        }
    }
}
