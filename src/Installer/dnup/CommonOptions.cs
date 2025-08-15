// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Text;

namespace Microsoft.DotNet.Tools.Bootstrapper;

internal class CommonOptions
{
    public static Option<bool> InteractiveOption = new("--interactive")
    {
        Description = "Allows the command to stop and wait for user input or action (for example to complete authentication).",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = _ => !IsCIEnvironmentOrRedirected()
    };


    private static bool IsCIEnvironmentOrRedirected() =>
        new Cli.Telemetry.CIEnvironmentDetectorForTelemetry().IsCIEnvironment() || Console.IsOutputRedirected;
}
