// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
#if !CLI_AOT
using NuGet.Common;
#endif

namespace Microsoft.DotNet.Configurer
{
    public static class CliFolderPathCalculator
    {
        public const string DotnetHomeVariableName = CliFolderPathCalculatorCore.DotnetHomeVariableName;
        private const string DotnetProfileDirectoryName = CliFolderPathCalculatorCore.DotnetProfileDirectoryName;
#if !CLI_AOT
        private const string ToolsShimFolderName = "tools";
        private const string ToolsResolverCacheFolderName = "toolResolverCache";

        public static string CliFallbackFolderPath =>
            Environment.GetEnvironmentVariable("DOTNET_CLI_TEST_FALLBACKFOLDER") ??
            Path.Combine(new DirectoryInfo(AppContext.BaseDirectory).Parent?.FullName ?? string.Empty, "NuGetFallbackFolder");

        public static string ToolsShimPath => Path.Combine(DotnetUserProfileFolderPath, ToolsShimFolderName);

        public static string ToolsPackagePath =>
            ToolPackageFolderPathCalculator.GetToolPackageFolderPath(ToolsShimPath);

        public static BashPathUnderHomeDirectory ToolsShimPathInUnix =>
            new(
                DotnetHomePath,
                Path.Combine(DotnetProfileDirectoryName, ToolsShimFolderName));

        public static string WindowsNonExpandedToolsShimPath
        {
            get
            {
                return string.IsNullOrEmpty(Environment.GetEnvironmentVariable(DotnetHomeVariableName))
                    ? $@"%USERPROFILE%\{DotnetProfileDirectoryName}\{ToolsShimFolderName}"
                    : ToolsShimPath;
            }
        }
#endif

        public static string DotnetUserProfileFolderPath =>
            Path.Combine(DotnetHomePath, DotnetProfileDirectoryName);

#if !CLI_AOT
        public static string ToolsResolverCachePath => Path.Combine(DotnetUserProfileFolderPath, ToolsResolverCacheFolderName);
#endif

        public static string DotnetHomePath
        {
            get
            {
                return new CliFolderPathCalculatorCore().GetDotnetHomePath()
#if CLI_AOT
                    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
#else
                    ?? throw new ConfigurationException(
                            string.Format(
                                LocalizableStrings.FailedToDetermineUserHomeDirectory,
                                DotnetHomeVariableName))
                        .DisplayAsError();
#endif
            }
        }

#if !CLI_AOT
        public static string NuGetUserSettingsDirectory =>
            NuGetEnvironment.GetFolderPath(NuGetFolderPath.UserSettingsDirectory);
#endif
    }
}
