// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using NuGet.Protocol;

namespace Microsoft.DotNet.Cli.Commands.Tool;

internal static class ToolDeprecationWarning
{
    public static void PrintDeprecationWarning(IReporter errorReporter, PackageId packageId, PackageDeprecationMetadata? deprecationMetadata)
    {
        var warning = FormatDeprecationWarning(packageId, deprecationMetadata);
        if (warning != null)
        {
            errorReporter.WriteLine(warning.Yellow());
        }
    }

    public static string? FormatDeprecationWarning(PackageId packageId, PackageDeprecationMetadata? deprecationMetadata)
    {
        if (deprecationMetadata == null)
        {
            return null;
        }

        var sb = new StringBuilder();
        sb.AppendLine(string.Format(CliCommandStrings.ToolPackageDeprecated, packageId));

        if (deprecationMetadata.Reasons?.Any() == true)
        {
            sb.AppendLine(string.Format(CliCommandStrings.ToolPackageDeprecatedReasons, string.Join(", ", deprecationMetadata.Reasons)));
        }

        if (!string.IsNullOrWhiteSpace(deprecationMetadata.Message))
        {
            sb.AppendLine(string.Format(CliCommandStrings.ToolPackageDeprecatedMessage, deprecationMetadata.Message));
        }

        if (deprecationMetadata.AlternatePackage != null)
        {
            var alternatePackageId = deprecationMetadata.AlternatePackage.PackageId;
            var range = deprecationMetadata.AlternatePackage.Range?.ToString();
            var alternateDescription = string.IsNullOrEmpty(range) || range == "*"
                ? alternatePackageId
                : $"{alternatePackageId} ({range})";
            sb.AppendLine(string.Format(CliCommandStrings.ToolPackageDeprecatedAlternativePackage, alternateDescription));
        }

        return sb.ToString().TrimEnd();
    }
}
