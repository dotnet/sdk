// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using TestUtilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.Tests;

/// <summary>
/// Scans the VMR for licenses and compares them to a baseline. This ensures that only open-source licenses are used for relevant files.
/// </summary>
/// <remarks>
/// Each sub-repo of the VMR is scanned separately because of the amount of time it takes.
/// When scanning is run, the test provides a list of files for the scanner to ignore. These include binary file types. It also includes
/// .il/.ildump file types which are massive, causing the scanner to choke and don't include license references anyway.
/// Once the scanner returns the results, a filtering process occurs. First, any detected license that is in the allowed list of licenses
/// is filtered out. The test defines a list of such licenses that all represent open-source licenses. Next, a license exclusions file is
/// applied to the filtering. This file contains a set of paths for which certain detected licenses are to be ignored. Such a path can be
/// defined to ignore all detected licenses or specific ones. These exclusions are useful for ignoring false positives where the scanning
/// tool has detected something in the file that makes it think it's a license reference when that's not actually the intent. Other cases
/// that are excluded are when the license is meant as configuration or test data and not actually applying to the code. These exclusions
/// further filter down the set of the detected licenses for each file. Everything that's left at this point is reported. It gets compared
/// to a baseline file (which is defined for each sub-repo). If the filtered results differ from what's defined in the baseline, the test fails.
/// 
/// Rules for determining how to resolve a detected license:
///   1. If it's an allowed open-source license, add it to the list of allowed licenses in LicenseScanTests.cs.
///   2. If the file shouldn't be scanned as a general rule because of its file type (e.g. image file), add it to the list of excluded file types in LicenseScanTests.cs.
///   3. Add it to LicenseExclusions.txt if the referenced license is one of the following:
///     a. Not applicable (e.g. test data)
///     b. False positive
///   4. If the license is not allowed for open-souce, the license needs to be fixed. Everything else should go in the baseline file.
/// </remarks>
public class LicenseScanTests : TestBase
{
    private const string BaselineSubDir = nameof(LicenseScanTests);

