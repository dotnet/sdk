// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.DotNet.Cli.Commands.Workload.List;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.VisualStudio.Setup.Configuration;

namespace Microsoft.DotNet.Cli.Workload.List.Tests;

public class VisualStudioWorkloadsTests
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
#pragma warning disable CA1416 // Validate platform compatibility
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

        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, setupConfiguration: setupConfiguration);

        List<KeyValuePair<string, string>> expected =
        [
            new("maui", "VS 18.4.11412.20"),
            new("maui-desktop", "VS 18.4.11412.20"),
            new("android", "VS 18.4.11412.20"),
        ];

        installedWorkloads.AsEnumerable().Should().BeEquivalentTo(expected);

#pragma warning restore CA1416 // Validate platform compatibility
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_NoVSInstances_ReturnsEmpty()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
#pragma warning disable CA1416 // Validate platform compatibility
        InstalledWorkloadsCollection installedWorkloads = new();

        SetupConfigurationMock setupConfiguration = new(
            new EnumSetupInstancesMock([]));

        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, setupConfiguration: setupConfiguration);

        installedWorkloads.AsEnumerable().Should().BeEmpty();
#pragma warning restore CA1416 // Validate platform compatibility
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_SetupConfigurationNotRegistered_ReturnsEmpty()
    {
        // This test verifies that COMException with REGDB_E_CLASSNOTREG is handled gracefully
        // when Visual Studio setup configuration COM classes are not registered (e.g., no VS installed)
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
#pragma warning disable CA1416 // Validate platform compatibility
        InstalledWorkloadsCollection installedWorkloads = new();

        SetupConfigurationThrowingMock setupConfiguration = new(
            new COMException("Class not registered", unchecked((int)0x80040154)));

        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, setupConfiguration: setupConfiguration);

        installedWorkloads.AsEnumerable().Should().BeEmpty();
#pragma warning restore CA1416 // Validate platform compatibility
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_OtherCOMException_Throws()
    {
        // Other COM exceptions should not be swallowed
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
#pragma warning disable CA1416 // Validate platform compatibility
        InstalledWorkloadsCollection installedWorkloads = new();

        SetupConfigurationThrowingMock setupConfiguration = new(
            new COMException("Some other COM error", unchecked((int)0x80004005)));

        Action act = () => VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, setupConfiguration: setupConfiguration);

        act.Should().Throw<COMException>().Where(e => e.ErrorCode == unchecked((int)0x80004005));
#pragma warning restore CA1416 // Validate platform compatibility
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_NoSdkPackageInstalled_ReturnsEmpty()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
#pragma warning disable CA1416 // Validate platform compatibility
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

        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, setupConfiguration: setupConfiguration);

        installedWorkloads.AsEnumerable().Should().BeEmpty();
#pragma warning restore CA1416 // Validate platform compatibility
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_EmptyPackageId_SkipsPackage()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
#pragma warning disable CA1416 // Validate platform compatibility
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

        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, setupConfiguration: setupConfiguration);

        List<KeyValuePair<string, string>> expected =
        [
            new("maui", "VS 18.4.11412.20"),
        ];

        installedWorkloads.AsEnumerable().Should().BeEquivalentTo(expected);
#pragma warning restore CA1416 // Validate platform compatibility
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_InvalidSdkVersion_SkipsInstance()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
#pragma warning disable CA1416 // Validate platform compatibility
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

        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, setupConfiguration: setupConfiguration);

        installedWorkloads.AsEnumerable().Should().BeEmpty();
#pragma warning restore CA1416 // Validate platform compatibility
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_FeatureBandMismatch_SkipsInstance()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
#pragma warning disable CA1416 // Validate platform compatibility
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

        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, sdkFeatureBand: requestedFeatureBand, setupConfiguration: setupConfiguration);

        installedWorkloads.AsEnumerable().Should().BeEmpty();
#pragma warning restore CA1416 // Validate platform compatibility
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_FeatureBandMatch_ReturnsWorkloads()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
#pragma warning disable CA1416 // Validate platform compatibility
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

        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, sdkFeatureBand: requestedFeatureBand, setupConfiguration: setupConfiguration);

        List<KeyValuePair<string, string>> expected =
        [
            new("maui", "VS 18.4.11412.20"),
        ];

        installedWorkloads.AsEnumerable().Should().BeEquivalentTo(expected);
#pragma warning restore CA1416 // Validate platform compatibility
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_NullFeatureBand_ReturnsAllWorkloads()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
#pragma warning disable CA1416 // Validate platform compatibility
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
        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, sdkFeatureBand: null, setupConfiguration: setupConfiguration);

        List<KeyValuePair<string, string>> expected =
        [
            new("maui", "VS 18.4.11412.20"),
        ];

        installedWorkloads.AsEnumerable().Should().BeEquivalentTo(expected);
