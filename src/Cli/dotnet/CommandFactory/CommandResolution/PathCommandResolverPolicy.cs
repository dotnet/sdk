// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.CommandFactory.CommandResolution;

public class PathCommandResolverPolicy : ICommandResolverPolicy
{
    public CompositeCommandResolver CreateCommandResolver(string currentWorkingDirectory = null)
    {
        return Create();
    }

    public static CompositeCommandResolver Create()
    {
        var environment = new EnvironmentProvider();

        IPlatformCommandSpecFactory platformCommandSpecFactory;
        if (OperatingSystem.IsWindows())
        {
            platformCommandSpecFactory = new WindowsExePreferredCommandSpecFactory();
        }
        else
        {
            platformCommandSpecFactory = new GenericPlatformCommandSpecFactory();
        }

        return CreatePathCommandResolverPolicy(
            environment,
            platformCommandSpecFactory);
    }

    public static CompositeCommandResolver CreatePathCommandResolverPolicy(
        IEnvironmentProvider environment,
        IPlatformCommandSpecFactory platformCommandSpecFactory)
    {
        var compositeCommandResolver = new CompositeCommandResolver();

        compositeCommandResolver.AddCommandResolver(
            new PathCommandResolver(environment, platformCommandSpecFactory));

        return compositeCommandResolver;
    }
}
