// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLineValidation;
using Microsoft.DotNet.Tools.Common;
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
        public void AddReferenceDoesNotHaveInteractiveFlagByDefault()
        {
            var result = Parser.Instance.Parse("dotnet add reference my.csproj");

            result.GetValue<bool>(ReferenceAddCommandParser.InteractiveOption)
                .Should().BeFalse();
        }

        [Fact]
        public void AddReferenceWithoutArgumentResultsInAnError()
        {
            var result = Parser.Instance.Parse("dotnet add reference");

            result
                .Errors
                .Select(e => e.Message)
                .Should()
                .BeEquivalentTo(string.Format(LocalizableStrings.RequiredArgumentMissingForCommand, "'reference'."));
        }
    }
}
