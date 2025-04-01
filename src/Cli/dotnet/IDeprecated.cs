// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;
namespace Microsoft.DotNet.Cli;

public interface IDeprecated
{
    public static Dictionary<CliOption, (string messageToShow, Version errorLevel)> DeprecatedOptions { get; } = new()
    {
        { PackageListCommandParser.DeprecatedOption, ("old versions are old", new Version(10, 0, 100)) }
    };

    public static Dictionary<CliArgument, (string messageToShow, Version errorLevel)> DeprecatedArguments { get; } = new()
    {
        { PackageAddCommandParser.CmdPackageArgument, ("argument bad; use option", new Version(10, 0, 100)) }
    };

    public bool IsDeprecated { get; set; }

    public static void WarnIfNecessary(IReporter reporter, string messageToShow, Version errorLevel)
    {
        reporter.WriteLine(messageToShow);
    }
}