// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.DotNet.Cli.Resources.Generator;

/// <summary>
///  Identifies a resource independently of its source content for incremental generation.
/// </summary>
internal sealed class ResourceIdentity : IEquatable<ResourceIdentity>
{
    private ResourceIdentity(
        string path,
        string? culture,
        string resourceFamily,
        string? className,
        bool canGenerate)
    {
        Path = path;
        Culture = culture;
        ResourceFamily = resourceFamily;
        ClassName = className;
        CanGenerate = canGenerate;
    }

    /// <summary>
    ///  Gets the resource file path.
    /// </summary>
    internal string Path { get; }

    /// <summary>
    ///  Gets the resource culture.
    /// </summary>
    internal string? Culture { get; }

    /// <summary>
    ///  Gets the neutral resource family.
    /// </summary>
    internal string ResourceFamily { get; }

    /// <summary>
    ///  Gets the generated class name when one can be produced.
    /// </summary>
    internal string? ClassName { get; }

    /// <summary>
    ///  Gets a value indicating whether the resource can participate in generation.
    /// </summary>
    internal bool CanGenerate { get; }

    /// <summary>
    ///  Creates the stable identity for a resource input.
    /// </summary>
    /// <param name="input">The resource input.</param>
    /// <param name="assemblyName">The consuming assembly name.</param>
    /// <returns>The resource identity.</returns>
    internal static ResourceIdentity Create(ResourceInput input, string? assemblyName)
    {
        ResourceGenerationOptions? options = null;
        bool canGenerate = input.IsSelected
            && !input.HasUnsupportedOptions
            && ResourceGenerationOptions.TryCreate(
                input,
                assemblyName,
                out options)
            && options is not null;

        return new(
            input.Path,
            input.Culture,
            ResourceGenerationOptions.GetResourceFamily(input, assemblyName),
            options?.ClassName,
            canGenerate);
    }

            /// <inheritdoc/>
    public bool Equals(ResourceIdentity? other)
    {
        return ReferenceEquals(this, other)
            || (other is not null
                && Path == other.Path
                && Culture == other.Culture
                && ResourceFamily == other.ResourceFamily
                && ClassName == other.ClassName
                && CanGenerate == other.CanGenerate);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as ResourceIdentity);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = StringComparer.Ordinal.GetHashCode(Path);
            hash = (hash * 397) ^ (Culture is null ? 0 : StringComparer.Ordinal.GetHashCode(Culture));
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ResourceFamily);
            hash = (hash * 397) ^ (ClassName is null ? 0 : StringComparer.Ordinal.GetHashCode(ClassName));
            return (hash * 397) ^ CanGenerate.GetHashCode();
        }
    }
}