    private static readonly string[] s_allowedLicenseExpressions = new string[]
    {
        "apache-1.1", // https://opensource.org/license/apache-1-1/
        "apache-2.0", // https://opensource.org/license/apache-2-0/
        "apache-2.0 WITH apple-runtime-library-exception", // https://github.com/nexB/scancode-toolkit/blob/develop/src/licensedcode/data/licenses/apple-runtime-library-exception.LICENSE
        "apache-2.0 WITH llvm-exception", // https://spdx.org/licenses/LLVM-exception.html
        "apsl-2.0", // https://opensource.org/license/apsl-2-0-php/
        "blueoak-1.0.0", // https://blueoakcouncil.org/license/1.0.0
        "boost-1.0", // https://opensource.org/license/bsl-1-0/
        "bsd-new", // https://opensource.org/license/BSD-3-clause/
        "bsd-original", // https://github.com/nexB/scancode-toolkit/blob/develop/src/licensedcode/data/licenses/bsd-original.LICENSE
        "bsd-original-uc", // https://github.com/nexB/scancode-toolkit/blob/develop/src/licensedcode/data/licenses/bsd-original-uc.LICENSE
        "bsd-simplified", // https://opensource.org/license/bsd-2-clause/
        "bsd-zero", // https://opensource.org/license/0bsd/
        "bytemark", // https://github.com/nexB/scancode-toolkit/blob/develop/src/licensedcode/data/licenses/bytemark.LICENSE
        "bzip2-libbzip-2010", // https://github.com/nexB/scancode-toolkit/blob/develop/src/licensedcode/data/licenses/bzip2-libbzip-2010.LICENSE
        "cc0-1.0", // https://creativecommons.org/publicdomain/zero/1.0/legalcode
        "cc-by-3.0", // https://creativecommons.org/licenses/by/3.0/legalcode
        "cc-by-3.0-us", // https://creativecommons.org/licenses/by/3.0/us/legalcode
        "cc-by-4.0", // https://creativecommons.org/licenses/by/4.0/legalcode
        "cc-by-sa-3.0", // https://creativecommons.org/licenses/by-sa/3.0/legalcode
        "cc-by-sa-4.0", // https://creativecommons.org/licenses/by-sa/4.0/legalcode
        "cc-pd", // https://creativecommons.org/publicdomain/mark/1.0/
        "cc-sa-1.0", // https://github.com/nexB/scancode-toolkit/blob/develop/src/licensedcode/data/licenses/cc-sa-1.0.LICENSE
        "epl-1.0", // https://opensource.org/license/epl-1-0/
        "generic-cla", // https://github.com/nexB/scancode-toolkit/blob/develop/src/licensedcode/data/licenses/generic-cla.LICENSE
        "gpl-1.0-plus", // https://opensource.org/license/gpl-1-0/
        "gpl-2.0", // https://opensource.org/license/gpl-2-0/
        "gpl-3.0", // https://opensource.org/license/gpl-3-0/
        "ietf", // https://github.com/nexB/scancode-toolkit/blob/develop/src/licensedcode/data/licenses/ietf.LICENSE
        "gpl-2.0-plus WITH autoconf-simple-exception-2.0", // https://github.com/nexB/scancode-toolkit/blob/develop/src/licensedcode/data/rules/gpl-2.0-plus_with_autoconf-simple-exception-2.0_8.RULE
        "gpl-2.0 WITH gcc-linking-exception-2.0", // https://github.com/nexB/scancode-toolkit/blob/develop/src/licensedcode/data/rules/gpl-2.0_with_gcc-linking-exception-2.0_6.RULE
        "gpl-3.0-plus WITH bison-exception-2.2", // https://github.com/nexB/scancode-toolkit/blob/develop/src/licensedcode/data/rules/gpl-3.0-plus_with_bison-exception-2.2_7.RULE
        "isc", // https://opensource.org/license/isc-license-txt/
        "iso-8879", // https://github.com/nexB/scancode-toolkit/blob/develop/src/licensedcode/data/licenses/iso-8879.LICENSE
        "lgpl-2.0-plus", // https://opensource.org/license/lgpl-2-0/
        "lgpl-2.1", // https://opensource.org/license/lgpl-2-1/
        "lgpl-2.1-plus", // https://opensource.org/license/lgpl-2-1/
        "lgpl-3.0", // https://opensource.org/license/lgpl-3-0/
        "llvm-exception", // https://spdx.org/licenses/LLVM-exception.html
        "lzma-sdk-9.22", // https://github.com/nexB/scancode-toolkit/blob/develop/src/licensedcode/data/licenses/lzma-sdk-9.22.LICENSE
        "mit", // https://opensource.org/license/mit/
        "mit-addition", // https://github.com/nexB/scancode-toolkit/blob/develop/src/licensedcode/data/licenses/mit-addition.LICENSE
        "mit-testregex", // https://github.com/nexB/scancode-toolkit/blob/develop/src/licensedcode/data/licenses/mit-testregex.LICENSE
        "ms-patent-promise", // https://github.com/nexB/scancode-toolkit/blob/develop/src/licensedcode/data/licenses/ms-patent-promise.LICENSE
        "ms-lpl", // https://github.com/nexB/scancode-toolkit/blob/develop/src/licensedcode/data/licenses/ms-lpl.LICENSE
        "ms-pl", // https://opensource.org/license/ms-pl-html/
        "ms-rl", // https://opensource.org/license/ms-rl-html/
        "newton-king-cla", // https://github.com/nexB/scancode-toolkit/blob/develop/src/licensedcode/data/licenses/newton-king-cla.LICENSE
        "ngpl", // https://opensource.org/license/nethack-php/
        "object-form-exception-to-mit", // https://github.com/nexB/scancode-toolkit/blob/develop/src/licensedcode/data/licenses/object-form-exception-to-mit.LICENSE
        "ofl-1.1", // https://opensource.org/license/ofl-1-1/
        "osf-1990", // https://fedoraproject.org/wiki/Licensing:MIT?rd=Licensing/MIT#HP_Variant
        "pcre2-exception", // https://github.com/nexB/scancode-toolkit/blob/develop/src/licensedcode/data/licenses/pcre2-exception.LICENSE
        "public-domain", // https://github.com/nexB/scancode-toolkit/blob/develop/src/licensedcode/data/licenses/public-domain.LICENSE
        "public-domain-disclaimer", // https://github.com/nexB/scancode-toolkit/blob/develop/src/licensedcode/data/licenses/public-domain-disclaimer.LICENSE
        "python", // https://opensource.org/license/python-2-0/
        "rpl-1.5", // https://opensource.org/license/rpl-1-5/
        "sax-pd", // https://github.com/nexB/scancode-toolkit/blob/develop/src/licensedcode/data/licenses/sax-pd.LICENSE
        "unicode", // https://opensource.org/license/unicode-inc-license-agreement-data-files-and-software/
        "unicode-mappings", // https://github.com/nexB/scancode-toolkit/blob/develop/src/licensedcode/data/licenses/unicode-mappings.LICENSE
        "unlicense", // https://opensource.org/license/unlicense/
        "uoi-ncsa", // https://opensource.org/license/uoi-ncsa-php/
        "w3c-software-19980720", // https://opensource.org/license/w3c/
        "w3c-software-doc-20150513", // https://opensource.org/license/w3c/
        "warranty-disclaimer", // https://github.com/nexB/scancode-toolkit/blob/develop/src/licensedcode/data/licenses/warranty-disclaimer.LICENSE
        "x11", // https://github.com/nexB/scancode-toolkit/blob/develop/src/licensedcode/data/licenses/x11.LICENSE
        "zlib" // https://opensource.org/license/zlib/
    };

