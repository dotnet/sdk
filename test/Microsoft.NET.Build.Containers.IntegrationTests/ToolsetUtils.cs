// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.IntegrationTests;

internal static class ToolsetUtils
{
    private const string ContainersPackageId = "Microsoft.NET.Build.Containers";

    /// <summary>
    /// Gets path to RuntimeIdentifierGraph.json file.
    /// </summary>
    /// <returns></returns>
    internal static string GetRuntimeGraphFilePath()
    {
        return SdkTestContext.GetRuntimeGraphFilePath();
    }

    internal static IManifestPicker RidGraphManifestPicker { get; } = new RidGraphManifestPicker(GetRuntimeGraphFilePath());

    /// <summary>
    /// Gets path to built Microsoft.NET.Build.Containers.*.nupkg prepared for tests.
    /// </summary>
    /// <returns></returns>
    internal static (string? PackagePath, string? PackageVersion) GetContainersPackagePath()
    {
        string packageDir = Path.Combine(SdkTestContext.Current.TestExecutionDirectory, "Container", "package");
        string[] packagePaths = Directory.GetFiles(packageDir, $"{ContainersPackageId}.*.nupkg");
        if (packagePaths.Length == 1)
        {
            string packageVersion = Path.GetFileNameWithoutExtension(packagePaths[0])[(ContainersPackageId.Length + 1)..];
            return (packagePaths[0], packageVersion);
        }

        throw new FileNotFoundException(
            $"Expected exactly one {ContainersPackageId}.*.nupkg in {packageDir}, but found {packagePaths.Length}. You may need to rerun the build.");
    }
}
