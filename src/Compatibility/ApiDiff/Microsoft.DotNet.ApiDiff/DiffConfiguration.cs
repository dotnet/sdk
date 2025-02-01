// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff;

/// <summary>
/// Defines a variety of configuration options for API diff.
/// </summary>
public class DiffConfiguration
{
    public required bool AddPartialModifier { get; init; }
    public required string AfterAssembliesFolderPath { get; init; }
    public string? AfterAssemblyReferencesFolderPath { get; init; }
    public required string[] AttributesToExclude { get; init; }
    public required string BeforeAssembliesFolderPath { get; init; }
    public string? BeforeAssemblyReferencesFolderPath { get; init; }
    public required bool Debug { get; init; }
    public required bool HideImplicitDefaultConstructors { get; init; }
    public required bool IncludeTableOfContents { get; init; }
    public required string OutputFolderPath { get; init; }
}
