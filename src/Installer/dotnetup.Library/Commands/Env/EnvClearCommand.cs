// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

/// <summary>
/// Fully removes everything dotnetup wrote into the environment: equivalent to
/// <c>env set none --dotnetup-on-path off</c>. Removes the managed profile block and the
/// dotnetup PATH entry, leaving no dotnet exposure and no dotnetup-on-PATH. The closest thing
/// to an "env uninstall" since dotnetup has no uninstall command.
/// </summary>
internal class EnvClearCommand : CommandBase
{
    private readonly IDotnetEnvironmentManager _dotnetEnvironment;
    private readonly IEnvironmentStateInspector _inspector;
    private readonly IEnvShellProvider? _shellProvider;

    public EnvClearCommand(ParseResult result, IDotnetEnvironmentManager? dotnetEnvironment = null, IEnvironmentStateInspector? inspector = null) : base(result)
    {
        _dotnetEnvironment = dotnetEnvironment ?? new DotnetEnvironmentManager();
        _inspector = inspector ?? new EnvironmentStateInspector(_dotnetEnvironment);
        _shellProvider = result.GetValue(CommonOptions.ShellOption);
    }

    protected override string GetCommandName() => "env clear";

    protected override void ExecuteCore()
    {
        EnvSettingsWriter.ApplyAndPersist(
            PathPreference.None,
            targetDotnetupOnPath: false,
            _dotnetEnvironment,
            _shellProvider,
            _inspector);
    }
}
