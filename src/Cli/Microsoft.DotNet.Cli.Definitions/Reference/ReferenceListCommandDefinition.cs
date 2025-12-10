// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Reference.List;

internal static class ReferenceListCommandDefinition
{
    public const string Name = "list";

    public static Command Create()
        => new(Name, CliCommandStrings.ReferenceListAppFullName);
}
