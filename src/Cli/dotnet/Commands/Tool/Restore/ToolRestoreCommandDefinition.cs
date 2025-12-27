// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Common;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Tool.Restore;

internal static class ToolRestoreCommandDefinition
{
    public static readonly Option<string> ConfigOption = ToolInstallCommandParser.ConfigOption;

    public static readonly Option<string[]> AddSourceOption = ToolInstallCommandParser.AddSourceOption;

    public static readonly Option<string> ToolManifestOption = ToolAppliedOption.ToolManifestOption(CliCommandStrings.ToolRestoreManifestPathOptionDescription);

    public static readonly Option<Utils.VerbosityOptions> VerbosityOption = ToolInstallCommandParser.VerbosityOption;

    public static Command Create()
    {
        Command command = new("restore", CliCommandStrings.ToolRestoreCommandDescription);

        command.Options.Add(ConfigOption);
        command.Options.Add(AddSourceOption);
        command.Options.Add(ToolManifestOption);
        command.Options.Add(ToolCommandRestorePassThroughOptions.DisableParallelOption);
        command.Options.Add(ToolCommandRestorePassThroughOptions.IgnoreFailedSourcesOption);
        command.Options.Add(ToolCommandRestorePassThroughOptions.NoCacheOption);
        command.Options.Add(ToolCommandRestorePassThroughOptions.NoHttpCacheOption);
        command.Options.Add(ToolCommandRestorePassThroughOptions.InteractiveRestoreOption);
        command.Options.Add(VerbosityOption);

        return command;
    }
}
