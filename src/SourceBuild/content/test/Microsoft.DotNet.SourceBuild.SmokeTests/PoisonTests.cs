// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.SmokeTests
{
    public class PoisonTests : SdkTests
    {
        public PoisonTests(ITestOutputHelper outputHelper) : base(outputHelper) { }

        [SkippableFact(Config.PoisonReportPathEnv, skipOnNullOrWhiteSpaceEnv: true)]
        public void VerifyUsage()
        {
            if (!File.Exists(Config.PoisonReportPath))
            {
                throw new InvalidOperationException($"Poison report '{Config.PoisonReportPath}' does not exist.");
            }

            string currentPoisonReport = File.ReadAllText(Config.PoisonReportPath);
            currentPoisonReport = RemoveHashes(currentPoisonReport);
            currentPoisonReport = BaselineHelper.RemoveRids(currentPoisonReport);
            currentPoisonReport = BaselineHelper.RemoveRids(currentPoisonReport, true);
            currentPoisonReport = BaselineHelper.RemoveVersions(currentPoisonReport);

            BaselineHelper.CompareBaselineContents("PoisonUsage.txt", currentPoisonReport, OutputHelper);
        }

        private static string RemoveHashes(string source) => Regex.Replace(source, "^\\s*<Hash>.*</Hash>(\r\n?|\n)", string.Empty, RegexOptions.Multiline);
    }
}
