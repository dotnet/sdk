// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#nullable enable

using FluentAssertions;
using Xunit;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class TypoCorrectionTests
    {
        [InlineData("wbe", "web|webapp|wpf|install|uninstall", "web|wpf", "Levanshtein algorithm")]
        [InlineData("uninstal", "web|webapp|install|uninstall", "uninstall|install", "StartsWith & Contains")]
        [InlineData("console", "web|webapp|install|uninstall", "", "No matches")]
        [InlineData("blazor", "razor|pazor|blazorweb|blazorservice|uninstall|pizor", "blazorweb|blazorservice|razor|pazor", "StartsWith & Levanshtein algorithm")]
        [InlineData("blazor", "razor|pazor|pazors", "razor|pazor", "Levanshtein algorithm with shortest distance filtering")]
        [InlineData("con", "lacon|test|consoleweb|preconsole|uninstall|ponsole|pons", "consoleweb|lacon|pons", "StartsWith & Contains & Levanshtein algorithm")]
        [InlineData("c", "lacon|test|consoleweb|preconsole|uninstall|ponsole|pons|ccs", "consoleweb|ccs", "StartsWith & Levanshtein algorithm")]
        [InlineData("c", "peacon|lecture|beacon", "", "No matches due to Contains restriction on input length")]
        [Theory]
        public void TypoCorrection_BasicTest(string token, string possibleTokens, string expectedTokens, string checkedScenario)
        {
            TypoCorrection.GetSimilarTokens(possibleTokens.Split('|'), token)
                .Should().BeEquivalentTo(expectedTokens.Split('|', System.StringSplitOptions.RemoveEmptyEntries), checkedScenario);
        }
    }
}
