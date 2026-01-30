// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Parse;

internal sealed class ParseCommandDefinition : Command
{
    public ParseCommandDefinition()
        : base("parse")
    {
        Hidden = true;
    }
}
