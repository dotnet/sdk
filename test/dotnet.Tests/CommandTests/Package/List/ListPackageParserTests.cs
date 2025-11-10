// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Hidden.List.Package;
using Microsoft.DotNet.Cli.CommandLine;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class ListPackageParserTests
    {
        [Fact]
        public void ListPackageCanForwardInteractiveFlag()
        {
            var result = Parser.Parse(["dotnet", "list", "package", "--interactive"]);

            result.OptionValuesToBeForwarded(ListPackageCommandParser.GetCommand()).Should().ContainSingle("--interactive");
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public void ListPackageDoesNotForwardVerbosityByDefault()
        {
            var result = Parser.Parse(["dotnet", "list", "package"]);

            result
                .OptionValuesToBeForwarded(ListPackageCommandParser.GetCommand())
                .Should()
                .NotContain(i => i.Contains("--verbosity", StringComparison.OrdinalIgnoreCase))
                .And.NotContain(i => i.Contains("-v", StringComparison.OrdinalIgnoreCase));
            result.Errors.Should().BeEmpty();
        }
    }
}
