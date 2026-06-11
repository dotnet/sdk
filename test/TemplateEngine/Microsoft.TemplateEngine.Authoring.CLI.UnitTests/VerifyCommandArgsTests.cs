// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.TemplateEngine.Authoring.CLI.Commands.Verify;

namespace Microsoft.TemplateEngine.Authoring.CLI.UnitTests
{
    public class VerifyCommandArgsTests
    {
        [Theory]
        [InlineData(null, new string[] { })]
        [InlineData(" ", new string[] { })]
        [InlineData(" a b     c", new string[] { "a", "b", "c" })]
        [InlineData(" abc   ", new string[] { "abc" })]
        [InlineData("a \"b     c \"  d  ", new string[] { "a", "b     c ", "d" })]
        [InlineData("aa \" bb cc \"dd", new string[] { "aa", " bb cc ", "dd" })]
        [InlineData("aa q= 'bb cc'dd", new string[] { "aa", "q=", "bb cc", "dd" })]
        public void OnTokenizeJoinedArgsResultIsExpected(string? input, IEnumerable<string> expectedOutput)
        {
            var result = VerifyCommandArgs.TokenizeJoinedArgs(input);
            result.Should().BeEquivalentTo(expectedOutput, options => options.WithStrictOrdering());
        }
    }
}
