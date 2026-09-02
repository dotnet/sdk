// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.NET.TestFramework;

// NOTE: Operating-system gating is intentionally NOT reimplemented here. MSTest ships
// [OSCondition(OperatingSystems.Windows | OperatingSystems.OSX | ...)] (with
// ConditionMode.Include/Exclude), which already covers it. Because MSTest combines
// condition attributes that have different GroupNames with AND, an OS requirement can be
// composed with the conditions below, for example:
//   [TestMethod, OSCondition(OperatingSystems.Windows), CoreMSBuildOnly]            // Core MSBuild on Windows
//   [TestMethod, OSCondition(OperatingSystems.Windows), RequiresMSBuildVersion("17.0")]
//   [TestMethod, OSCondition(OperatingSystems.OSX)]                                  // macOS only
//   [TestMethod, OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]          // everything except macOS
// Only conditions that MSTest does not already provide (MSBuild flavor/version, shared
// framework availability, and process architecture) are defined here.

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class CoreMSBuildOnlyAttribute : ConditionBaseAttribute
{
    public CoreMSBuildOnlyAttribute()
        : base(ConditionMode.Include)
    {
        IgnoreMessage = "This test requires Core MSBuild to run";
    }

    public override string GroupName => nameof(CoreMSBuildOnlyAttribute);

    public override bool IsConditionMet => !SdkTestContext.Current.ToolsetUnderTest.ShouldUseFullFrameworkMSBuild;
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class FullMSBuildOnlyAttribute : ConditionBaseAttribute
{
    public FullMSBuildOnlyAttribute()
        : base(ConditionMode.Include)
    {
        IgnoreMessage = "This test requires Full MSBuild to run";
    }

    public override string GroupName => nameof(FullMSBuildOnlyAttribute);

    public override bool IsConditionMet => SdkTestContext.Current.ToolsetUnderTest.ShouldUseFullFrameworkMSBuild;
}

/// <summary>
/// Restricts a test to the specified process architecture(s). Compose with
/// <c>[OSCondition(...)]</c> for OS gating. Use <see cref="ConditionMode.Exclude"/> to skip
/// the listed architectures instead of requiring them.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class ArchitectureConditionAttribute : ConditionBaseAttribute
{
    private readonly Architecture[] _architectures;

    public ArchitectureConditionAttribute(params Architecture[] architectures)
        : this(ConditionMode.Include, architectures)
    {
    }

    public ArchitectureConditionAttribute(ConditionMode mode, params Architecture[] architectures)
        : base(mode)
    {
        _architectures = architectures;
        IgnoreMessage = mode == ConditionMode.Include
            ? $"This test is only supported on architecture(s): {string.Join(", ", architectures)}"
            : $"This test is not supported on architecture(s): {string.Join(", ", architectures)}";
    }

    public override string GroupName => nameof(ArchitectureConditionAttribute);

    public override bool IsConditionMet => Array.IndexOf(_architectures, RuntimeInformation.ProcessArchitecture) >= 0;
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class RequiresMSBuildVersionAttribute : ConditionBaseAttribute
{
    private readonly string _version;

    public RequiresMSBuildVersionAttribute(string version)
        : base(ConditionMode.Include)
    {
        _version = version;
        IgnoreMessage = $"This test requires MSBuild version {version} to run";
    }

    public string? Reason { get; set; }

    public override string GroupName => nameof(RequiresMSBuildVersionAttribute);

    public override bool IsConditionMet => IsRequiredMSBuildVersionAvailable(_version);

    private bool IsRequiredMSBuildVersionAvailable(string version)
    {
        if (!Version.TryParse(SdkTestContext.Current.ToolsetUnderTest.MSBuildVersion, out Version? msbuildVersion))
        {
            IgnoreMessage = $"Failed to determine the version of MSBuild ({SdkTestContext.Current.ToolsetUnderTest.MSBuildVersion}).";
            return false;
        }
        if (!Version.TryParse(version, out Version? requiredVersion))
        {
            IgnoreMessage = $"Failed to determine the version required by this test ({version}).";
            return false;
        }
        if (requiredVersion > msbuildVersion)
        {
            IgnoreMessage = $"This test requires MSBuild version {version} to run (using {SdkTestContext.Current.ToolsetUnderTest.MSBuildVersion}).";
            return false;
        }

        return true;
    }
}

#if NETCOREAPP

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class RequiresSpecificFrameworkAttribute : ConditionBaseAttribute
{
    private readonly string _framework;

    public RequiresSpecificFrameworkAttribute(string framework)
        : base(ConditionMode.Include)
    {
        _framework = framework;
        IgnoreMessage = $"This test requires a shared framework that isn't present: {framework}";
    }

    public override string GroupName => nameof(RequiresSpecificFrameworkAttribute);

    public override bool IsConditionMet => EnvironmentInfo.SupportsTargetFramework(_framework);
}

#endif
