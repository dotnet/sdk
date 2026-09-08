// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.CommandFactory.CommandResolution;

namespace Microsoft.DotNet.Tests
{

    [TestClass]
    public class GivenADotnetToolsCommandResolver : SdkTest
    {
        private readonly DotnetToolsCommandResolver _dotnetToolsCommandResolver;

        public GivenADotnetToolsCommandResolver()
        {
            var dotnetToolPath = Path.Combine(SdkTestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, "DotnetTools");
            _dotnetToolsCommandResolver = new DotnetToolsCommandResolver(dotnetToolPath);
        }

        [TestMethod]
        public void ItReturnsNullWhenCommandNameIsNull()
        {
            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = null,
            };

            var result = _dotnetToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [TestMethod]
        public void ItReturnsNullWhenCommandNameDoesNotExistInProjectTools()
        {
            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "nonexistent-command",
            };

            var result = _dotnetToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [TestMethod]
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

        [TestMethod]
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

    [TestClass]
    public class GivenADotnetToolsCommandResolverAggregateTools
    {
        [TestMethod]
        [DataRow("dotnet-dev-certs")]
        [DataRow("dotnet-user-jwts")]
        [DataRow("dotnet-user-secrets")]
        public void ItReturnsAnExecutableCommandSpecFromAggregateToolPackage(string commandName)
        {
            var dotnetToolPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var toolDirectory = Path.Combine(dotnetToolPath, "aspnetcoretools", "1.0.0", "tools", "any", "win-x64");
            Directory.CreateDirectory(toolDirectory);
            var executableName = OperatingSystem.IsWindows() ? $"{commandName}.exe" : commandName;
            var executablePath = Path.Combine(toolDirectory, executableName);

            try
            {
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
            finally
            {
                try
                {
                    if (Directory.Exists(dotnetToolPath))
                    {
                        Directory.Delete(dotnetToolPath, recursive: true);
                    }
                }
                catch
                {
                }
            }
        }
    }
}
