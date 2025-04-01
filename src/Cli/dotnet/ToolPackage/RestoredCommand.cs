// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Cli.ToolPackage;

internal class RestoredCommand(
    ToolCommandName name,
    string runner,
    FilePath executable)
{
    public ToolCommandName Name { get; private set; } = name;

    public string Runner { get; private set; } = runner ?? throw new ArgumentNullException(nameof(runner));

    public FilePath Executable { get; private set; } = executable;

    public string DebugToString()
    {
        return $"ToolCommandName: {Name.Value} - Runner: {Runner} - FilePath: {Executable.Value}";
    }
}
