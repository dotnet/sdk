// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli;

public abstract class CommandBase
{
    protected ParseResult _parseResult;

    protected CommandBase(ParseResult parseResult)
    {
        _parseResult = parseResult;
        ShowHelpOrErrorIfAppropriate(parseResult);
    }

    protected CommandBase() { }

    protected virtual void ShowHelpOrErrorIfAppropriate(ParseResult parseResult)
    {
        parseResult.ShowHelpOrErrorIfAppropriate();
    }

    public abstract int Execute();
}