#pragma warning restore CA1416 // Validate platform compatibility
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_MultipleVSInstances_ReturnsWorkloadsFromAll()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
#pragma warning disable CA1416 // Validate platform compatibility
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

        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, sdkFeatureBand: requestedFeatureBand, setupConfiguration: setupConfiguration);

        List<KeyValuePair<string, string>> expected =
        [
            new("maui", "VS 18.4.11412.20"),
            new("android", "VS 17.8.34567.10"),
            new("ios", "VS 17.8.34567.10"),
        ];

        installedWorkloads.AsEnumerable().Should().BeEquivalentTo(expected);
#pragma warning restore CA1416 // Validate platform compatibility
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_MultipleVSInstances_OnlyMatchingFeatureBand()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
#pragma warning disable CA1416 // Validate platform compatibility
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

        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, sdkFeatureBand: requestedFeatureBand, setupConfiguration: setupConfiguration);

        List<KeyValuePair<string, string>> expected =
        [
            new("maui", "VS 18.4.11412.20"),
        ];

        installedWorkloads.AsEnumerable().Should().BeEquivalentTo(expected);
#pragma warning restore CA1416 // Validate platform compatibility
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_NewStyleComponentId_MatchesWorkload()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
#pragma warning disable CA1416 // Validate platform compatibility
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

        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, setupConfiguration: setupConfiguration);

        List<KeyValuePair<string, string>> expected =
        [
            new("maui", "VS 18.4.11412.20"),
            new("android", "VS 18.4.11412.20"),
        ];

        installedWorkloads.AsEnumerable().Should().BeEquivalentTo(expected);
#pragma warning restore CA1416 // Validate platform compatibility
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_UnknownPackageId_Ignored()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
#pragma warning disable CA1416 // Validate platform compatibility
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

        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, setupConfiguration: setupConfiguration);

        List<KeyValuePair<string, string>> expected =
        [
            new("maui", "VS 18.4.11412.20"),
        ];

        installedWorkloads.AsEnumerable().Should().BeEquivalentTo(expected);
#pragma warning restore CA1416 // Validate platform compatibility
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_DuplicateWorkloadInInstance_AddedOnce()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
#pragma warning disable CA1416 // Validate platform compatibility
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

        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, setupConfiguration: setupConfiguration);

        // Should only contain one entry for maui since HashSet is used internally
        List<KeyValuePair<string, string>> expected =
        [
            new("maui", "VS 18.4.11412.20"),
        ];

        installedWorkloads.AsEnumerable().Should().BeEquivalentTo(expected);
#pragma warning restore CA1416 // Validate platform compatibility
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_AllSupportedVSProducts_ReturnsWorkloads()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
#pragma warning disable CA1416 // Validate platform compatibility
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

        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, sdkFeatureBand: requestedFeatureBand, setupConfiguration: setupConfiguration);

        List<KeyValuePair<string, string>> expected =
        [
            new("maui", "VS 17.1.0.0"),
            new("android", "VS 17.2.0.0"),
            new("ios", "VS 17.3.0.0"),
        ];

        installedWorkloads.AsEnumerable().Should().BeEquivalentTo(expected);
#pragma warning restore CA1416 // Validate platform compatibility
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_SdkBeforeWorkloads_StillMatches()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
#pragma warning disable CA1416 // Validate platform compatibility
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

        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, setupConfiguration: setupConfiguration);

        List<KeyValuePair<string, string>> expected =
        [
            new("maui", "VS 18.4.11412.20"),
            new("android", "VS 18.4.11412.20"),
        ];

        installedWorkloads.AsEnumerable().Should().BeEquivalentTo(expected);
#pragma warning restore CA1416 // Validate platform compatibility
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_NoWorkloadPackages_ReturnsEmpty()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
#pragma warning disable CA1416 // Validate platform compatibility
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

        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, setupConfiguration: setupConfiguration);

        installedWorkloads.AsEnumerable().Should().BeEmpty();
#pragma warning restore CA1416 // Validate platform compatibility
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_EmptyPackageList_ReturnsEmpty()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
#pragma warning disable CA1416 // Validate platform compatibility
        InstalledWorkloadsCollection installedWorkloads = new();

        SetupConfigurationMock setupConfiguration = new(
            new EnumSetupInstancesMock(
                [
                    new SetupInstanceMock(
                        "18.4.11412.20",
                        new SetupPackageReferenceMock("Microsoft.VisualStudio.Product.Professional"),
                        []),
                ]));

        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, setupConfiguration: setupConfiguration);

        installedWorkloads.AsEnumerable().Should().BeEmpty();
#pragma warning restore CA1416 // Validate platform compatibility
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_WorkloadWithDashesMatchesDotNotation()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
#pragma warning disable CA1416 // Validate platform compatibility
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

        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, setupConfiguration: setupConfiguration);

        List<KeyValuePair<string, string>> expected =
        [
            new("maui-desktop", "VS 18.4.11412.20"),
            new("maui-mobile", "VS 18.4.11412.20"),
            new("wasm-tools", "VS 18.4.11412.20"),
        ];

        installedWorkloads.AsEnumerable().Should().BeEquivalentTo(expected);
