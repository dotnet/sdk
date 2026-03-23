// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Workload.List;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.VisualStudio.Setup.Configuration;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.System.Variant;

#pragma warning disable CA1416 // Validate platform compatibility

namespace Microsoft.DotNet.Cli.Workload.List.Tests;

public unsafe class VisualStudioWorkloadsTests
{
    private static readonly WorkloadResolver.WorkloadInfo[] s_workloadInfo =
    [
        new(new("android"), ".NET SDK Workload for building Android applications."),
        new(new("ios"), ".NET SDK Workload for building iOS applications."),
        new(new("maccatalyst"), ".NET SDK Workload for building MacCatalyst applications."),
        new(new("macos"), ".NET SDK Workload for building macOS applications."),
        new(new("maui"), ".NET MAUI SDK for all platforms"),
        new(new("maui-mobile"), ".NET MAUI SDK for Mobile"),
        new(new("maui-desktop"), ".NET MAUI SDK for Desktop"),
        new(new("maui-android"), ".NET MAUI SDK for Android"),
        new(new("maui-maccatalyst"), ".NET MAUI SDK for Mac Catalyst"),
        new(new("maui-ios"), ".NET MAUI SDK for iOS"),
        new(new("maui-windows"), ".NET MAUI SDK for Windows"),
        new(new("maui-tizen"), ".NET MAUI SDK for Tizen"),
        new(new("tvos"), ".NET SDK Workload for building tvOS applications."),
        new(new("wasm-tools"), ".NET WebAssembly build tools"),
        new(new("wasm-experimental"), ".NET WebAssembly experimental tooling"),
        new(new("wasi-experimental"), "workloads/wasi-experimental/description"),
        new(new("mobile-librarybuilder"), "workloads/mobile-librarybuilder/description"),
        new(new("wasm-tools-net6"), ".NET WebAssembly build tools for net6.0"),
        new(new("wasm-tools-net7"), ".NET WebAssembly build tools for net7.0"),
        new(new("wasm-experimental-net7"), ".NET WebAssembly experimental tooling for net7.0"),
        new(new("wasm-tools-net8"), ".NET WebAssembly build tools for net8.0"),
        new(new("wasm-experimental-net8"), ".NET WebAssembly experimental tooling for net8.0"),
        new(new("wasi-experimental-net8"), "workloads/wasi-experimental-net8/description"),
        new(new("wasm-tools-net9"), ".NET WebAssembly build tools for .NET 9.0"),
        new(new("wasm-experimental-net9"), ".NET WebAssembly experimental tooling for .NET 9.0"),
        new(new("wasi-experimental-net9"), "workloads/wasi-experimental-net9/description"),
        new(new("mobile-librarybuilder-net9"), "workloads/mobile-librarybuilder-net9/description"),
    ];

    // Workload Ids contains WorkloadInfo key names to key names with an additional key name
    // lookup prefaced with "Microsoft.NET.Component.". Key names have dashes replaced with periods.


    [WindowsOnlyFact]
    public void GetInstalledWorkloads_Basic()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
        InstalledWorkloadsCollection installedWorkloads = new();

        SetupConfigurationMock setupConfiguration = new(
            new EnumSetupInstancesMock(
                [
                    new SetupInstanceMock(
                        "18.4.11412.20",
                        // Has to be one of the three known Visual Studio products
                        new SetupPackageReferenceMock("Microsoft.VisualStudio.Product.Professional"),
                        [
                            new SetupPackageReferenceMock("maui"),
                            new SetupPackageReferenceMock("maui.desktop"),
                            new SetupPackageReferenceMock("android"),
                            // Need some .NET toolset package installed to include any matches
                            new SetupPackageReferenceMock("Microsoft.NetCore.Toolset.10.0.101"),
                        ]),
                ]));

