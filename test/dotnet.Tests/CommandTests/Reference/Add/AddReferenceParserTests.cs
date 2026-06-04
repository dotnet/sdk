// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands.Hidden.Add.Reference;
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
            var result = Parser.Parse(["dotnet", "add", "reference", "my.csproj"]);

            var command = Assert.IsType<AddReferenceCommandDefinition>(result.CommandResult.Command);

            // Compare against the same cached value the argument's default factory uses
            // (CommonOptions.NormalizedCurrentDirectory) rather than a fresh
            // Directory.GetCurrentDirectory(). The current directory is process-global mutable
            // state, and other tests in this assembly call Directory.SetCurrentDirectory while
            // running in parallel, so re-deriving it here races and makes this test flaky.
            result.GetValue(command.Parent.ProjectOrFileArgument)
                .Should()
                .BeEquivalentTo(CommonOptions.NormalizedCurrentDirectory.Value);
        }

        [Fact]
        public void AddReferenceHasInteractiveFlag()
        {
            var result = Parser.Parse(["dotnet", "add", "reference", "my.csproj", "--interactive"]);

            var command = Assert.IsType<AddReferenceCommandDefinition>(result.CommandResult.Command);

            result.GetValue(command.InteractiveOption)
                .Should().BeTrue();
        }

        [Fact]
        public void AddReferenceWithoutArgumentResultsInAnError()
        {
            var result = Parser.Parse(["dotnet", "add", "reference"]);

            result.Errors.Should().NotBeEmpty();

            var argument = (result.Errors.SingleOrDefault()?.SymbolResult as ArgumentResult)?.Argument;

            var command = Assert.IsType<AddReferenceCommandDefinition>(result.CommandResult.Command);
            argument.Should().Be(command.ProjectPathArgument);
        }
    }
}
