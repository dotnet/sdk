// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
#if !CLI_AOT
using Microsoft.DotNet.Cli.Extensions;
#endif

namespace Microsoft.DotNet.Cli;

public abstract class CommandBase
{
    protected ParseResult _parseResult;

    protected CommandBase(ParseResult parseResult)
    {
        _parseResult = parseResult;
#if !CLI_AOT
        ShowHelpOrErrorIfAppropriate(parseResult);
#endif
    }

    protected CommandBase() { }

#if !CLI_AOT
    protected virtual void ShowHelpOrErrorIfAppropriate(ParseResult parseResult)
    {
        parseResult.ShowHelpOrErrorIfAppropriate();
    }
#endif

    public abstract int Execute();
}

public abstract class CommandBase<TDefinition>(ParseResult parseResult) : CommandBase(parseResult)
    where TDefinition : Command
{
    protected TDefinition Definition { get; } = (TDefinition)parseResult.CommandResult.Command;
}
