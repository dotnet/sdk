// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff;

/// <summary>
/// Defines the necessary configuration options for API diff.
/// </summary>
public record DiffConfiguration(
    bool AddPartialModifier,
    string AfterAssembliesFolderPath,
    string? AfterAssemblyReferencesFolderPath,
    string[]? AttributesToExclude,
    string BeforeAssembliesFolderPath,
    string? BeforeAssemblyReferencesFolderPath,
    bool Debug,
    bool HideImplicitDefaultConstructors,
    bool IncludeTableOfContents,
    string OutputFolderPath
);
