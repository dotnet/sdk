// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.ShellShim;
using Microsoft.DotNet.Cli.ToolPackage;

namespace Microsoft.DotNet.Cli.Commands.Tool.Uninstall;

internal static class ToolUninstallCommandLowLevelErrorConverter
{
    public static IEnumerable<string> GetUserFacingMessages(Exception ex, PackageId packageId)
    {
        string[] userFacingMessages = null;
        if (ex is ToolPackageException)
        {
            userFacingMessages = [string.Format(CliStrings.FailedToUninstallToolPackage, packageId, ex.Message)];
        }
        else if (ex is ToolConfigurationException || ex is ShellShimException)
        {
            userFacingMessages = [string.Format(CliCommandStrings.FailedToUninstallTool, packageId, ex.Message)];
        }

        return userFacingMessages;
    }

    public static bool ShouldConvertToUserFacingError(Exception ex)
    {
        return ex is ToolPackageException
               || ex is ToolConfigurationException
               || ex is ShellShimException;
    }
}
