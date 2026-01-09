// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.BuildServer.Shutdown;

internal sealed class BuildServerShutdownCommandDefinition : Command
{
    public readonly Option<bool> MSBuildOption = new("--msbuild")
    {
        Description = CommandDefinitionStrings.MSBuildOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public readonly Option<bool> VbcsOption = new("--vbcscompiler")
    {
        Description = CommandDefinitionStrings.VBCSCompilerOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public readonly Option<bool> RazorOption = new("--razor")
    {
        Description = CommandDefinitionStrings.RazorOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public BuildServerShutdownCommandDefinition()
        : base("shutdown", CommandDefinitionStrings.BuildServerShutdownCommandDescription)
    {
        Options.Add(MSBuildOption);
        Options.Add(VbcsOption);
        Options.Add(RazorOption);
    }
}
