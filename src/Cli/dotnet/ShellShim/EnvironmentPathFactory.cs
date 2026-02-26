// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Cli.ShellShim;

internal static class EnvironmentPathFactory
{
    public static IEnvironmentPath CreateEnvironmentPath(
        bool isDotnetBeingInvokedFromNativeInstaller = false,
        IEnvironmentProvider? environmentProvider = null)
    {
        IEnvironmentPath? environmentPath = null;

#if !DOT_NET_BUILD_FROM_SOURCE
        if (OperatingSystem.IsWindows())
        {
            // On Windows MSI will in charge of appending ToolShimPath

            if (!isDotnetBeingInvokedFromNativeInstaller)
            {
                environmentPath = new WindowsEnvironmentPath(
                    CliFolderPathCalculator.ToolsShimPath,
                    CliFolderPathCalculator.WindowsNonExpandedToolsShimPath,
                    environmentProvider ?? new EnvironmentProvider(),
                    new WindowsRegistryEnvironmentPathEditor(),
                    Reporter.Output);
            }
        }
        else
#endif
        if (OperatingSystem.IsLinux() && isDotnetBeingInvokedFromNativeInstaller)
        {
            environmentPath = new LinuxEnvironmentPath(
                CliFolderPathCalculator.ToolsShimPathInUnix,
                Reporter.Output,
                environmentProvider ?? new EnvironmentProvider(),
                new FileWrapper());
        }
        else if (OperatingSystem.IsMacOS() && isDotnetBeingInvokedFromNativeInstaller)
        {
            environmentPath = new OsxBashEnvironmentPath(
                executablePath: CliFolderPathCalculator.ToolsShimPathInUnix,
                reporter: Reporter.Output,
                environmentProvider: environmentProvider ?? new EnvironmentProvider(),
                fileSystem: new FileWrapper());
        }

        return environmentPath ?? new DoNothingEnvironmentPath();
    }

    public static IEnvironmentPathInstruction CreateEnvironmentPathInstruction(
        IEnvironmentProvider? environmentProvider = null)
    {
        environmentProvider ??= new EnvironmentProvider();

        if (OperatingSystem.IsMacOS() && ZshDetector.IsZshTheUsersShell(environmentProvider))
        {
            return new OsxZshEnvironmentPathInstruction(
                executablePath: CliFolderPathCalculator.ToolsShimPathInUnix,
                reporter: Reporter.Output,
                environmentProvider: environmentProvider);
        }

#if !DOT_NET_BUILD_FROM_SOURCE
        if (OperatingSystem.IsWindows())
        {
            return new WindowsEnvironmentPath(
                CliFolderPathCalculator.ToolsShimPath,
                nonExpandedPackageExecutablePath: CliFolderPathCalculator.WindowsNonExpandedToolsShimPath,
                expandedEnvironmentReader: environmentProvider,
                environmentPathEditor: new WindowsRegistryEnvironmentPathEditor(),
                reporter: Reporter.Output);
        }
#endif

        return CreateEnvironmentPath(true, environmentProvider);
    }
}
