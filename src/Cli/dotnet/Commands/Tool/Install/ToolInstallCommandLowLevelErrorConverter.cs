// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.ShellShim;
using Microsoft.DotNet.Cli.ToolPackage;

namespace Microsoft.DotNet.Cli.Commands.Tool.Install;

internal static class InstallToolCommandLowLevelErrorConverter
{
    public static IEnumerable<string> GetUserFacingMessages(Exception ex, PackageId packageId)
    {
        if (ex is ToolConfigurationException)
        {
            yield return string.Format(CliCommandStrings.InvalidToolConfiguration, ex.Message);
            yield return string.Format(CliCommandStrings.ToolInstallationFailedContactAuthor, packageId);
        }
        else if (ex is ShellShimException)
        {
            yield return string.Format(CliCommandStrings.FailedToCreateToolShim, packageId, ex.Message);
            yield return string.Format(CliCommandStrings.ToolInstallationFailed, packageId);
        }
    }

    public static bool ShouldConvertToUserFacingError(Exception ex)
    {
        return ex is ToolConfigurationException
               || ex is ShellShimException;
    }
}
