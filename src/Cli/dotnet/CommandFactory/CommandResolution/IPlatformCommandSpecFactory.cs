// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.CommandFactory.CommandResolution;

public interface IPlatformCommandSpecFactory
{
    CommandSpec CreateCommandSpec(
       string commandName,
       IEnumerable<string> args,
       string commandPath,
       IEnvironmentProvider environment);
}
