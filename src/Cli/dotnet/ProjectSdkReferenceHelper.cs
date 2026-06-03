// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli;

internal enum SdkAddResult
{
    Added,
    Updated,
    Unchanged,
}

/// <summary>
/// Reads and updates SDK references in SDK-style MSBuild project files.
/// </summary>
internal static class ProjectSdkReferenceHelper
{
    /// <summary>
    /// Returns whether the project file already references the given SDK.
    /// </summary>
    public static bool ContainsSdk(ProjectRootElement project, string sdkName)
    {
        ArgumentException.ThrowIfNullOrEmpty(sdkName);

        var attributeSdks = ParseSdkAttribute(project.Sdk);
        if (attributeSdks.Exists(s => string.Equals(s.Name, sdkName, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return GetSdkElements(project).Any(s => string.Equals(s.Name, sdkName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Adds or updates an SDK reference in the project file.
    /// When <paramref name="leaveExistingUnchangedWhenVersionIsNull"/> is <see langword="true"/> and
    /// <paramref name="version"/> is <see langword="null"/>, an existing reference is left unchanged.
    /// </summary>
    public static SdkAddResult AddOrUpdateSdk(
        ProjectRootElement project,
        string sdkName,
        string? version,
        bool leaveExistingUnchangedWhenVersionIsNull)
    {
        ArgumentException.ThrowIfNullOrEmpty(sdkName);

        var attributeSdks = ParseSdkAttribute(project.Sdk);
        for (int i = 0; i < attributeSdks.Count; i++)
        {
            if (string.Equals(attributeSdks[i].Name, sdkName, StringComparison.OrdinalIgnoreCase))
            {
                if (leaveExistingUnchangedWhenVersionIsNull && version is null)
                {
                    return SdkAddResult.Unchanged;
                }

                attributeSdks[i] = new SdkReference(sdkName, version);
                project.Sdk = FormatSdkAttribute(attributeSdks);
                return SdkAddResult.Updated;
            }
        }

        foreach (var sdkElement in GetSdkElements(project))
        {
            if (string.Equals(sdkElement.Name, sdkName, StringComparison.OrdinalIgnoreCase))
            {
                if (leaveExistingUnchangedWhenVersionIsNull && version is null)
                {
                    return SdkAddResult.Unchanged;
                }

                SetSdkElementVersion(sdkElement, version);
                return SdkAddResult.Updated;
            }
        }

        var newSdkElement = project.CreateProjectSdkElement(sdkName, version ?? string.Empty);
        SetSdkElementVersion(newSdkElement, version);
        InsertAdditiveSdkElement(project, newSdkElement);

        return SdkAddResult.Added;
    }

    /// <summary>
    /// Removes an SDK reference from the project file.
    /// </summary>
    /// <returns><see langword="true"/> if the SDK reference was removed.</returns>
    public static bool TryRemoveSdk(ProjectRootElement project, string sdkName)
    {
        ArgumentException.ThrowIfNullOrEmpty(sdkName);

        var attributeSdks = ParseSdkAttribute(project.Sdk);
        for (int i = 0; i < attributeSdks.Count; i++)
        {
            if (!string.Equals(attributeSdks[i].Name, sdkName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsPrimarySdkReference(project, sdkName))
            {
                throw new GracefulException(CliCommandStrings.CannotRemovePrimarySdkReference, sdkName);
            }

            attributeSdks.RemoveAt(i);
            project.Sdk = FormatSdkAttribute(attributeSdks);
            return true;
        }

        foreach (var sdkElement in GetSdkElements(project).ToList())
        {
            if (!string.Equals(sdkElement.Name, sdkName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsPrimarySdkReference(project, sdkName))
            {
                throw new GracefulException(CliCommandStrings.CannotRemovePrimarySdkReference, sdkName);
            }

            project.RemoveChild(sdkElement);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns whether the given SDK name is the primary SDK for the project.
    /// </summary>
    public static bool IsPrimarySdkReference(ProjectRootElement project, string sdkName)
    {
        ArgumentException.ThrowIfNullOrEmpty(sdkName);

        return TryGetPrimarySdk(project, out string primaryName, out _) &&
               string.Equals(primaryName, sdkName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetPrimarySdk(ProjectRootElement project, out string name, out string? version)
    {
        name = string.Empty;
        version = null;

        var attributeSdks = ParseSdkAttribute(project.Sdk);
        if (attributeSdks.Count > 0)
        {
            name = attributeSdks[0].Name;
            version = attributeSdks[0].Version;
            return true;
        }

        var firstSdkElement = GetSdkElements(project).FirstOrDefault();
        if (firstSdkElement is null)
        {
            return false;
        }

        name = firstSdkElement.Name;
        version = string.IsNullOrEmpty(firstSdkElement.Version) ? null : firstSdkElement.Version;
        return true;
    }

    /// <summary>
    /// Inserts an additive <c>&lt;Sdk&gt;</c> after existing SDK elements to preserve import order.
    /// </summary>
    private static void InsertAdditiveSdkElement(ProjectRootElement project, ProjectSdkElement newSdkElement)
    {
        var sdkElements = GetSdkElements(project).ToList();
        if (sdkElements.Count > 0)
        {
            project.InsertAfterChild(newSdkElement, sdkElements[^1]);
            return;
        }

        var insertBefore = project.Children.FirstOrDefault();
        if (insertBefore is null)
        {
            project.AppendChild(newSdkElement);
        }
        else
        {
            project.InsertBeforeChild(newSdkElement, insertBefore);
        }
    }

    private static IEnumerable<ProjectSdkElement> GetSdkElements(ProjectRootElement project)
        => project.Children.OfType<ProjectSdkElement>();

    private static List<SdkReference> ParseSdkAttribute(string? sdkAttribute)
    {
        if (string.IsNullOrWhiteSpace(sdkAttribute))
        {
            return [];
        }

        return sdkAttribute
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseSingleSdkReference)
            .ToList();
    }

    private static SdkReference ParseSingleSdkReference(string reference)
    {
        var slashIndex = reference.IndexOf('/');
        if (slashIndex >= 0)
        {
            return new SdkReference(reference[..slashIndex], reference[(slashIndex + 1)..]);
        }

        return new SdkReference(reference, null);
    }

    private static string FormatSdkAttribute(IReadOnlyList<SdkReference> sdks)
        => string.Join(";", sdks.Select(FormatSingleSdkReference));

    private static string FormatSingleSdkReference(SdkReference sdk)
        => sdk.Version is null ? sdk.Name : $"{sdk.Name}/{sdk.Version}";

    private static void SetSdkElementVersion(ProjectSdkElement sdkElement, string? version)
    {
        if (version is null)
        {
            sdkElement.Version = string.Empty;
        }
        else
        {
            sdkElement.Version = version;
        }
    }

    private readonly record struct SdkReference(string Name, string? Version);
}
