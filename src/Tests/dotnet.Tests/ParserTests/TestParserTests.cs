// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Xunit;
using Xunit.Abstractions;
using System;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class TestParserTests : IDisposable
    {
        public TestParserTests(ITestOutputHelper output)
        {
            this.output = output;
            this.startingCulture = System.Globalization.CultureInfo.CurrentCulture;
        }

        private readonly ITestOutputHelper output;
        private readonly System.Globalization.CultureInfo startingCulture;

        public void Dispose()
        {
            System.Globalization.CultureInfo.CurrentCulture = this.startingCulture;
        }

        [InlineData("en-US")]
        [InlineData("de-DE")]
        [Theory]
        public void TestParserCanGetArgumentFromDoubleDash(string culture)
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo(culture);
            var result = Parser.Instance.Parse(new[] { "dotnet", "test", "myproj.proj" });
            result.Errors.Count.Should().Be(1); // one unmatched token error
            result.ShowHelpOrErrorIfAppropriate(); // Should not throw error
        }
    }
}
