// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools.Tool.Search;
using Xunit;
using Xunit.Abstractions;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class ToolSearchParserTests
    {
        private readonly ITestOutputHelper output;

        public ToolSearchParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact(Skip = "Test few tests")]
        public void DotnetToolSearchShouldThrowWhenNoSearchTerm()
        {
            var result = Parser.Instance.Parse("dotnet tool search");
            var appliedCommand = result["dotnet"]["tool"]["search"];
            Action a = () => new ToolSearchCommand(appliedCommand, result);
            a.ShouldThrow<CommandParsingException>();
        }
    }
}