    private static readonly string[] s_ignoredFilePatterns = new string[]
    {
        "*.ildump",
    };

    private readonly string _targetRepo;
    private readonly string _relativeRepoPath;
    public static bool IncludeLicenseScanTests => !string.IsNullOrWhiteSpace(Config.LicenseScanPath);

    public LicenseScanTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
        Assert.NotNull(Config.LicenseScanPath);
        _targetRepo = new DirectoryInfo(Config.LicenseScanPath).Name;
        
        Match relativeRepoPathMatch = Regex.Match(Config.LicenseScanPath, @"(src/)[^/]+");
        Assert.True(relativeRepoPathMatch.Success);
        _relativeRepoPath = relativeRepoPathMatch.Value;
    }

    [ConditionalFact(typeof(LicenseScanTests), nameof(IncludeLicenseScanTests))]
    public void ScanForLicenses()
    {
        Assert.NotNull(Config.LicenseScanPath);

        // Indicates how long until a timeout occurs for scanning a given file
        const int FileScanTimeoutSeconds = 300;

        string scancodeResultsPath = Path.Combine(Config.LogsDirectory, "scancode-results.json");

        // Scancode Doc: https://scancode-toolkit.readthedocs.io/en/latest/index.html
        string ignoreOptions = string.Join(" ", s_ignoredFilePatterns.Select(pattern => $"--ignore {pattern}"));
        ExecuteHelper.ExecuteProcessValidateExitCode(
            "scancode",
            $"--license --processes 4 --timeout {FileScanTimeoutSeconds} --strip-root --only-findings {ignoreOptions} --json-pp {scancodeResultsPath} {Config.LicenseScanPath}",
            OutputHelper);

        JsonDocument doc = JsonDocument.Parse(File.ReadAllText(scancodeResultsPath));
        ScancodeResults? scancodeResults = doc.Deserialize<ScancodeResults>();
        Assert.NotNull(scancodeResults);

        FilterFiles(scancodeResults);

        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };
        string json = JsonSerializer.Serialize(scancodeResults, options);

        string baselineName = $"Licenses.{_targetRepo}.json";

        string baselinePath = BaselineHelper.GetBaselineFilePath(baselineName, BaselineSubDir);
        string expectedFilePath = Path.Combine(Config.LogsDirectory, baselineName);
        if (File.Exists(baselinePath))
        {
            File.Copy(baselinePath, expectedFilePath, overwrite: true);
        }
        else
        {
            // If there is no license baseline, generate a default empty one.
            ScancodeResults defaultResults = new();
            string defaultResultsJson = JsonSerializer.Serialize(defaultResults, options);
            File.WriteAllText(expectedFilePath, defaultResultsJson);
        }

        string actualFilePath = Path.Combine(Config.LogsDirectory, $"Updated{baselineName}");
        File.WriteAllText(actualFilePath, json);

        BaselineHelper.CompareFiles(expectedFilePath, actualFilePath, OutputHelper);
    }

    private void FilterFiles(ScancodeResults scancodeResults)
    {
        // This will filter out files that we don't want to include in the baseline.
        // Filtering can happen in two ways:
        //   1. There are a set of allowed license expressions that apply to all files. If a file has a match on one of those licenses,
        //      that license will not be considered.
        //   2. The LicenseExclusions.txt file contains a list of files and the licenses that should be excluded from those files.
        // Once the license expression filtering has been applied, if a file has any licenses left, it will be included in the baseline.
        // In that case, the baseline will list all of the licenses for that file, even if some were originally excluded during this processing.
        // In other words, the baseline will be fully representative of the licenses that apply to the files that are listed there.

        // We only care about the license expressions that are in the target repo.
        ExclusionsHelper exclusionsHelper = new("LicenseExclusions.txt", BaselineSubDir, "^" + Regex.Escape(_relativeRepoPath) + "/");

        for (int i = scancodeResults.Files.Count - 1; i >= 0; i--)
        {
            ScancodeFileResult file = scancodeResults.Files[i];

            // A license expression can be a logical expression, e.g. "(MIT OR Apache-2.0)"
            // For our purposes, we just care about the license involved, not the semantics of the expression.
            // Parse out all the expression syntax to just get the license names.
            string[] licenses = file.LicenseExpression?
                .Replace("(", string.Empty)
                .Replace(")", string.Empty)
                .Replace(" AND ", ",")
                .Replace(" OR ", ",")
                .Split(",", StringSplitOptions.RemoveEmptyEntries)
                .Select(license => license.Trim())
                .ToArray()
                ?? Array.Empty<string>();

            // First check whether the file's licenses can all be matched with allowed expressions
            IEnumerable<string> disallowedLicenses = licenses
                .Where(license => !s_allowedLicenseExpressions.Contains(license, StringComparer.OrdinalIgnoreCase));

            if (!disallowedLicenses.Any())
            {
                scancodeResults.Files.Remove(file);
            }
            else
            {
                // There are some licenses that are not allowed. Now check whether the file is excluded.

                // The path in the exclusion file is rooted from the VMR. But the path in the scancode results is rooted from the
                // target repo within the VMR. So we need to add back the beginning part of the path.
                string fullRelativePath = Path.Combine(_relativeRepoPath, file.Path);

                var remainingLicenses = disallowedLicenses.Where(license => !exclusionsHelper.IsFileExcluded(fullRelativePath, license));

                if (!remainingLicenses.Any())
                {
                    scancodeResults.Files.Remove(file);
                }
            }
        }
        exclusionsHelper.GenerateNewBaselineFile(_targetRepo);
    }

    private class ScancodeResults
    {
        [JsonPropertyName("files")]
        public List<ScancodeFileResult> Files { get; set; } = new();
    }

    private class ScancodeFileResult
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("detected_license_expression")]
        public string? LicenseExpression { get; set; }
    }
}
