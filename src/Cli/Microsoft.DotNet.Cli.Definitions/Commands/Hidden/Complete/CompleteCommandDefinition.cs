// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Complete;

internal sealed class CompleteCommandDefinition : Command
{
    public readonly Argument<string> PathArgument = new("path");

    public readonly Option<int?> PositionOption = new("--position")
    {
        HelpName = "command"
    };

    public CompleteCommandDefinition()
        : base("complete")
    {
        Hidden = true;

        Arguments.Add(PathArgument);
        Options.Add(PositionOption);
    }
}
