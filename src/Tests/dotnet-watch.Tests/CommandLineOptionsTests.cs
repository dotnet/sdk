// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Watcher.Tools
{
    public class CommandLineOptionsTests
    {
        private readonly Extensions.Tools.Internal.TestReporter _testReporter;

        public CommandLineOptionsTests(ITestOutputHelper output)
        {
            _testReporter = new(output);
        }

        [Theory]
        [InlineData(new object[] { new[] { "-h" } })]
        [InlineData(new object[] { new[] { "-?" } })]
        [InlineData(new object[] { new[] { "--help" } })]
        [InlineData(new object[] { new[] { "--help", "--bogus" } })]
        public async Task HelpArgs(string[] args)
        {
            var rootCommand = Program.CreateRootCommand(c => Task.FromResult(0), _testReporter);
            CliConfiguration configuration = new(rootCommand)
            {
                Output = new StringWriter()
            };

            await configuration.Parse(args).InvokeAsync();

            Assert.Contains("Usage:", configuration.Output.ToString());
        }

        [Theory]
        [InlineData(new[] { "run" }, new[] { "run" })]
        [InlineData(new[] { "run", "--", "subarg" }, new[] { "run", "subarg" })]
        [InlineData(new[] { "--", "run", "--", "subarg" }, new[] { "run", "--", "subarg" })]
        [InlineData(new[] { "--unrecognized-arg" }, new[] { "--unrecognized-arg" })]
        public async Task ParsesRemainingArgs(string[] args, string[] expected)
        {
            CommandLineOptions options = null;

            var rootCommand = Program.CreateRootCommand(c =>
            {
                options = c;
                return Task.FromResult(0);
            }, _testReporter);
            CliConfiguration configuration = new(rootCommand)
            {
                Output = new StringWriter()
            };

            await configuration.Parse(args).InvokeAsync();

            Assert.NotNull(options);

            Assert.Equal(expected, options.RemainingArguments);
            Assert.Empty(configuration.Output.ToString());
        }

        [Fact]
        public async Task CannotHaveQuietAndVerbose()
        {
            var rootCommand = Program.CreateRootCommand(c => Task.FromResult(0), _testReporter);
            CliConfiguration configuration = new(rootCommand)
            {
                Error = new StringWriter()
            };

            await configuration.Parse(new[] { "--quiet", "--verbose" }).InvokeAsync();

            Assert.Contains(Resources.Error_QuietAndVerboseSpecified, configuration.Error.ToString());
        }

        [Fact]
        public async Task ShortFormForProjectArgumentPrintsWarning()
        {
            var reporter = new Mock<Extensions.Tools.Internal.IReporter>();
            reporter.Setup(r => r.Warn(Resources.Warning_ProjectAbbreviationDeprecated, It.IsAny<string>())).Verifiable();
            CommandLineOptions options = null;
            var rootCommand = Program.CreateRootCommand(c => { options = c; return Task.FromResult(0); }, reporter.Object);

            await rootCommand.Parse(new[] { "-p", "MyProject.csproj" }).InvokeAsync();

            reporter.Verify();
            Assert.NotNull(options);
            Assert.Equal("MyProject.csproj", options.Project);
        }

        [Fact]
        public async Task LongFormForProjectArgumentWorks()
        {
            var reporter = new Mock<Extensions.Tools.Internal.IReporter>();
            CommandLineOptions options = null;
            var rootCommand = Program.CreateRootCommand(c => { options = c; return Task.FromResult(0); }, reporter.Object);

            await rootCommand.Parse(new[] { "--project", "MyProject.csproj" }).InvokeAsync();

            reporter.Verify(r => r.Warn(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
            Assert.NotNull(options);
            Assert.Equal("MyProject.csproj", options.Project);
        }

        [Fact]
        public async Task LongFormForLaunchProfileArgumentWorks()
        {
            var reporter = new Mock<Extensions.Tools.Internal.IReporter>();
            CommandLineOptions options = null;
            var rootCommand = Program.CreateRootCommand(c => { options = c; return Task.FromResult(0); }, reporter.Object);

            await rootCommand.Parse(new[] { "--launch-profile", "CustomLaunchProfile" }).InvokeAsync();

            reporter.Verify(r => r.Warn(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
            Assert.NotNull(options);
            Assert.Equal("CustomLaunchProfile", options.LaunchProfile);
        }

        [Fact]
        public async Task ShortFormForLaunchProfileArgumentWorks()
        {
            var reporter = new Mock<Extensions.Tools.Internal.IReporter>();
            CommandLineOptions options = null;
            var rootCommand = Program.CreateRootCommand(c => { options = c; return Task.FromResult(0); }, reporter.Object);

            await rootCommand.Parse(new[] { "-lp", "CustomLaunchProfile" }).InvokeAsync();

            reporter.Verify(r => r.Warn(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
            Assert.NotNull(options);
            Assert.Equal("CustomLaunchProfile", options.LaunchProfile);
        }
    }
}
