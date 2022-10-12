// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Extensions.Logging;

namespace Microsoft.TemplateEngine.Authoring.CLI.Commands
{
    internal class LocalizeCommand : Command
    {
        internal LocalizeCommand(ILoggerFactory loggerFactory)
            : base("localize")
        {
            this.AddCommand(new ExportCommand(loggerFactory));
        }
    }
}
