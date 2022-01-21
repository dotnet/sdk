// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.DotNet.SourceBuild.SmokeTests
{
    internal class BaselineHelper
    {
        public static void Compare(string baselineFileName, IOrderedEnumerable<string> actualEntries)
        {
            IEnumerable<string> baseline = File.ReadAllLines(GetBaselineFilePath(baselineFileName));
            string[] missingEntries = actualEntries.Except(baseline).ToArray();
            string[] extraEntries = baseline.Except(actualEntries).ToArray();

            string? message = null;
            if (missingEntries.Length > 0)
            {
                message = $"Missing entries in '{baselineFileName}' baseline: {Environment.NewLine}{string.Join(Environment.NewLine, missingEntries)}{Environment.NewLine}{Environment.NewLine}";
            }

            if (extraEntries.Length > 0)
            {
                message += $"Extra entries in '{baselineFileName}' baseline: {Environment.NewLine}{string.Join(Environment.NewLine, extraEntries)}{Environment.NewLine}{Environment.NewLine}";
            }

            Assert.Null(message);
        }

        private static string GetBaselineFilePath(string baselineFileName) => Path.Combine(Directory.GetCurrentDirectory(), "baselines", baselineFileName);
    }
}
