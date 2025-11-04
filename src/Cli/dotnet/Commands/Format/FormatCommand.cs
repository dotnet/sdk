// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Format;

public class FormatCommand(IEnumerable<string> argsToForward) : FormatForwardingApp(argsToForward)
{
    public static FormatCommand FromArgs(string[] args)
    {
        var result = Parser.Parse(["dotnet", "format", .. args]);
        return FromParseResult(result);
    }

    public static FormatCommand FromParseResult(ParseResult result)
    {
        return new FormatCommand(result.GetValue(FormatCommandParser.Arguments));
    }

    public static int Run(ParseResult parseResult)
    {
        parseResult.HandleDebugSwitch();

        return FromParseResult(parseResult).Execute();
    }

    public static int Run(string[] args)
    {
        DebugHelper.HandleDebugSwitch(ref args);
        return new FormatForwardingApp(args).Execute();
    }
}
