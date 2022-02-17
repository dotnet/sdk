// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class TestCommandParserTests
    {
        [Fact]
        public void SurroundWithDoubleQuotesWithNullThrows()
        {
            Assert.Throws<ArgumentNullException>(() =>
                TestCommandParser.SurroundWithDoubleQuotes(null));
        }

        [Theory]
        [InlineData("")]
        [InlineData("\"a\"")]
        [InlineData("\"aaa\"")]
        public void SurroundWithDoubleQuotesWhenAlreadySurroundedDoesNothing(string input)
        {
            var escapedInput = "\"" + input + "\"";
            var result = TestCommandParser.SurroundWithDoubleQuotes(escapedInput);
            result.Should().Be(escapedInput);
        }

        [Theory]
        [InlineData("")]
        [InlineData("a")]
        [InlineData("aaa")]
        [InlineData("\"a")]
        [InlineData("a\"")]
        public void SurroundWithDoubleQuotesWhenNotSurroundedSurrounds(string input)
        {
            var result = TestCommandParser.SurroundWithDoubleQuotes(input);
            result.Should().Be("\"" + input + "\"");
        }

        [Theory]
        [InlineData("\\\\")]
        [InlineData("\\\\\\\\")]
        [InlineData("/\\\\")]
        [InlineData("/\\/\\/\\\\")]
        public void SurroundWithDoubleQuotesHandlesCorrectlyEvenCountOfTrailingBackslashes(string input)
        {
            var result = TestCommandParser.SurroundWithDoubleQuotes(input);
            result.Should().Be("\"" + input + "\"");
        }

        [Theory]
        [InlineData("\\")]
        [InlineData("\\\\\\")]
        [InlineData("/\\")]
        [InlineData("/\\/\\/\\")]
        public void SurroundWithDoubleQuotesHandlesCorrectlyOddCountOfTrailingBackslashes(string input)
        {
            var result = TestCommandParser.SurroundWithDoubleQuotes(input);
            result.Should().Be("\"" + input + "\\\"");
        }
    }
}
