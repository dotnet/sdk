// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Dotnet.Installation;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

/// <summary>
/// Determines which project global.json file and sibling .dotnet directory a local SDK install should use.
/// Assumes the nearest global.json found by walking up from the starting directory owns that project scope.
/// </summary>
internal sealed record LocalSdkSetupPlan(
    string ProjectDirectory,
    string GlobalJsonPath,
    string LocalDotnetPath,
    bool GlobalJsonExisted,
    GlobalJsonInfo GlobalJsonInfo)
{
    public static LocalSdkSetupPlan Create(string startDirectory, string? requestedChannel)
    {
        string fullStartDirectory = Path.GetFullPath(startDirectory);
        GlobalJsonInfo globalJsonInfo;
        try
        {
            globalJsonInfo = GlobalJsonModifier.GetGlobalJsonInfo(fullStartDirectory);
        }
        catch (JsonException ex)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.UserConfigurationCorrupted,
                $"The global.json file found while resolving local SDK setup is not valid JSON: {ex.Message}",
                ex);
        }

        bool globalJsonExisted = globalJsonInfo.GlobalJsonPath is not null;
        string projectDirectory = globalJsonExisted
            ? Path.GetDirectoryName(globalJsonInfo.GlobalJsonPath!)!
            : fullStartDirectory;
        string globalJsonPath = globalJsonInfo.GlobalJsonPath ?? Path.Combine(projectDirectory, "global.json");

        if (globalJsonExisted
            && string.IsNullOrWhiteSpace(globalJsonInfo.SdkVersion)
            && string.IsNullOrWhiteSpace(requestedChannel))
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.ContextResolutionFailed,
                $"The global.json file at '{globalJsonPath}' does not specify sdk.version. Specify the SDK channel or version to install locally.");
        }

        string localDotnetPath = Path.GetFullPath(Path.Combine(projectDirectory, ".dotnet"));
        RejectReparsePoint(localDotnetPath);

        globalJsonInfo.GlobalJsonPath = globalJsonPath;
        return new LocalSdkSetupPlan(
            projectDirectory,
            globalJsonPath,
            localDotnetPath,
            globalJsonExisted,
            globalJsonInfo);
    }

    private static void RejectReparsePoint(string localDotnetPath)
    {
        if (!Directory.Exists(localDotnetPath))
        {
            return;
        }

        if ((File.GetAttributes(localDotnetPath) & FileAttributes.ReparsePoint) != 0)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.ContextResolutionFailed,
                $"The local SDK directory '{localDotnetPath}' is a symlink or reparse point. Remove it or replace it with a regular directory before using --local.");
        }
    }
}
