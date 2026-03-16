// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.NugetSearch;
using Parser = Microsoft.DotNet.Cli.Parser;
using Microsoft.DotNet.Cli.Commands.Tool.Search;

namespace dotnet.Tests.ToolSearchTests
{
    public class NugetSearchApiParameterTests
    {
        [Fact]
        public void ItShouldValidateSkipType()
        {
            var result = Parser.Parse("dotnet tool search mytool --skip wrongtype");

            var command = new ToolSearchCommand(result);
            Action a = () => command.GetNugetSearchApiParameter();
            a.Should().Throw<GracefulException>();
        }

        [Fact]
        public void ItShouldValidateTakeType()
        {
            var result = Parser.Parse("dotnet tool search mytool --take wrongtype");

            var command = new ToolSearchCommand(result);
            Action a = () => command.GetNugetSearchApiParameter();
            a.Should().Throw<GracefulException>();
        }

        [Fact]
        public void ItShouldNotThrowWhenInputIsValid()
        {
            var parseResult = Parser.Parse("dotnet tool search mytool --detail --skip 3 --take 4 --prerelease");

            var command = new ToolSearchCommand(parseResult);
            var result = command.GetNugetSearchApiParameter();
            result.Prerelease.Should().Be(true);
            result.Skip.Should().Be(3);
            result.Take.Should().Be(4);
        }
    }
}
