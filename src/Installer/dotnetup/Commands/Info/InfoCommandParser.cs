// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Info;

internal static class InfoCommandParser
{
    public static readonly Option<bool> JsonOption = new("--json")
    {
        Description = Strings.InfoJsonOptionDescription,
        Arity = ArgumentArity.ZeroOrOne
    };
}
