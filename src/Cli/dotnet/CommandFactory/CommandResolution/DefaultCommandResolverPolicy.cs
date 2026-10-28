// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.CommandFactory.CommandResolution;

public class DefaultCommandResolverPolicy : ICommandResolverPolicy
{
    public CompositeCommandResolver CreateCommandResolver(string? sdkRoot = null, string? currentWorkingDirectory = null)
    {
        return Create(sdkRoot: sdkRoot, currentWorkingDirectory: currentWorkingDirectory);
    }

    public static CompositeCommandResolver Create(string? sdkRoot = null,string? currentWorkingDirectory = null)
    {
        var environment = new EnvironmentProvider();
        var publishedPathCommandSpecFactory = new PublishPathCommandSpecFactory();

        IPlatformCommandSpecFactory platformCommandSpecFactory;
        if (OperatingSystem.IsWindows())
        {
            platformCommandSpecFactory = new WindowsExePreferredCommandSpecFactory();
        }
        else
        {
            platformCommandSpecFactory = new GenericPlatformCommandSpecFactory();
        }

        return CreateDefaultCommandResolver(
            environment,
#if !CLI_AOT
            new PackagedCommandSpecFactoryWithCliRuntime(),
#endif
            platformCommandSpecFactory,
            publishedPathCommandSpecFactory,
            sdkRoot,
            currentWorkingDirectory);
    }

    public static CompositeCommandResolver CreateDefaultCommandResolver(
        IEnvironmentProvider environment,
#if !CLI_AOT
        IPackagedCommandSpecFactory packagedCommandSpecFactory,
#endif
        IPlatformCommandSpecFactory platformCommandSpecFactory,
        IPublishedPathCommandSpecFactory publishedPathCommandSpecFactory,
        string? sdkRoot = null,
        string? currentWorkingDirectory = null)
    {
        var compositeCommandResolver = new CompositeCommandResolver();

        compositeCommandResolver.AddCommandResolver(new MuxerCommandResolver());
        if (sdkRoot != null)
        {
            compositeCommandResolver.AddCommandResolver(DotnetToolsCommandResolver.ForSdkRoot(sdkRoot));
        }
        else
        {
            compositeCommandResolver.AddCommandResolver(new DotnetToolsCommandResolver());
        }
        compositeCommandResolver.AddCommandResolver(new LocalToolsCommandResolver(currentWorkingDirectory: currentWorkingDirectory));
        compositeCommandResolver.AddCommandResolver(new RootedCommandResolver());
#if !CLI_AOT
        // ProjectToolsCommandResolver resolves legacy DotNetCliToolReference tools by evaluating the
        // project with MSBuild and reading the NuGet lock file - neither of which is AOT-compatible.
        // The AOT bridge omits it and falls back to the managed CLI for these (rare) invocations.
        compositeCommandResolver.AddCommandResolver(
            new ProjectToolsCommandResolver(packagedCommandSpecFactory, environment));
#endif
        compositeCommandResolver.AddCommandResolver(new AppBaseDllCommandResolver());
        compositeCommandResolver.AddCommandResolver(
            new AppBaseCommandResolver(environment, platformCommandSpecFactory));
        compositeCommandResolver.AddCommandResolver(
            new PathCommandResolver(environment, platformCommandSpecFactory));
        compositeCommandResolver.AddCommandResolver(
            new PublishedPathCommandResolver(environment, publishedPathCommandSpecFactory));

        return compositeCommandResolver;
    }
}
