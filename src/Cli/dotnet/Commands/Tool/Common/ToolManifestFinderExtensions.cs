// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.ToolManifest;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Tools.Tool.Common;

internal static class ToolManifestFinderExtensions
{
    public static (FilePath? filePath, string warningMessage) ExplicitManifestOrFindManifestContainPackageId(
        this IToolManifestFinder toolManifestFinder,
        string explicitManifestFile,
        PackageId packageId)
    {
        if (!string.IsNullOrWhiteSpace(explicitManifestFile))
        {
            return (new FilePath(explicitManifestFile), null);
        }

        IReadOnlyList<FilePath> manifestFilesContainPackageId;
        try
        {
            manifestFilesContainPackageId = toolManifestFinder.FindByPackageId(packageId);
        }
        catch (ToolManifestCannotBeFoundException e)
        {
            throw new GracefulException([e.Message, LocalizableStrings.NoManifestGuide], verboseMessages: [e.VerboseMessage], isUserError: false);
        }

        if (manifestFilesContainPackageId.Any())
        {
            string warning = null;
            if (manifestFilesContainPackageId.Count > 1)
            {
                warning =
                    string.Format(
                        LocalizableStrings.SamePackageIdInOtherManifestFile,
                        string.Join(
                            Environment.NewLine,
                            manifestFilesContainPackageId.Skip(1).Select(m => $"\t{m}")));
            }

            return (manifestFilesContainPackageId.First(), warning);
        }

        return (null, null);
    }
}
