// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli;

public abstract class CommandBase
{
    protected ParseResult _parseResult;

    protected CommandBase(ParseResult parseResult) : this(parseResult, services: null)
    {
    }

    protected CommandBase(ParseResult parseResult, CommandServices services) : this(services)
    {
        _parseResult = parseResult;
        parseResult.ShowHelpOrErrorIfAppropriate();
    }

    protected CommandBase() : this(services: null)
    {
    }

    protected CommandBase(CommandServices services)
    {
        Services = services ?? new CommandServices();
    }

    protected internal CommandServices Services { get; }

    public abstract int Execute();
}

public abstract class CommandBase<TDefinition>(ParseResult parseResult, CommandServices services) : CommandBase(parseResult, services)
    where TDefinition : Command
{
    public CommandBase(ParseResult parseResult) : this(parseResult, services: null)
    {
    }

    protected TDefinition Definition { get; } = (TDefinition)parseResult.CommandResult.Command;
}
