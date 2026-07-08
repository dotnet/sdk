// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands.Hidden.Add.Reference;
using Microsoft.DotNet.Cli.Utils;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    [TestClass]
    public class AddReferenceParserTests
    {

        [TestMethod]
        public void AddReferenceHasDefaultArgumentSetToCurrentDirectory()
        {
            var result = Parser.Parse(["dotnet", "add", "reference", "my.csproj"]);

            var command = Assert.IsExactInstanceOfType<AddReferenceCommandDefinition>(result.CommandResult.Command);

            result.GetValue(command.Parent.ProjectOrFileArgument)
                .Should()
                .BeEquivalentTo(
                    PathUtilities.EnsureTrailingSlash(Directory.GetCurrentDirectory()));
        }

        [TestMethod]
        public void AddReferenceHasInteractiveFlag()
        {
            var result = Parser.Parse(["dotnet", "add", "reference", "my.csproj", "--interactive"]);

            var command = Assert.IsExactInstanceOfType<AddReferenceCommandDefinition>(result.CommandResult.Command);

            result.GetValue(command.InteractiveOption)
                .Should().BeTrue();
        }

        [TestMethod]
        public void AddReferenceWithoutArgumentResultsInAnError()
        {
            var result = Parser.Parse(["dotnet", "add", "reference"]);

            result.Errors.Should().NotBeEmpty();

            var argument = (result.Errors.SingleOrDefault()?.SymbolResult as ArgumentResult)?.Argument;

            var command = Assert.IsExactInstanceOfType<AddReferenceCommandDefinition>(result.CommandResult.Command);
            argument.Should().Be(command.ProjectPathArgument);
        }
    }
}
