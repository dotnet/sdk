// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Sdk.Add;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Add.Sdk;

internal sealed class AddSdkCommandDefinition() : SdkAddCommandDefinitionBase(Name)
{
    public new const string Name = "sdk";

    public AddCommandDefinition Parent => (AddCommandDefinition)Parents.Single();

    public override Argument<string>? GetProjectOrFileArgument()
        => Parent.ProjectOrFileArgument;
}
