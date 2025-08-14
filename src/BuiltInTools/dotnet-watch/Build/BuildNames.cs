﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch;

internal static class PropertyNames
{
    public const string TargetFramework = nameof(TargetFramework);
    public const string TargetFrameworkIdentifier = nameof(TargetFrameworkIdentifier);
    public const string TargetPath = nameof(TargetPath);
    public const string EnableDefaultItems = nameof(EnableDefaultItems);
    public const string TargetFrameworks = nameof(TargetFrameworks);
    public const string WebAssemblyHotReloadCapabilities = nameof(WebAssemblyHotReloadCapabilities);
    public const string TargetFrameworkVersion = nameof(TargetFrameworkVersion);
    public const string TargetName = nameof(TargetName);
    public const string IntermediateOutputPath = nameof(IntermediateOutputPath);
    public const string HotReloadAutoRestart = nameof(HotReloadAutoRestart);
    public const string DefaultItemExcludes = nameof(DefaultItemExcludes);
    public const string CustomCollectWatchItems = nameof(CustomCollectWatchItems);
    public const string UsingMicrosoftNETSdkRazor = nameof(UsingMicrosoftNETSdkRazor);
    public const string DotNetWatchContentFiles = nameof(DotNetWatchContentFiles);
    public const string DotNetWatchBuild = nameof(DotNetWatchBuild);
    public const string DesignTimeBuild = nameof(DesignTimeBuild);
    public const string SkipCompilerExecution = nameof(SkipCompilerExecution);
    public const string ProvideCommandLineArgs = nameof(ProvideCommandLineArgs);
}

internal static class ItemNames
{
    public const string Watch = nameof(Watch);
    public const string AdditionalFiles = nameof(AdditionalFiles);
    public const string Compile = nameof(Compile);
    public const string Content = nameof(Content);
    public const string ProjectCapability = nameof(ProjectCapability);
}

internal static class MetadataNames
{
    public const string Watch = nameof(Watch);
}

internal static class TargetNames
{
    public const string Compile = nameof(Compile);
    public const string Restore = nameof(Restore);
    public const string GenerateComputedBuildStaticWebAssets = nameof(GenerateComputedBuildStaticWebAssets);
}
