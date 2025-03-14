// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLineValidation;
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

            result
                .Errors
                .Select(e => e.Message)
                .Should()
                // This string comes from System.CommandLine:
                // https://github.com/dotnet/command-line-api/blob/b4485d8417c4eb0ec3d1a291e1003686618ce598/src/System.CommandLine/Properties/Resources.resx#L135-L137
                .BeEquivalentTo("Required argument missing for command: 'reference'.");
        }
    }
}
