// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
namespace Microsoft.DotNet.Cli;

public interface IDeprecated
{
    public static Dictionary<CliOption, (string messageToShow, ReleaseVersion errorLevel)> DeprecatedOptions { get; } = new()
    {
        { PackageListCommandParser.DeprecatedOption, ("old versions are old", new ReleaseVersion(10, 0, 100)) }
    };

    public static Dictionary<CliArgument, (string messageToShow, ReleaseVersion errorLevel)> DeprecatedArguments { get; } = new()
    {
        { PackageAddCommandParser.CmdPackageArgument, ("argument bad; use option", new ReleaseVersion(10, 0, 100)) }
    };

    public bool IsDeprecated { get; set; }

    public static void WarnIfNecessary(IReporter reporter, string messageToShow, ReleaseVersion errorLevel)
    {
        if (new ReleaseVersion(Utils.Product.Version) < errorLevel)
        {
            reporter.WriteLine(messageToShow);
        }
        else
        {
            reporter.WriteLine(messageToShow.Yellow());
        }
    }
}