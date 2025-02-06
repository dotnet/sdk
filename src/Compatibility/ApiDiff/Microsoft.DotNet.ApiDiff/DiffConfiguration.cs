// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff;

/// <summary>
/// Defines the necessary configuration options for API diff.
/// </summary>
public record DiffConfiguration(
    string AfterAssembliesFolderPath,
    string? AfterAssemblyReferencesFolderPath,
    string BeforeAssembliesFolderPath,
    string? BeforeAssemblyReferencesFolderPath,
    string OutputFolderPath,
    string TableOfContentsTitle,
    string[]? AttributesToExclude,
    bool AddPartialModifier,
    bool HideImplicitDefaultConstructors,
    bool Debug
);
