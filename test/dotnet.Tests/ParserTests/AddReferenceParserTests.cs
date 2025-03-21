// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class AddReferenceParserTests
    {
        private readonly ITestOutputHelper output;

        public AddReferenceParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void AddReferenceHasDefaultArgumentSetToCurrentDirectory()
        {
            var result = Parser.Instance.Parse("dotnet add reference my.csproj");

            result.GetValue<string>(AddCommandParser.ProjectArgument)
                .Should()
                .BeEquivalentTo(
                    PathUtility.EnsureTrailingSlash(Directory.GetCurrentDirectory()));
        }

        [Fact]
        public void AddReferenceHasInteractiveFlag()
        {
            var result = Parser.Instance.Parse("dotnet add reference my.csproj --interactive");

            result.GetValue<bool>(ReferenceAddCommandParser.InteractiveOption)
                .Should().BeTrue();
        }

        [Fact]
        public void AddReferenceDoesHaveInteractiveFlagByDefault()
        {
            var result = Parser.Instance.Parse("dotnet add reference my.csproj");

            result.GetValue<bool>(ReferenceAddCommandParser.InteractiveOption)
                .Should().BeTrue();
        }

        [Fact]
        public void AddReferenceWithoutArgumentResultsInAnError()
        {
            var result = Parser.Instance.Parse("dotnet add reference");

            result.Errors.Should().NotBeEmpty();

            var argument = (result.Errors.SingleOrDefault()?.SymbolResult as ArgumentResult)?.Argument;

            argument.Should().Be(ReferenceAddCommandParser.ProjectPathArgument);
        }
    }
}
