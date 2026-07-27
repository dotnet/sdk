// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.TemplateEngine.Authoring.CLI.Commands.Verify;

namespace Microsoft.TemplateEngine.Authoring.CLI.UnitTests
{
    [TestClass]
    public class VerifyCommandArgsTests
    {
        [TestMethod]
        [DataRow(null, new string[] { })]
        [DataRow(" ", new string[] { })]
        [DataRow(" a b     c", new string[] { "a", "b", "c" })]
        [DataRow(" abc   ", new string[] { "abc" })]
        [DataRow("a \"b     c \"  d  ", new string[] { "a", "b     c ", "d" })]
        [DataRow("aa \" bb cc \"dd", new string[] { "aa", " bb cc ", "dd" })]
        [DataRow("aa q= 'bb cc'dd", new string[] { "aa", "q=", "bb cc", "dd" })]
        public void OnTokenizeJoinedArgsResultIsExpected(string? input, IEnumerable<string> expectedOutput)
        {
            var result = VerifyCommandArgs.TokenizeJoinedArgs(input);
            result.Should().BeEquivalentTo(expectedOutput, options => options.WithStrictOrdering());
        }
    }
}
