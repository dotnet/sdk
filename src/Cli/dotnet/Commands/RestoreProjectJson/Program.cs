// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.RestoreProjectJson;

public partial class RestoreProjectJsonCommand
{
    public static int Run(string[] args)
    {
        DebugHelper.HandleDebugSwitch(ref args);

        return NuGet3.Restore(args);
    }
}
