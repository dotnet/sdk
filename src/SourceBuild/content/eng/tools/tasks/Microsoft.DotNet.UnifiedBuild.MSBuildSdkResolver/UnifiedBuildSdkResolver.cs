// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using Microsoft.Build.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.UnifiedBuild.Tasks
{
    /// <summary>
    /// Extends the SDK to handle "SOURCE_BUILT_SDK_*" override environment variables. Each override
    /// should provide a set of 3 environment variables:
    ///
    /// SOURCE_BUILT_SDK_ID_EXAMPLE=Your.Sdk.Example
    ///   ID of the SDK nuget package to override.
    /// 
    /// SOURCE_BUILT_SDK_DIR_EXAMPLE=/git/repo/bin/extracted/Your.Sdk.Example/
    ///   Directory where the sdk/Sdk.props and/or sdk/Sdk.targets files are located. This should be
    ///   the directory where the override SDK package is extracted.
    /// 
    /// SOURCE_BUILT_SDK_VERSION_EXAMPLE=1.0.0-source-built
    ///   (Optional) Version of the SDK package to use. This is informational.
    /// </summary>
    public class UnifiedBuildSdkResolver : SdkResolver
    {
        public override string Name => nameof(UnifiedBuildSdkResolver);

        public override int Priority => 0;

        public override SdkResult Resolve(
            SdkReference sdkReference,
            SdkResolverContext resolverContext,
            SdkResultFactory factory)
        {
            string sdkDescription = sdkReference.Name;
            if (!string.IsNullOrEmpty(sdkReference.Version))
            {
                sdkDescription += $" {sdkReference.Version}";
            }
            if (!string.IsNullOrEmpty(sdkReference.MinimumVersion))
            {
                sdkDescription += $" (>= {sdkReference.MinimumVersion})";
            }

            SourceBuiltSdkOverride[] overrides = Environment.GetEnvironmentVariables()
                .Cast<DictionaryEntry>()
                .Select(SourceBuiltSdkOverride.Create)
                .Where(o => o != null)
                .ToArray();

            void LogMessage(string message)
            {
                resolverContext.Logger.LogMessage($"[{Name}] {message}", MessageImportance.Low);
            }

            if (overrides.Any())
            {
                string separator = overrides.Length == 1 ? " " : Environment.NewLine;

                LogMessage(
                    $"Looking for SDK {sdkDescription}. Detected config(s) in env:{separator}" +
                    string.Join(Environment.NewLine, overrides.Select(o => o.ToString())));
            }

            SourceBuiltSdkOverride[] matches = overrides
                .Where(o => sdkReference.Name.Equals(o?.Id, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var unresolvableReasons = new List<string>();

            if (matches.Length != 1)
            {
                unresolvableReasons.Add(
                    $"{matches.Length} overrides found for '{sdkReference.Name}'");
            }
            else
            {
                SourceBuiltSdkOverride match = matches[0];
                string[] matchProblems = match.GetValidationErrors().ToArray();

                if (matchProblems.Any())
                {
                    unresolvableReasons.Add($"Found match '{match.Group}' with problems:");
                    unresolvableReasons.AddRange(matchProblems);
                }
                else
                {
                    LogMessage($"Overriding {sdkDescription} with '{match.Group}'");

                    return factory.IndicateSuccess(match.SdkDir, match.Version);
                }
            }

            return factory.IndicateFailure(unresolvableReasons.Select(r => $"[{Name}] {r}"));
        }

        /// <summary>
        /// Takes a directory path (not ending in a separator) and determines if it is the "sdk"
        /// directory inside a SDK package with a case-insensitive comparison.
        /// </summary>
        private static bool IsSdkDirectory(string path)
        {
            return Path.GetFileName(path).Equals("sdk", StringComparison.OrdinalIgnoreCase);
        }

        private class SourceBuiltSdkOverride
        {
            private const string EnvPrefix = "SOURCE_BUILT_SDK_";
            private const string EnvId = EnvPrefix + "ID_";
            private const string EnvVersion = EnvPrefix + "VERSION_";
            private const string EnvDir = EnvPrefix + "DIR_";

            public static SourceBuiltSdkOverride Create(DictionaryEntry entry)
            {
                if (entry.Key is string key && key.StartsWith(EnvId))
                {
                    // E.g. "ARCADE" from "SOURCE_BUILD_SDK_ID_ARCADE=Microsoft.DotNet.Arcade.Sdk".
                    string group = key.Substring(EnvId.Length);

                    if (string.IsNullOrEmpty(group))
                    {
                        return null;
                    }

                    string id = entry.Value as string;
                    string version = Environment.GetEnvironmentVariable(EnvVersion + group);
                    string dir = Environment.GetEnvironmentVariable(EnvDir + group);

                    if (string.IsNullOrEmpty(version))
                    {
                        version = "1.0.0-source-built";
                    }

                    string sdkDir = null;
                    if (!string.IsNullOrEmpty(dir))
                    {
                        sdkDir = Directory.EnumerateDirectories(dir).FirstOrDefault(IsSdkDirectory);
                    }

                    return new SourceBuiltSdkOverride
                    {
                        Group = group,
                        Id = id,
                        Version = version,
                        Dir = dir,
                        SdkDir = sdkDir,
                    };
                }
                return null;
            }

            /// <summary>
            /// Name of the environment variable group, used to associate the multiple env vars.
            /// </summary>
            public string Group { get; set; }

            /// <summary>
            /// ID of the SDK package.
            /// </summary>
            public string Id { get; set; }

            /// <summary>
            /// Version of the source-built SDK package to use instead.
            /// </summary>
            public string Version { get; set; }

            /// <summary>
            /// Directory where the source-built SDK files are found, in extracted form.
            /// </summary>
            public string Dir { get; set; }

            /// <summary>
            /// Directory where the Sdk.props/Sdk.targets files are found, inside Dir.
            /// </summary>
            public string SdkDir { get; set; }

            public override string ToString() => $"'{Group}' for '{Id}/{Version}' at '{Dir}'";

            public IEnumerable<string> GetValidationErrors()
            {
                if (string.IsNullOrEmpty(Id))
                {
                    yield return $"'{EnvId}{Group}' not specified.";
                }
                if (string.IsNullOrEmpty(Dir))
                {
                    yield return $"'{EnvDir}{Group}' not specified.";
                }
                else if (string.IsNullOrEmpty(SdkDir))
                {
                    yield return $"Didn't find any 'sdk' directory in SDK package dir '{Dir}'";
                }
            }
        }
    }
}
