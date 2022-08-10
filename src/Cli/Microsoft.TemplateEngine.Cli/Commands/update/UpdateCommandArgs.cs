﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class UpdateCommandArgs : GlobalArgs
    {
        public UpdateCommandArgs(BaseUpdateCommand command, ParseResult parseResult) : base(command, parseResult)
        {
            if (command is UpdateCommand updateCommand)
            {
                CheckOnly = parseResult.GetValueForOption(UpdateCommand.CheckOnlyOption);
            }
            else if (command is LegacyUpdateCheckCommand)
            {
                CheckOnly = true;
            }
            else if (command is LegacyUpdateApplyCommand)
            {
                CheckOnly = false;
            }
            else
            {
                throw new ArgumentException($"Unsupported type {command.GetType().FullName}", nameof(command));
            }

            Interactive = parseResult.GetValueForOption(command.InteractiveOption);
            AdditionalSources = parseResult.GetValueForOption(command.AddSourceOption);
        }

        public bool CheckOnly { get; }

        public bool Interactive { get; }

        public IReadOnlyList<string>? AdditionalSources { get; }
    }
}
