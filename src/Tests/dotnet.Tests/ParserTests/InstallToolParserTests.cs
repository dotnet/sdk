// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Xunit;
using Xunit.Abstractions;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class InstallToolParserTests
    {
        private readonly ITestOutputHelper output;

        public InstallToolParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact(Skip = "Test few tests")]
        public void InstallGlobaltoolParserCanGetPackageIdAndPackageVersion()
        {
            var command = Parser.Instance;
            var result = command.Parse("dotnet tool install -g console.test.app --version 1.0.1");

            var parseResult = result["dotnet"]["tool"]["install"];

            var packageId = parseResult.Arguments.Single();
            var packageVersion = parseResult.ValueOrDefault<string>("version");

            packageId.Should().Be("console.test.app");
            packageVersion.Should().Be("1.0.1");
        }

        [Fact(Skip = "Test few tests")]
        public void InstallGlobaltoolParserCanGetFollowingArguments()
        {
            var command = Parser.Instance;
            var result =
                command.Parse(
                    @"dotnet tool install -g console.test.app --version 1.0.1 --framework netcoreapp2.0 --configfile C:\TestAssetLocalNugetFeed");

            var parseResult = result["dotnet"]["tool"]["install"];

            parseResult.ValueOrDefault<string>("configfile").Should().Be(@"C:\TestAssetLocalNugetFeed");
            parseResult.ValueOrDefault<string>("framework").Should().Be("netcoreapp2.0");
        }

        [Fact(Skip = "Test few tests")]
        public void InstallToolParserCanParseSourceOption()
        {
            const string expectedSourceValue = "TestSourceValue";

            var result =
                Parser.Instance.Parse($"dotnet tool install -g --add-source {expectedSourceValue} console.test.app");

            var appliedOptions = result["dotnet"]["tool"]["install"];
            appliedOptions.ValueOrDefault<string[]>("add-source").First().Should().Be(expectedSourceValue);
        }

        [Fact(Skip = "Test few tests")]
        public void InstallToolParserCanParseMultipleSourceOption()
        {
            const string expectedSourceValue1 = "TestSourceValue1";
            const string expectedSourceValue2 = "TestSourceValue2";

            var result =
                Parser.Instance.Parse(
                    $"dotnet tool install -g " +
                    $"--add-source {expectedSourceValue1} " +
                    $"--add-source {expectedSourceValue2} console.test.app");

            var appliedOptions = result["dotnet"]["tool"]["install"];

            appliedOptions.ValueOrDefault<string[]>("add-source")[0].Should().Be(expectedSourceValue1);
            appliedOptions.ValueOrDefault<string[]>("add-source")[1].Should().Be(expectedSourceValue2);
        }

        [Fact(Skip = "Test few tests")]
        public void InstallToolParserCanGetGlobalOption()
        {
            var result = Parser.Instance.Parse("dotnet tool install -g console.test.app");

            var appliedOptions = result["dotnet"]["tool"]["install"];
            appliedOptions.ValueOrDefault<bool>("global").Should().Be(true);
        }
        
        [Fact(Skip = "Test few tests")]
        public void InstallToolParserCanGetLocalOption()
        {
            var result = Parser.Instance.Parse("dotnet tool install --local console.test.app");

            var appliedOptions = result["dotnet"]["tool"]["install"];
            appliedOptions.ValueOrDefault<bool>("local").Should().Be(true);
        }

        [Fact(Skip = "Test few tests")]
        public void InstallToolParserCanGetManifestOption()
        {
            var result =
                Parser.Instance.Parse(
                    "dotnet tool install --local console.test.app --tool-manifest folder/my-manifest.format");

            var appliedOptions = result["dotnet"]["tool"]["install"];
            appliedOptions.ValueOrDefault<string>("tool-manifest").Should().Be("folder/my-manifest.format");
        }

        [Fact(Skip = "Test few tests")]
        public void InstallToolParserCanParseVerbosityOption()
        {
            const string expectedVerbosityLevel = "diag";

            var result = Parser.Instance.Parse($"dotnet tool install -g --verbosity:{expectedVerbosityLevel} console.test.app");

            var appliedOptions = result["dotnet"]["tool"]["install"];
            appliedOptions.SingleArgumentOrDefault("verbosity").Should().Be(expectedVerbosityLevel);
        }

        [Fact(Skip = "Test few tests")]
        public void InstallToolParserCanParseToolPathOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool install --tool-path C:\Tools console.test.app");

            var appliedOptions = result["dotnet"]["tool"]["install"];
            appliedOptions.SingleArgumentOrDefault("tool-path").Should().Be(@"C:\Tools");
        }

        [Fact(Skip = "Test few tests")]
        public void InstallToolParserCanParseNoCacheOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool install -g console.test.app --no-cache");

            var appliedOptions = result["dotnet"]["tool"]["install"];
            appliedOptions.OptionValuesToBeForwarded().Should().ContainSingle("--no-cache");
        }

        [Fact(Skip = "Test few tests")]
        public void InstallToolParserCanParseIgnoreFailedSourcesOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool install -g console.test.app --ignore-failed-sources");

            var appliedOptions = result["dotnet"]["tool"]["install"];
            appliedOptions.OptionValuesToBeForwarded().Should().ContainSingle("--ignore-failed-sources");
        }

        [Fact(Skip = "Test few tests")]
        public void InstallToolParserCanParseDisableParallelOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool install -g console.test.app --disable-parallel");

            var appliedOptions = result["dotnet"]["tool"]["install"];
            appliedOptions.OptionValuesToBeForwarded().Should().ContainSingle("--disable-parallel");
        }

        [Fact(Skip = "Test few tests")]
        public void InstallToolParserCanParseInteractiveRestoreOption()
        {
            var result =
                Parser.Instance.Parse(@"dotnet tool install -g console.test.app --interactive");

            var appliedOptions = result["dotnet"]["tool"]["install"];
            appliedOptions.OptionValuesToBeForwarded().Should().ContainSingle("--interactive");
        }
    }
}
