// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Cli.Commands.Test;

internal sealed class HelpEventArgs : EventArgs
{
    public string ModulePath { get; set; }

    public CommandLineOption[] CommandLineOptions { get; set; }
}