#pragma warning restore CA1416 // Validate platform compatibility
    }

    [WindowsOnlyFact]
    public void GetInstalledWorkloads_MultipleSdkVersions_UsesFirstMatchingFeatureBand()
    {
        MockWorkloadResolver workloadResolver = new(s_workloadInfo);
#pragma warning disable CA1416 // Validate platform compatibility
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

        VisualStudioWorkloads.GetInstalledWorkloads(workloadResolver, installedWorkloads, sdkFeatureBand: requestedFeatureBand, setupConfiguration: setupConfiguration);

        // Should be empty because SDK feature band doesn't match
        installedWorkloads.AsEnumerable().Should().BeEmpty();
#pragma warning restore CA1416 // Validate platform compatibility
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

    public class SetupInstanceMock : ISetupInstance2
    {
        private readonly string _installationVersion;
        private readonly ISetupPackageReference _product;
        private readonly ISetupPackageReference[] _packages;

        public SetupInstanceMock(string installationVersion, ISetupPackageReference product, ISetupPackageReference[] packages)
        {
            _installationVersion = installationVersion;
            _product = product;
            _packages = packages;
        }

        public string GetInstallationVersion() => _installationVersion;
        public ISetupPackageReference GetProduct() => _product;
        public ISetupPackageReference[] GetPackages() => _packages;


        public string GetInstanceId() => throw new NotImplementedException();
        public FILETIME GetInstallDate() => throw new NotImplementedException();
        public string GetInstallationName() => throw new NotImplementedException();
        public string GetInstallationPath() => throw new NotImplementedException();
        public string GetDisplayName(int lcid = 0) => throw new NotImplementedException();
        public string GetDescription(int lcid = 0) => throw new NotImplementedException();
        public string ResolvePath(string? pwszRelativePath = null) => throw new NotImplementedException();
        public InstanceState GetState() => throw new NotImplementedException();
        public string GetProductPath() => throw new NotImplementedException();
        public ISetupErrorState GetErrors() => throw new NotImplementedException();
        public bool IsLaunchable() => throw new NotImplementedException();
        public bool IsComplete() => throw new NotImplementedException();
        public ISetupPropertyStore GetProperties() => throw new NotImplementedException();
        public string GetEnginePath() => throw new NotImplementedException();
    }

    public class SetupPackageReferenceMock : ISetupPackageReference
    {
        private readonly string _id;

        public SetupPackageReferenceMock(string id)
        {
            _id = id;
        }

        // Used while getting VS version info and packages.
        // Packages starting with "Microsoft.NetCore.Toolset." are trimmed and included. All others are matched against
        // the workload resolver's available workloads.
        public string GetId() => _id;

        public string GetVersion() => throw new NotImplementedException();
        public string GetChip() => throw new NotImplementedException();
        public string GetLanguage() => throw new NotImplementedException();
        public string GetBranch() => throw new NotImplementedException();
        string ISetupPackageReference.GetType() => throw new NotImplementedException();
        public string GetUniqueId() => throw new NotImplementedException();
        public bool GetIsExtension() => throw new NotImplementedException();
    }

    public class EnumSetupInstancesMock : IEnumSetupInstances
    {
        private int _index;
        private readonly ISetupInstance[] _instances;

        public EnumSetupInstancesMock(ISetupInstance[] instances)
        {
            _instances = instances;
        }

        // Next
        // GetInstallationVersion()
        // GetProduct().GetId()
        public void Next(int celt, ISetupInstance[] rgelt, out int pceltFetched)
        {
            if (celt != 1
                || _index >= _instances.Length
                || rgelt.Length == 0)
            {
                pceltFetched = 0;
                return;
            }

            rgelt[0] = _instances[_index++];
            pceltFetched = 1;
        }

        public void Skip(int celt) => throw new NotImplementedException();
        public void Reset() => throw new NotImplementedException();
        public IEnumSetupInstances Clone() => throw new NotImplementedException();
    }

    public class SetupConfigurationMock : ISetupConfiguration2
    {
        private readonly IEnumSetupInstances _enumSetupInstances;
        public SetupConfigurationMock(IEnumSetupInstances enumSetupInstances)
        {
            _enumSetupInstances = enumSetupInstances;
        }

        public IEnumSetupInstances EnumInstances() => _enumSetupInstances;

        public IEnumSetupInstances EnumInstancesThatSupportComponents(string pwszComponentId) => throw new NotImplementedException();
        public ISetupInstance GetInstanceForCurrentProcess() => throw new NotImplementedException();
        public ISetupInstance GetInstanceForPath(string path) => throw new NotImplementedException();
        public IEnumSetupInstances EnumAllInstances() => throw new NotImplementedException();
    }

    public class SetupConfigurationThrowingMock : ISetupConfiguration2
    {
        private readonly COMException _exception;
        public SetupConfigurationThrowingMock(COMException exception)
        {
            _exception = exception;
        }

        public IEnumSetupInstances EnumInstances() => throw _exception;

        public IEnumSetupInstances EnumInstancesThatSupportComponents(string pwszComponentId) => throw new NotImplementedException();
        public ISetupInstance GetInstanceForCurrentProcess() => throw new NotImplementedException();
        public ISetupInstance GetInstanceForPath(string path) => throw new NotImplementedException();
        public IEnumSetupInstances EnumAllInstances() => throw new NotImplementedException();
    }
}
