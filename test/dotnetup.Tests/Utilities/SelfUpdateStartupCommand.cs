// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

internal sealed class SelfUpdateStartupCommand(ParseResult parseResult) : CommandBase(parseResult, "selfupdate-startup-test")
{
    public bool Ran { get; private set; }

    protected override void ExecuteCore() => Ran = true;
}