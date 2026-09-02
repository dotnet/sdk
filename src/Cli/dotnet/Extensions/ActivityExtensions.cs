// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics;

namespace Microsoft.DotNet.Cli.Extensions;

internal static class ActivityExtensions
{
    public static void SetDisplayName(this Activity? activity, ParseResult parseResult)
    {
        if (activity is null)
        {
            return;
        }

        var name = parseResult.GetCommandName();
        activity.DisplayName = name;
        activity.SetTag("command.name", name);
    }
}
