// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Package.Add;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Add.Package;

internal sealed class AddPackageCommandDefinition() : PackageAddCommandDefinitionBase(Name)
{
    public new const string Name = "package";

    public AddCommandDefinition Parent => (AddCommandDefinition)Parents.Single();

    public override Argument<string>? GetProjectOrFileArgument()
        => Parent.ProjectOrFileArgument;
}
