// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.TemplateEngine.Cli
{
    public static class NewCommandFactory
    {
        public static Command Create(Func<ParseResult, ICliTemplateEngineHost> hostBuilder)
            => new NewCommand(hostBuilder ?? throw new ArgumentNullException(nameof(hostBuilder)));
    }
}
