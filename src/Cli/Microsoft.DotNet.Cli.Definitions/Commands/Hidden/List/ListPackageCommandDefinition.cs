// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Package.List;

namespace Microsoft.DotNet.Cli.Commands.Hidden.List.Package;

internal sealed class ListPackageCommandDefinition() : PackageListCommandDefinitionBase(Name)
{
    public new const string Name = "package";

    public ListCommandDefinition Parent => (ListCommandDefinition)Parents.Single();

    public override string? GetFileOrDirectory(ParseResult parseResult)
        => parseResult.GetValue(Parent.SlnOrProjectArgument);
}