        using var setupConfigurationScope = setupConfiguration.AsComScope();

        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, setupConfiguration: setupConfigurationScope);

        List<KeyValuePair<string, string>> expected =
        [
            new("maui", "VS 18.4.11412.20"),
            new("maui-desktop", "VS 18.4.11412.20"),
            new("android", "VS 18.4.11412.20"),
        ];

        installedWorkloads.AsEnumerable().Should().BeEquivalentTo(expected);

    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_NoVSInstances_ReturnsEmpty()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
        InstalledWorkloadsCollection installedWorkloads = new();

        SetupConfigurationMock setupConfiguration = new(
            new EnumSetupInstancesMock([]));

        using var setupConfigurationScope = setupConfiguration.AsComScope();

        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, setupConfiguration: setupConfigurationScope);

        installedWorkloads.AsEnumerable().Should().BeEmpty();
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_NoSdkPackageInstalled_ReturnsEmpty()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
        InstalledWorkloadsCollection installedWorkloads = new();

        // VS instance has workload packages but no SDK toolset package
        SetupConfigurationMock setupConfiguration = new(
            new EnumSetupInstancesMock(
                [
                    new SetupInstanceMock(
                        "18.4.11412.20",
                        new SetupPackageReferenceMock("Microsoft.VisualStudio.Product.Professional"),
                        [
                            new SetupPackageReferenceMock("maui"),
                            new SetupPackageReferenceMock("android"),
                        ]),
                ]));

        using var setupConfigurationScope = setupConfiguration.AsComScope();

        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, setupConfiguration: setupConfigurationScope);

        installedWorkloads.AsEnumerable().Should().BeEmpty();
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_EmptyPackageId_SkipsPackage()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
        InstalledWorkloadsCollection installedWorkloads = new();

        SetupConfigurationMock setupConfiguration = new(
            new EnumSetupInstancesMock(
                [
                    new SetupInstanceMock(
                        "18.4.11412.20",
                        new SetupPackageReferenceMock("Microsoft.VisualStudio.Product.Professional"),
                        [
                            new SetupPackageReferenceMock(""),
                            new SetupPackageReferenceMock("   "),
                            new SetupPackageReferenceMock("maui"),
                            new SetupPackageReferenceMock("Microsoft.NetCore.Toolset.10.0.101"),
                        ]),
                ]));

        using var setupConfigurationScope = setupConfiguration.AsComScope();
        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, setupConfiguration: setupConfigurationScope);

        List<KeyValuePair<string, string>> expected =
        [
            new("maui", "VS 18.4.11412.20"),
        ];

        installedWorkloads.AsEnumerable().Should().BeEquivalentTo(expected);
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_InvalidSdkVersion_SkipsInstance()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
        InstalledWorkloadsCollection installedWorkloads = new();

        SetupConfigurationMock setupConfiguration = new(
            new EnumSetupInstancesMock(
                [
                    new SetupInstanceMock(
                        "18.4.11412.20",
                        new SetupPackageReferenceMock("Microsoft.VisualStudio.Product.Professional"),
                        [
                            new SetupPackageReferenceMock("maui"),
                            // Invalid version format - can't be parsed
                            new SetupPackageReferenceMock("Microsoft.NetCore.Toolset.invalid-version"),
                        ]),
                ]));

        using var setupConfigurationScope = setupConfiguration.AsComScope();
        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, setupConfiguration: setupConfigurationScope);

        installedWorkloads.AsEnumerable().Should().BeEmpty();
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_FeatureBandMismatch_SkipsInstance()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
        InstalledWorkloadsCollection installedWorkloads = new();

        SetupConfigurationMock setupConfiguration = new(
            new EnumSetupInstancesMock(
                [
                    new SetupInstanceMock(
                        "18.4.11412.20",
                        new SetupPackageReferenceMock("Microsoft.VisualStudio.Product.Professional"),
                        [
                            new SetupPackageReferenceMock("maui"),
                            new SetupPackageReferenceMock("Microsoft.NetCore.Toolset.9.0.100"),
                        ]),
                ]));

        // Request workloads for a different feature band
        SdkFeatureBand requestedFeatureBand = new("10.0.100");

        using var setupConfigurationScope = setupConfiguration.AsComScope();
        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, sdkFeatureBand: requestedFeatureBand, setupConfiguration: setupConfigurationScope);

        installedWorkloads.AsEnumerable().Should().BeEmpty();
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_FeatureBandMatch_ReturnsWorkloads()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
        InstalledWorkloadsCollection installedWorkloads = new();

        SetupConfigurationMock setupConfiguration = new(
            new EnumSetupInstancesMock(
                [
                    new SetupInstanceMock(
                        "18.4.11412.20",
                        new SetupPackageReferenceMock("Microsoft.VisualStudio.Product.Professional"),
                        [
                            new SetupPackageReferenceMock("maui"),
                            new SetupPackageReferenceMock("Microsoft.NetCore.Toolset.10.0.100"),
                        ]),
                ]));

        SdkFeatureBand requestedFeatureBand = new("10.0.100");

        using var setupConfigurationScope = setupConfiguration.AsComScope();
        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, sdkFeatureBand: requestedFeatureBand, setupConfiguration: setupConfigurationScope);

        List<KeyValuePair<string, string>> expected =
        [
            new("maui", "VS 18.4.11412.20"),
        ];

        installedWorkloads.AsEnumerable().Should().BeEquivalentTo(expected);
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_NullFeatureBand_ReturnsAllWorkloads()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
        InstalledWorkloadsCollection installedWorkloads = new();

        SetupConfigurationMock setupConfiguration = new(
            new EnumSetupInstancesMock(
                [
                    new SetupInstanceMock(
                        "18.4.11412.20",
                        new SetupPackageReferenceMock("Microsoft.VisualStudio.Product.Professional"),
                        [
                            new SetupPackageReferenceMock("maui"),
                            new SetupPackageReferenceMock("Microsoft.NetCore.Toolset.9.0.100"),
                        ]),
                ]));

        // When sdkFeatureBand is null, all feature bands should match
        using var setupConfigurationScope = setupConfiguration.AsComScope();
        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, sdkFeatureBand: null, setupConfiguration: setupConfigurationScope);

        List<KeyValuePair<string, string>> expected =
        [
            new("maui", "VS 18.4.11412.20"),
        ];

        installedWorkloads.AsEnumerable().Should().BeEquivalentTo(expected);
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_MultipleVSInstances_ReturnsWorkloadsFromAll()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
        InstalledWorkloadsCollection installedWorkloads = new();

        SetupConfigurationMock setupConfiguration = new(
            new EnumSetupInstancesMock(
                [
                    new SetupInstanceMock(
                        "18.4.11412.20",
                        new SetupPackageReferenceMock("Microsoft.VisualStudio.Product.Professional"),
                        [
                            new SetupPackageReferenceMock("maui"),
                            new SetupPackageReferenceMock("Microsoft.NetCore.Toolset.10.0.100"),
                        ]),
                    new SetupInstanceMock(
                        "17.8.34567.10",
                        new SetupPackageReferenceMock("Microsoft.VisualStudio.Product.Enterprise"),
                        [
                            new SetupPackageReferenceMock("android"),
                            new SetupPackageReferenceMock("ios"),
                            new SetupPackageReferenceMock("Microsoft.NetCore.Toolset.10.0.100"),
                        ]),
                ]));

        SdkFeatureBand requestedFeatureBand = new("10.0.100");

        using var setupConfigurationScope = setupConfiguration.AsComScope();
        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, sdkFeatureBand: requestedFeatureBand, setupConfiguration: setupConfigurationScope);

        List<KeyValuePair<string, string>> expected =
        [
            new("maui", "VS 18.4.11412.20"),
            new("android", "VS 17.8.34567.10"),
            new("ios", "VS 17.8.34567.10"),
        ];

        installedWorkloads.AsEnumerable().Should().BeEquivalentTo(expected);
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_MultipleVSInstances_OnlyMatchingFeatureBand()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
        InstalledWorkloadsCollection installedWorkloads = new();

        SetupConfigurationMock setupConfiguration = new(
            new EnumSetupInstancesMock(
                [
                    new SetupInstanceMock(
                        "18.4.11412.20",
                        new SetupPackageReferenceMock("Microsoft.VisualStudio.Product.Professional"),
                        [
                            new SetupPackageReferenceMock("maui"),
                            new SetupPackageReferenceMock("Microsoft.NetCore.Toolset.10.0.100"),
                        ]),
                    new SetupInstanceMock(
                        "17.8.34567.10",
                        new SetupPackageReferenceMock("Microsoft.VisualStudio.Product.Enterprise"),
                        [
                            new SetupPackageReferenceMock("android"),
                            // Different feature band - should be skipped
                            new SetupPackageReferenceMock("Microsoft.NetCore.Toolset.9.0.100"),
                        ]),
                ]));

        SdkFeatureBand requestedFeatureBand = new("10.0.100");

        using var setupConfigurationScope = setupConfiguration.AsComScope();
        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, sdkFeatureBand: requestedFeatureBand, setupConfiguration: setupConfigurationScope);

        List<KeyValuePair<string, string>> expected =
        [
            new("maui", "VS 18.4.11412.20"),
        ];

        installedWorkloads.AsEnumerable().Should().BeEquivalentTo(expected);
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_NewStyleComponentId_MatchesWorkload()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
        InstalledWorkloadsCollection installedWorkloads = new();

        // Test the new VS 17.12+ component ID format: Microsoft.NET.Component.<workloadId>
        SetupConfigurationMock setupConfiguration = new(
            new EnumSetupInstancesMock(
                [
                    new SetupInstanceMock(
                        "18.4.11412.20",
                        new SetupPackageReferenceMock("Microsoft.VisualStudio.Product.Professional"),
                        [
                            new SetupPackageReferenceMock("Microsoft.NET.Component.maui"),
                            new SetupPackageReferenceMock("Microsoft.NET.Component.android"),
                            new SetupPackageReferenceMock("Microsoft.NetCore.Toolset.10.0.100"),
                        ]),
                ]));

        using var setupConfigurationScope = setupConfiguration.AsComScope();
        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, setupConfiguration: setupConfigurationScope);

        List<KeyValuePair<string, string>> expected =
        [
            new("maui", "VS 18.4.11412.20"),
            new("android", "VS 18.4.11412.20"),
        ];

        installedWorkloads.AsEnumerable().Should().BeEquivalentTo(expected);
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_UnknownPackageId_Ignored()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
        InstalledWorkloadsCollection installedWorkloads = new();

        SetupConfigurationMock setupConfiguration = new(
            new EnumSetupInstancesMock(
                [
                    new SetupInstanceMock(
                        "18.4.11412.20",
                        new SetupPackageReferenceMock("Microsoft.VisualStudio.Product.Professional"),
                        [
                            new SetupPackageReferenceMock("maui"),
                            new SetupPackageReferenceMock("unknown.workload.id"),
                            new SetupPackageReferenceMock("another.unknown"),
                            new SetupPackageReferenceMock("Microsoft.NetCore.Toolset.10.0.100"),
                        ]),
                ]));

        using var setupConfigurationScope = setupConfiguration.AsComScope();
        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, setupConfiguration: setupConfigurationScope);

        List<KeyValuePair<string, string>> expected =
        [
            new("maui", "VS 18.4.11412.20"),
        ];

        installedWorkloads.AsEnumerable().Should().BeEquivalentTo(expected);
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_DuplicateWorkloadInInstance_AddedOnce()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
        InstalledWorkloadsCollection installedWorkloads = new();

        // Same workload referenced via both old and new style component IDs
        SetupConfigurationMock setupConfiguration = new(
            new EnumSetupInstancesMock(
                [
                    new SetupInstanceMock(
                        "18.4.11412.20",
                        new SetupPackageReferenceMock("Microsoft.VisualStudio.Product.Professional"),
                        [
                            new SetupPackageReferenceMock("maui"),
                            new SetupPackageReferenceMock("Microsoft.NET.Component.maui"),
                            new SetupPackageReferenceMock("Microsoft.NetCore.Toolset.10.0.100"),
                        ]),
                ]));

        using var setupConfigurationScope = setupConfiguration.AsComScope();
        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, setupConfiguration: setupConfigurationScope);

        // Should only contain one entry for maui since HashSet is used internally
        List<KeyValuePair<string, string>> expected =
        [
            new("maui", "VS 18.4.11412.20"),
        ];

        installedWorkloads.AsEnumerable().Should().BeEquivalentTo(expected);
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_AllSupportedVSProducts_ReturnsWorkloads()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
        InstalledWorkloadsCollection installedWorkloads = new();

        SetupConfigurationMock setupConfiguration = new(
            new EnumSetupInstancesMock(
                [
                    new SetupInstanceMock(
                        "17.1.0.0",
                        new SetupPackageReferenceMock("Microsoft.VisualStudio.Product.Community"),
                        [
                            new SetupPackageReferenceMock("maui"),
                            new SetupPackageReferenceMock("Microsoft.NetCore.Toolset.10.0.100"),
                        ]),
                    new SetupInstanceMock(
                        "17.2.0.0",
                        new SetupPackageReferenceMock("Microsoft.VisualStudio.Product.Professional"),
                        [
                            new SetupPackageReferenceMock("android"),
                            new SetupPackageReferenceMock("Microsoft.NetCore.Toolset.10.0.100"),
                        ]),
                    new SetupInstanceMock(
                        "17.3.0.0",
                        new SetupPackageReferenceMock("Microsoft.VisualStudio.Product.Enterprise"),
                        [
                            new SetupPackageReferenceMock("ios"),
                            new SetupPackageReferenceMock("Microsoft.NetCore.Toolset.10.0.100"),
                        ]),
                ]));

        SdkFeatureBand requestedFeatureBand = new("10.0.100");

        using var setupConfigurationScope = setupConfiguration.AsComScope();
        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, sdkFeatureBand: requestedFeatureBand, setupConfiguration: setupConfigurationScope);

        List<KeyValuePair<string, string>> expected =
        [
            new("maui", "VS 17.1.0.0"),
            new("android", "VS 17.2.0.0"),
            new("ios", "VS 17.3.0.0"),
        ];

        installedWorkloads.AsEnumerable().Should().BeEquivalentTo(expected);
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_SdkBeforeWorkloads_StillMatches()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
        InstalledWorkloadsCollection installedWorkloads = new();

        // SDK toolset package appears before workload packages
        SetupConfigurationMock setupConfiguration = new(
            new EnumSetupInstancesMock(
                [
                    new SetupInstanceMock(
                        "18.4.11412.20",
                        new SetupPackageReferenceMock("Microsoft.VisualStudio.Product.Professional"),
                        [
                            new SetupPackageReferenceMock("Microsoft.NetCore.Toolset.10.0.100"),
                            new SetupPackageReferenceMock("maui"),
                            new SetupPackageReferenceMock("android"),
                        ]),
                ]));

        using var setupConfigurationScope = setupConfiguration.AsComScope();
        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, setupConfiguration: setupConfigurationScope);

        List<KeyValuePair<string, string>> expected =
        [
            new("maui", "VS 18.4.11412.20"),
            new("android", "VS 18.4.11412.20"),
        ];

        installedWorkloads.AsEnumerable().Should().BeEquivalentTo(expected);
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_NoWorkloadPackages_ReturnsEmpty()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
        InstalledWorkloadsCollection installedWorkloads = new();

        // VS instance has SDK but no workload packages
        SetupConfigurationMock setupConfiguration = new(
            new EnumSetupInstancesMock(
                [
                    new SetupInstanceMock(
                        "18.4.11412.20",
                        new SetupPackageReferenceMock("Microsoft.VisualStudio.Product.Professional"),
                        [
                            new SetupPackageReferenceMock("Microsoft.NetCore.Toolset.10.0.100"),
                            new SetupPackageReferenceMock("Some.Other.Package"),
                        ]),
                ]));

        using var setupConfigurationScope = setupConfiguration.AsComScope();
        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, setupConfiguration: setupConfigurationScope);

        installedWorkloads.AsEnumerable().Should().BeEmpty();
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_EmptyPackageList_ReturnsEmpty()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
        InstalledWorkloadsCollection installedWorkloads = new();

        SetupConfigurationMock setupConfiguration = new(
            new EnumSetupInstancesMock(
                [
                    new SetupInstanceMock(
                        "18.4.11412.20",
                        new SetupPackageReferenceMock("Microsoft.VisualStudio.Product.Professional"),
                        []),
                ]));

        using var setupConfigurationScope = setupConfiguration.AsComScope();
        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, setupConfiguration: setupConfigurationScope);

        installedWorkloads.AsEnumerable().Should().BeEmpty();
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_WorkloadWithDashesMatchesDotNotation()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
        InstalledWorkloadsCollection installedWorkloads = new();

        // VS uses dots instead of dashes: maui-desktop -> maui.desktop
        SetupConfigurationMock setupConfiguration = new(
            new EnumSetupInstancesMock(
                [
                    new SetupInstanceMock(
                        "18.4.11412.20",
                        new SetupPackageReferenceMock("Microsoft.VisualStudio.Product.Professional"),
                        [
                            new SetupPackageReferenceMock("maui.desktop"),
                            new SetupPackageReferenceMock("maui.mobile"),
                            new SetupPackageReferenceMock("wasm.tools"),
                            new SetupPackageReferenceMock("Microsoft.NetCore.Toolset.10.0.100"),
                        ]),
                ]));

        using var setupConfigurationScope = setupConfiguration.AsComScope();
        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, setupConfiguration: setupConfigurationScope);

        List<KeyValuePair<string, string>> expected =
        [
            new("maui-desktop", "VS 18.4.11412.20"),
            new("maui-mobile", "VS 18.4.11412.20"),
            new("wasm-tools", "VS 18.4.11412.20"),
        ];

        installedWorkloads.AsEnumerable().Should().BeEquivalentTo(expected);
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_MultipleSdkVersions_UsesFirstMatchingFeatureBand()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
        InstalledWorkloadsCollection installedWorkloads = new();

        // Instance has multiple SDK packages - first matching one determines hasMatchingSdk
        SetupConfigurationMock setupConfiguration = new(
            new EnumSetupInstancesMock(
                [
                    new SetupInstanceMock(
                        "18.4.11412.20",
                        new SetupPackageReferenceMock("Microsoft.VisualStudio.Product.Professional"),
                        [
                            new SetupPackageReferenceMock("maui"),
                            // First SDK package doesn't match
                            new SetupPackageReferenceMock("Microsoft.NetCore.Toolset.9.0.100"),
                        ]),
                ]));

        SdkFeatureBand requestedFeatureBand = new("10.0.100");

        using var setupConfigurationScope = setupConfiguration.AsComScope();
        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, sdkFeatureBand: requestedFeatureBand, setupConfiguration: setupConfigurationScope);

        // Should be empty because SDK feature band doesn't match
        installedWorkloads.AsEnumerable().Should().BeEmpty();
    }

    private class MockWorkloadResolver : IWorkloadResolver
    {
        private readonly IEnumerable<WorkloadResolver.WorkloadInfo> _workloads;

        public MockWorkloadResolver(IEnumerable<WorkloadResolver.WorkloadInfo> workloads) => _workloads = workloads;

        public IEnumerable<WorkloadResolver.WorkloadInfo> GetAvailableWorkloads() => _workloads;

        public WorkloadResolver CreateOverlayResolver(IWorkloadManifestProvider overlayManifestProvider) => throw new NotImplementedException();

        public IEnumerable<WorkloadResolver.WorkloadInfo> GetExtendedWorkloads(IEnumerable<WorkloadId> workloadIds) => throw new NotImplementedException();
        public IEnumerable<WorkloadManifestInfo> GetInstalledManifests() => throw new NotImplementedException();
        public IEnumerable<WorkloadResolver.PackInfo> GetInstalledWorkloadPacksOfKind(WorkloadPackKind kind) => throw new NotImplementedException();
        public string GetManifestFeatureBand(string manifestId) => throw new NotImplementedException();
        public WorkloadManifest GetManifestFromWorkload(WorkloadId workloadId) => throw new NotImplementedException();
        public string GetManifestVersion(string manifestId) => throw new NotImplementedException();
        public IEnumerable<WorkloadPackId> GetPacksInWorkload(WorkloadId workloadId) => throw new NotImplementedException();
        public string GetSdkFeatureBand() => throw new NotImplementedException();
        public IEnumerable<WorkloadId> GetUpdatedWorkloads(WorkloadResolver advertisingManifestResolver, IEnumerable<WorkloadId> installedWorkloads) => throw new NotImplementedException();
        public IWorkloadManifestProvider GetWorkloadManifestProvider() => throw new NotImplementedException();
        public ISet<WorkloadResolver.WorkloadInfo>? GetWorkloadSuggestionForMissingPacks(IList<WorkloadPackId> packId, out ISet<WorkloadPackId> unsatisfiablePacks) => throw new NotImplementedException();
        public IWorkloadManifestProvider.WorkloadVersionInfo GetWorkloadVersion() => throw new NotImplementedException();
        public bool IsPlatformIncompatibleWorkload(WorkloadId workloadId) => throw new NotImplementedException();
        public void RefreshWorkloadManifests() => throw new NotImplementedException();
        public WorkloadResolver.PackInfo? TryGetPackInfo(WorkloadPackId packId) => throw new NotImplementedException();
    }

    internal unsafe class SetupInstanceMock : ISetupInstance2.Interface
    {
        private readonly string _installationVersion;
        private readonly ISetupPackageReference.Interface _product;
        private readonly ISetupPackageReference.Interface[] _packages;

        public SetupInstanceMock(
            string installationVersion,
            ISetupPackageReference.Interface product,
            ISetupPackageReference.Interface[] packages)
        {
            _installationVersion = installationVersion;
            _product = product;
            _packages = packages;
        }

        HRESULT ISetupInstance2.Interface.GetInstallationVersion(BSTR* pbstrInstallationVersion)
        {
            if (pbstrInstallationVersion is null)
            {
                return HRESULT.E_POINTER;
            }

            *pbstrInstallationVersion = new BSTR(_installationVersion);
            return HRESULT.S_OK;
        }

        HRESULT ISetupInstance2.Interface.GetProduct(ISetupPackageReference** ppPackage)
        {
            if (ppPackage is null)
            {
                return HRESULT.E_POINTER;
            }

            *ppPackage = (ISetupPackageReference*)Marshal.GetComInterfaceForObject(_product, typeof(ISetupPackageReference.Interface));
            return HRESULT.S_OK;
        }

        HRESULT ISetupInstance2.Interface.GetPackages(SAFEARRAY** ppsaPackages)
        {
            if (ppsaPackages is null)
            {
                return HRESULT.E_POINTER;
            }

            SAFEARRAY* psa = PInvoke.SafeArrayCreateVector(VARENUM.VT_UNKNOWN, 0, (uint)_packages.Length);
            if (psa is null)
            {
                return HRESULT.E_OUTOFMEMORY;
            }

            for (int i = 0; i < _packages.Length; i++)
            {
                nint pUnk = Marshal.GetComInterfaceForObject(_packages[i], typeof(ISetupPackageReference.Interface));
                int index = i;
                HRESULT hr = PInvoke.SafeArrayPutElement(psa, &index, (void*)pUnk);
                Marshal.Release(pUnk);  // PutElement AddRefs internally
                
                if (hr.Failed)
                {
                    PInvoke.SafeArrayDestroy(psa);
                    return hr;
                }
            }

            *ppsaPackages = psa;
            return HRESULT.S_OK;
        }

        HRESULT ISetupInstance2.Interface.GetInstanceId(BSTR* pbstrInstanceId) => throw new NotImplementedException();
        HRESULT ISetupInstance2.Interface.GetInstallDate(Windows.Win32.Foundation.FILETIME* pInstallDate) => throw new NotImplementedException();
        HRESULT ISetupInstance2.Interface.GetInstallationName(BSTR* pbstrInstallationName) => throw new NotImplementedException();
        HRESULT ISetupInstance2.Interface.GetInstallationPath(BSTR* pbstrInstallationPath) => throw new NotImplementedException();
        HRESULT ISetupInstance2.Interface.GetDisplayName(uint lcid, BSTR* pbstrDisplayName) => throw new NotImplementedException();
        HRESULT ISetupInstance2.Interface.GetDescription(uint lcid, BSTR* pbstrDescription) => throw new NotImplementedException();
        HRESULT ISetupInstance2.Interface.ResolvePath(PWSTR pwszRelativePath, BSTR* pbstrAbsolutePath) => throw new NotImplementedException();
        HRESULT ISetupInstance2.Interface.GetState(InstanceState* pState) => throw new NotImplementedException();
        HRESULT ISetupInstance2.Interface.GetProductPath(BSTR* pbstrProductPath) => throw new NotImplementedException();
        HRESULT ISetupInstance2.Interface.GetErrors(ISetupErrorState** ppErrorState) => throw new NotImplementedException();
        HRESULT ISetupInstance2.Interface.IsLaunchable(VARIANT_BOOL* pfLaunchable) => throw new NotImplementedException();
        HRESULT ISetupInstance2.Interface.IsComplete(VARIANT_BOOL* pfComplete) => throw new NotImplementedException();
        HRESULT ISetupInstance2.Interface.GetProperties(ISetupPropertyStore** ppPropertyStore) => throw new NotImplementedException();
        HRESULT ISetupInstance2.Interface.GetEnginePath(BSTR* pbstrEnginePath) => throw new NotImplementedException();
        HRESULT ISetupInstance.Interface.GetInstanceId(BSTR* pbstrInstanceId) => throw new NotImplementedException();
        HRESULT ISetupInstance.Interface.GetInstallDate(Windows.Win32.Foundation.FILETIME* pInstallDate) => throw new NotImplementedException();
        HRESULT ISetupInstance.Interface.GetInstallationName(BSTR* pbstrInstallationName) => throw new NotImplementedException();
        HRESULT ISetupInstance.Interface.GetInstallationPath(BSTR* pbstrInstallationPath) => throw new NotImplementedException();
        HRESULT ISetupInstance.Interface.GetInstallationVersion(BSTR* pbstrInstallationVersion) => throw new NotImplementedException();
        HRESULT ISetupInstance.Interface.GetDisplayName(uint lcid, BSTR* pbstrDisplayName) => throw new NotImplementedException();
        HRESULT ISetupInstance.Interface.GetDescription(uint lcid, BSTR* pbstrDescription) => throw new NotImplementedException();
        HRESULT ISetupInstance.Interface.ResolvePath(PWSTR pwszRelativePath, BSTR* pbstrAbsolutePath) => throw new NotImplementedException();
    }

    public unsafe class SetupPackageReferenceMock : ISetupPackageReference.Interface
    {
        private readonly string _id;

        public SetupPackageReferenceMock(string id) => _id = id;

        // Used while getting VS version info and packages.
        // Packages starting with "Microsoft.NetCore.Toolset." are trimmed and included. All others are matched against
        // the workload resolver's available workloads.
        public string GetId() => _id;

        HRESULT ISetupPackageReference.Interface.GetId(BSTR* pbstrId)
        {
            if (pbstrId is null)
            {
                return HRESULT.E_POINTER;
            }

            *pbstrId = new BSTR(_id);
            return HRESULT.S_OK;
        }

        HRESULT ISetupPackageReference.Interface.GetVersion(BSTR* pbstrVersion) => throw new NotImplementedException();
        HRESULT ISetupPackageReference.Interface.GetChip(BSTR* pbstrChip) => throw new NotImplementedException();
        HRESULT ISetupPackageReference.Interface.GetLanguage(BSTR* pbstrLanguage) => throw new NotImplementedException();
        HRESULT ISetupPackageReference.Interface.GetBranch(BSTR* pbstrBranch) => throw new NotImplementedException();
        HRESULT ISetupPackageReference.Interface.GetType(BSTR* pbstrType) => throw new NotImplementedException();
        HRESULT ISetupPackageReference.Interface.GetUniqueId(BSTR* pbstrUniqueId) => throw new NotImplementedException();
        HRESULT ISetupPackageReference.Interface.GetIsExtension(VARIANT_BOOL* pfIsExtension) => throw new NotImplementedException();
    }

    internal unsafe class EnumSetupInstancesMock : IEnumSetupInstances.Interface
    {
        private int _index;
        private readonly ISetupInstance.Interface[] _instances;

        public EnumSetupInstancesMock(ISetupInstance.Interface[] instances)
        {
            _instances = instances;
        }

        // Next
        // GetInstallationVersion()
        // GetProduct().GetId()
        HRESULT IEnumSetupInstances.Interface.Next(uint celt, ISetupInstance** rgelt, uint* pceltFetched)
        {
            if (rgelt is null)
            {
                return HRESULT.E_POINTER;
            }

            if (celt != 1)
            {
                *pceltFetched = 0;
                return HRESULT.E_INVALIDARG;
            }

            if (_index >= _instances.Length)
            {
                if (pceltFetched is not null)
                {
                    // pceltFetched can be null if celt is 1
                    *pceltFetched = 0;
                }

                return HRESULT.S_FALSE;
            }

            rgelt[0] = (ISetupInstance*)Marshal.GetComInterfaceForObject(_instances[_index++], typeof(ISetupInstance.Interface));
            if (pceltFetched is not null)
            {
                // pceltFetched can be null if celt is 1
                *pceltFetched = 1;
            }

            return HRESULT.S_OK;
        }

        HRESULT IEnumSetupInstances.Interface.Skip(uint celt) => throw new NotImplementedException();
        HRESULT IEnumSetupInstances.Interface.Reset() => throw new NotImplementedException();
        HRESULT IEnumSetupInstances.Interface.Clone(IEnumSetupInstances** ppEnumInstances) => throw new NotImplementedException();
    }

    internal class SetupConfigurationMock : ISetupConfiguration2.Interface
    {
        private readonly IEnumSetupInstances.Interface _enumSetupInstances;
        public SetupConfigurationMock(IEnumSetupInstances.Interface enumSetupInstances)
        {
            _enumSetupInstances = enumSetupInstances;
        }

        public ComScope<ISetupConfiguration2> AsComScope() =>
            new((ISetupConfiguration2*)Marshal.GetComInterfaceForObject(this, typeof(ISetupConfiguration2.Interface)));

        HRESULT ISetupConfiguration2.Interface.EnumInstances(IEnumSetupInstances** ppEnumInstances)
        {
            if (ppEnumInstances is null)
            {
                return HRESULT.E_POINTER;
            }

            *ppEnumInstances = (IEnumSetupInstances*)Marshal.GetComInterfaceForObject(_enumSetupInstances, typeof(IEnumSetupInstances.Interface));

            return HRESULT.S_OK;
        }

        HRESULT ISetupConfiguration2.Interface.GetInstanceForCurrentProcess(ISetupInstance** ppInstance) => throw new NotImplementedException();
        HRESULT ISetupConfiguration2.Interface.GetInstanceForPath(PCWSTR path, ISetupInstance** ppInstance) => throw new NotImplementedException();
        HRESULT ISetupConfiguration2.Interface.EnumAllInstances(IEnumSetupInstances** ppEnumInstances) => throw new NotImplementedException();
        HRESULT ISetupConfiguration.Interface.EnumInstances(IEnumSetupInstances** ppEnumInstances) => throw new NotImplementedException();
        HRESULT ISetupConfiguration.Interface.GetInstanceForCurrentProcess(ISetupInstance** ppInstance) => throw new NotImplementedException();
        HRESULT ISetupConfiguration.Interface.GetInstanceForPath(PCWSTR path, ISetupInstance** ppInstance) => throw new NotImplementedException();
    }
}

#pragma warning restore CA1416 // Validate platform compatibility
