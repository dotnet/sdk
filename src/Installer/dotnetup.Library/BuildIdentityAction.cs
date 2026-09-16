// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>Reports the loaded identity without constructing a command or enabling telemetry.</summary>
internal sealed class BuildIdentityAction : SynchronousCommandLineAction
{
    public override int Invoke(ParseResult parseResult)
    {
        parseResult.InvocationConfiguration.Output.WriteLine(DotnetupProcessInfo.BuildIdentity);
        return 0;
    }
}