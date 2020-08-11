﻿// Copyright (c) .NET Foundation and contributors. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 

using System;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.CommandFactory;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;
using LocalizableStrings = Microsoft.DotNet.Cli.Utils.LocalizableStrings;

namespace Microsoft.DotNet.Tests
{
    public class CommandObjectTests : SdkTest
    {
        public CommandObjectTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void WhenItCannotResolveCommandItThrows()
        {
            Action a = () => { CommandFactoryUsingResolver.Create(new ResolveNothingCommandResolverPolicy(), "non-exist-command", Array.Empty<string>()); };
            a.ShouldThrow<CommandUnknownException>();
        }

        [Fact]
        public void WhenItCannotResolveCommandButCommandIsInListOfKnownToolsItThrows()
        {
            Action a = () => { CommandFactoryUsingResolver.Create(new ResolveNothingCommandResolverPolicy(), "non-exist-command", Array.Empty<string>()); };
            a.ShouldThrow<CommandUnknownException>();
        }

        private class ResolveNothingCommandResolverPolicy : ICommandResolverPolicy
        {
            public CompositeCommandResolver CreateCommandResolver()
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
