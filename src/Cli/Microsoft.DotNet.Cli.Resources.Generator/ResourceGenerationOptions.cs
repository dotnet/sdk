// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.DotNet.Cli.Resources.Generator;

/// <summary>
///  Contains validated names and paths used to generate one resource class.
/// </summary>
internal sealed class ResourceGenerationOptions
{
    private ResourceGenerationOptions(
        ResourceInput input,
        string resourceName,
        string resourceFamily,
        string className,
        string? namespaceName,
        string classIdentifier,
        string hintName)
    {
        Input = input;
        ResourceName = resourceName;
        ResourceFamily = resourceFamily;
        ClassName = className;
        NamespaceName = namespaceName;
        ClassIdentifier = classIdentifier;
        HintName = hintName;
    }

    /// <summary>
    ///  Gets the source resource input.
    /// </summary>
    internal ResourceInput Input { get; }

    /// <summary>
    ///  Gets the manifest resource name used for lookup.
    /// </summary>
    internal string ResourceName { get; }

    /// <summary>
    ///  Gets the neutral resource family used to group localized siblings.
    /// </summary>
    internal string ResourceFamily { get; }

    /// <summary>
    ///  Gets the fully qualified generated class name.
    /// </summary>
    internal string ClassName { get; }

    /// <summary>
    ///  Gets the generated namespace, or <see langword="null"/> for the global namespace.
    /// </summary>
    internal string? NamespaceName { get; }

    /// <summary>
    ///  Gets the unqualified generated class identifier.
    /// </summary>
    internal string ClassIdentifier { get; }

    /// <summary>
    ///  Gets the source-generator hint name.
    /// </summary>
    internal string HintName { get; }

    /// <summary>
    ///  Tries to create validated generation options for a resource input.
    /// </summary>
    /// <param name="input">The resource input.</param>
    /// <param name="assemblyName">The consuming assembly name.</param>
    /// <param name="options">Receives the validated options when successful.</param>
    /// <returns>
    ///  <see langword="true"/> when the generated class and namespace are valid; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    internal static bool TryCreate(
        ResourceInput input,
        string? assemblyName,
        out ResourceGenerationOptions? options)
    {
        string defaultName = GetDefaultResourceAndClassName(input, assemblyName);
        string resourceName = input.ManifestResourceName is null || input.ManifestResourceName.Length == 0
            ? defaultName
            : input.ManifestResourceName;

        string className = input.ClassName is null || input.ClassName.Length == 0
            ? defaultName
            : input.ClassName;


        int lastDot = className.LastIndexOf('.');
        string? namespaceName = lastDot < 0 ? null : className.Substring(0, lastDot);
        string classIdentifier = lastDot < 0 ? className : className.Substring(lastDot + 1);

        if (!CSharpIdentifier.IsValidIdentifier(classIdentifier)
            || !CSharpIdentifier.IsValidNamespace(namespaceName))
        {
            options = null;
            return false;
        }

        string hintName = BuildHintName(className);
        options = new(
            input,
            resourceName,
            GetResourceFamily(input, assemblyName),
            className,
            namespaceName,
            classIdentifier,
            hintName);

        return true;
    }

    /// <summary>
    ///  Gets the neutral resource family for an input.
    /// </summary>
    /// <param name="input">The resource input.</param>
    /// <param name="assemblyName">The consuming assembly name.</param>
    /// <returns>The neutral resource family.</returns>
    internal static string GetResourceFamily(ResourceInput input, string? assemblyName)
    {
        if (input.CliResourceFamily is not null && input.CliResourceFamily.Length > 0)
        {
            return input.CliResourceFamily;
        }

        string family = GetResourceName(input, assemblyName);

        if (string.IsNullOrEmpty(input.Culture))
        {
            return family;
        }

        string suffix = "." + input.Culture;
        return family.EndsWith(suffix, StringComparison.Ordinal)
            ? family.Substring(0, family.Length - suffix.Length)
            : family;
    }

    private static string GetResourceName(ResourceInput input, string? assemblyName)
    {
        if (input.ManifestResourceName is not null && input.ManifestResourceName.Length > 0)
        {
            return input.ManifestResourceName;
        }

        return GetDefaultResourceAndClassName(input, assemblyName);
    }

    private static string GetDefaultResourceAndClassName(ResourceInput input, string? assemblyName)
    {
        string sourcePath = input.Link is null || input.Link.Length == 0
            ? input.Path
            : input.Link;

        string hintName = GetFileNameWithoutExtension(sourcePath);
        string relativeDir = GetRelativeDirectory(input);
        string relativeName = CollapseDots(relativeDir.Replace('\\', '.').Replace('/', '.') + hintName);
        string? rootNamespace = string.IsNullOrEmpty(input.RootNamespace)
            ? assemblyName
            : input.RootNamespace;

        return string.IsNullOrEmpty(rootNamespace)
            ? relativeName
            : rootNamespace + "." + relativeName;
    }

    private static string GetRelativeDirectory(ResourceInput input)
    {
        if (input.Link is not null && input.Link.Length > 0)
        {
            return GetDirectoryName(input.Link);
        }

        if (input.RelativeDir is not null
            && input.RelativeDir.Length > 0
            && !input.RelativeDir.StartsWith("..", StringComparison.Ordinal)
            && !HasRootMarker(input.RelativeDir))
        {
            return input.RelativeDir;
        }

        return string.Empty;
    }

    private static bool HasRootMarker(string path)
    {
        char firstCharacter = path[0];
        if (firstCharacter is '/' or '\\')
        {
            return true;
        }

        return path.Length > 1
            && path[1] == ':'
            && (firstCharacter is >= 'A' and <= 'Z' or >= 'a' and <= 'z');
    }

    private static string GetDirectoryName(string path)
    {
        int separator = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
        return separator < 0 ? string.Empty : path.Substring(0, separator + 1);
    }

    private static string GetFileNameWithoutExtension(string path)
    {
        int separator = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
        string fileName = separator < 0 ? path : path.Substring(separator + 1);
        int extension = fileName.LastIndexOf('.');
        return extension < 0 ? fileName : fileName.Substring(0, extension);
    }

    private static string CollapseDots(string value)
    {
        while (value.IndexOf("..", StringComparison.Ordinal) >= 0)
        {
            value = value.Replace("..", ".");
        }

        return value.Trim('.');
    }

    private static string BuildHintName(string className)
    {
        using ValueStringBuilder builder = new(stackalloc char[256]);
        foreach (char character in className)
        {
            builder.Append(CSharpIdentifier.IsIdentifierPartCharacter(character) || character == '.'
                ? character
                : '_');
        }

        builder.Append(".Designer.cs");
        return builder.ToString();
    }
}