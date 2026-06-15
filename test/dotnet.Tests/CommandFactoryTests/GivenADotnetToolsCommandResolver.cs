// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.CommandFactory.CommandResolution;

namespace Microsoft.DotNet.Tests
{

    public class GivenADotnetToolsCommandResolver : SdkTest
    {
        private readonly DotnetToolsCommandResolver _dotnetToolsCommandResolver;

        public GivenADotnetToolsCommandResolver(ITestOutputHelper log) : base(log)
        {
            var dotnetToolPath = Path.Combine(SdkTestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, "DotnetTools");
            _dotnetToolsCommandResolver = new DotnetToolsCommandResolver(dotnetToolPath);
        }

        [Fact]
        public void ItReturnsNullWhenCommandNameIsNull()
        {
            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = null,
            };

            var result = _dotnetToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void ItReturnsNullWhenCommandNameDoesNotExistInProjectTools()
        {
            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "nonexistent-command",
            };

            var result = _dotnetToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void ItReturnsACommandSpec()
        {
            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "dotnet-watch",
            };

            var result = _dotnetToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();

            var commandPath = result.Args.Trim('"');
            commandPath.Should().Contain("dotnet-watch.dll");
        }

        [Fact]
        public void ItReturnsAnExecutableCommandSpecWhenExecutableExists()
        {
            var dotnetToolPath = TestAssetsManager.CreateTestDirectory().Path;
            var commandName = "dotnet-user-secrets";
            var toolDirectory = Path.Combine(dotnetToolPath, commandName, "1.0.0", "tools", "any", "win-x64");
            Directory.CreateDirectory(toolDirectory);
            var executableName = OperatingSystem.IsWindows() ? $"{commandName}.exe" : commandName;
            var executablePath = Path.Combine(toolDirectory, executableName);

            File.WriteAllText(executablePath, "test command that does nothing.");

            var resolver = new DotnetToolsCommandResolver(dotnetToolPath);
            var result = resolver.Resolve(new CommandResolverArguments()
            {
                CommandName = commandName,
                CommandArguments = ["--help"],
            });

            result.Should().NotBeNull();
            result.Path.Should().Be(executablePath);
            result.Args.Should().Be("--help");
        }
    }
}
