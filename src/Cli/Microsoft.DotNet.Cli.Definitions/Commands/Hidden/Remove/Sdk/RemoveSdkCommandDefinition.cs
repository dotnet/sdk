// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Sdk.Remove;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Remove.Sdk;

internal sealed class RemoveSdkCommandDefinition() : SdkRemoveCommandDefinitionBase(Name)
{
    public new const string Name = "sdk";

    public RemoveCommandDefinition Parent => (RemoveCommandDefinition)Parents.Single();

    public override Argument<string>? GetProjectOrFileArgument()
        => Parent.ProjectOrFileArgument;
}
