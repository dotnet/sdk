// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Cli.Test.Tests;

[TestClass]
public class RegexPatternHelperTests : SdkTest
{
    [TestMethod]
    public void RunPatternUsesTheGeneratedAppFramework()
    {
        string path = Path.Combine("bin", "Debug", ToolsetInfo.CurrentTargetFramework, "TestProject.dll");
        string output = $"{path} ({ToolsetInfo.CurrentTargetFramework}|x64) passed";
        string pattern = RegexPatternHelper.GenerateProjectRegexPattern("TestProject", "passed", true, "Debug");

        Assert.MatchesRegex(pattern, output);
        Regex.IsMatch($"{path} (net99.0|x64) passed", pattern).Should().BeFalse();
    }

    [TestMethod]
    public void DiscoveryPatternUsesTheGeneratedAppFramework()
    {
        string path = Path.Combine("bin", "Debug", ToolsetInfo.CurrentTargetFramework, "TestProject.dll");
        string output = $"Discovered 1 tests: {path} ({ToolsetInfo.CurrentTargetFramework}|arm64) Test0";
        string pattern = RegexPatternHelper.GenerateProjectRegexPattern("TestProject", true, "Debug", "Discovered 1 tests", ["Test0"]);

        Assert.MatchesRegex(pattern, output);
        Regex.IsMatch($"Discovered 1 tests: {path} (net99.0|arm64) Test0", pattern).Should().BeFalse();
    }
}
