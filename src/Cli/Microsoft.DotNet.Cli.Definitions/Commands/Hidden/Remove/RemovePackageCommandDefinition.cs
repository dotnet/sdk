// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Package.Remove;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Remove.Package;

internal sealed class RemovePackageCommandDefinition() : PackageRemoveCommandDefinitionBase(Name)
{
    public new const string Name = "package";
}
