// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandFactory;
using Microsoft.DotNet.Cli.CommandFactory.CommandResolution;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.ToolManifest;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.DotNet.Cli.ToolPackage;
namespace Microsoft.DotNet.Cli.Commands.Tool.Runx;

internal class ToolRunxCommand(ParseResult result) : CommandBase(result)
{
    private readonly string _toolCommandName = result.GetValue(ToolRunxCommandParser.CommandNameArgument);
    private readonly IEnumerable<string> _forwardArgument = result.GetValue(ToolRunxCommandParser.CommandArgument);
    public bool _allowRollForward = result.GetValue(ToolRunxCommandParser.RollForwardOption);

    public override int Execute()
    {
        var tempDir = new DirectoryPath(PathUtilities.CreateTempSubdirectory());
        return 0;
    }
}
