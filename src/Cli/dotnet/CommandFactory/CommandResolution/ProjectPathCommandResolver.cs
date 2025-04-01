// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;

namespace Microsoft.DotNet.Cli.CommandFactory.CommandResolution;

public class ProjectPathCommandResolver(IEnvironmentProvider environment,
    IPlatformCommandSpecFactory commandSpecFactory) : AbstractPathBasedCommandResolver(environment, commandSpecFactory)
{
    internal override string ResolveCommandPath(CommandResolverArguments commandResolverArguments)
    {
        if (commandResolverArguments.ProjectDirectory == null)
        {
            return null;
        }

        return _environment.GetCommandPathFromRootPath(
            commandResolverArguments.ProjectDirectory,
            commandResolverArguments.CommandName,
            commandResolverArguments.InferredExtensions.OrEmptyIfNull());
    }
}
