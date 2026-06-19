// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    [TestClass]
    public class TypoCorrectionTests
    {
        [DataRow("wbe", "web|webapp|wpf|install|uninstall", "web|wpf", "Levanshtein algorithm")]
        [DataRow("uninstal", "web|webapp|install|uninstall", "uninstall|install", "StartsWith & Contains")]
        [DataRow("console", "web|webapp|install|uninstall", "", "No matches")]
        [DataRow("blazor", "razor|pazor|blazorweb|blazorservice|uninstall|pizor", "blazorweb|blazorservice|razor|pazor", "StartsWith & Levanshtein algorithm")]
        [DataRow("blazor", "razor|pazor|pazors", "razor|pazor", "Levanshtein algorithm with shortest distance filtering")]
        [DataRow("con", "lacon|test|consoleweb|precon|uninstall|ponsole|pons", "consoleweb|lacon|precon|pons", "StartsWith & Contains & Levanshtein algorithm")]
        [DataRow("c", "lacon|test|consoleweb|preconsole|uninstall|ponsole|pons|ccs", "consoleweb|ccs", "StartsWith & Levanshtein algorithm")]
        [DataRow("c", "peacon|lecture|beacon", "", "No matches due to Contains restriction on input length")]
        [DataRow(
            "eac",
            "peac|lect|beac|zeac|dect|meac|qeac|aect|oeac|xeac|necte|geacy|gueac",
            "peac|beac|zeac|meac|qeac|oeac|xeac|geacy|gueac|lect",
            "Contains due to max number of suggestions restriction")]
        [DataRow(
            "eacy",
            "eacyy|eacyl|eacys|eacyt|eacyp|eacyzz|eacyqwe|eacyasd|eacyaa|eacynbv|eacyrfd|peacy|peacp",
            "eacyy|eacyl|eacys|eacyt|eacyp|eacyzz|eacyaa|eacyqwe|eacyasd|eacynbv",
            "StartsWith due to max number of suggestions restriction")]
        [TestMethod]
        public void TypoCorrection_BasicTest(string token, string possibleTokens, string expectedTokens, string checkedScenario)
        {
            TypoCorrection.GetSimilarTokens(possibleTokens.Split('|'), token)
                .Should().BeEquivalentTo(expectedTokens.Split('|', StringSplitOptions.RemoveEmptyEntries), checkedScenario);
        }
    }
}
