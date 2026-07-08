// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.CommandFactory;
using Microsoft.DotNet.Cli.CommandFactory.CommandResolution;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tests
{
    [TestClass]
    public class CommandObjectTests : SdkTest
    {
        public CommandObjectTests()
        {
        }

        [TestMethod]
        public void WhenItCannotResolveCommandItThrows()
        {
            Action a = () => { CommandFactoryUsingResolver.Create(new ResolveNothingCommandResolverPolicy(), "non-exist-command", Array.Empty<string>()); };
            a.Should().Throw<CommandUnknownException>();
        }

        [TestMethod]
        public void WhenItCannotResolveCommandButCommandIsInListOfKnownToolsItThrows()
        {
            Action a = () => { CommandFactoryUsingResolver.Create(new ResolveNothingCommandResolverPolicy(), "non-exist-command", Array.Empty<string>()); };
            a.Should().Throw<CommandUnknownException>();
        }

        private class ResolveNothingCommandResolverPolicy : ICommandResolverPolicy
        {
            public CompositeCommandResolver CreateCommandResolver(string sdkRoot = null, string currentWorkingDirectory = null)
            {
                var compositeCommandResolver = new CompositeCommandResolver();
                compositeCommandResolver.AddCommandResolver(new ResolveNothingCommandResolver());

                return compositeCommandResolver;
            }
        }

        private class ResolveNothingCommandResolver : ICommandResolver
        {
            public CommandSpec Resolve(CommandResolverArguments arguments)
            {
                return null;
            }
        }
    }
}
