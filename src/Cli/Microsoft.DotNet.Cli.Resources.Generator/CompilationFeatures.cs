// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.DotNet.Cli.Resources.Generator;

/// <summary>
///  Describes language and framework features available to generated source.
/// </summary>
internal sealed class CompilationFeatures
{
    private CompilationFeatures(
        string? assemblyName,
        bool supportsNullable,
        bool hasAggressiveInlining,
        bool hasNotNullIfNotNull)
    {
        AssemblyName = assemblyName;
        SupportsNullable = supportsNullable;
        HasAggressiveInlining = hasAggressiveInlining;
        HasNotNullIfNotNull = hasNotNullIfNotNull;
    }

    /// <summary>
    ///  Gets the name of the consuming assembly.
    /// </summary>
    internal string? AssemblyName { get; }

    /// <summary>
    ///  Gets a value indicating whether the compilation supports nullable annotations.
    /// </summary>
    internal bool SupportsNullable { get; }

    /// <summary>
    ///  Gets a value indicating whether <c>MethodImplOptions.AggressiveInlining</c> is available.
    /// </summary>
    internal bool HasAggressiveInlining { get; }

    /// <summary>
    ///  Gets a value indicating whether <c>NotNullIfNotNullAttribute</c> is available.
    /// </summary>
    internal bool HasNotNullIfNotNull { get; }

    /// <summary>
    ///  Creates the feature set for a compilation.
    /// </summary>
    /// <param name="compilation">The consuming compilation.</param>
    /// <returns>The detected feature set.</returns>
    internal static CompilationFeatures Create(Compilation compilation)
    {
        bool supportsNullable = compilation is CSharpCompilation csharpCompilation
            && csharpCompilation.LanguageVersion >= LanguageVersion.CSharp8;

        INamedTypeSymbol? methodImplOptions = compilation.GetTypeByMetadataName(
            "System.Runtime.CompilerServices.MethodImplOptions");

        bool hasAggressiveInlining = methodImplOptions?.GetMembers("AggressiveInlining").Length > 0;
        bool hasNotNullIfNotNull = compilation.GetTypeByMetadataName(
            "System.Diagnostics.CodeAnalysis.NotNullIfNotNullAttribute") is not null;

        return new(
            compilation.AssemblyName,
            supportsNullable,
            hasAggressiveInlining,
            hasNotNullIfNotNull);
    }
}