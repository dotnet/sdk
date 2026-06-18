// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.NET.TestFramework;

[Flags]
public enum TestPlatforms
{
    Windows = 1,
    Linux = 2,
    OSX = 4,
    FreeBSD = 8,
    Any = Windows | Linux | OSX | FreeBSD,
}

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

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class CoreMSBuildAndWindowsOnlyAttribute : ConditionBaseAttribute
{
    public CoreMSBuildAndWindowsOnlyAttribute()
        : base(ConditionMode.Include)
    {
        IgnoreMessage = "This test requires Core MSBuild and Windows to run";
    }

    public override string GroupName => nameof(CoreMSBuildAndWindowsOnlyAttribute);

    public override bool IsConditionMet =>
        !SdkTestContext.Current.ToolsetUnderTest.ShouldUseFullFrameworkMSBuild
            && RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class MacOsOnlyAttribute : ConditionBaseAttribute
{
    public MacOsOnlyAttribute()
        : base(ConditionMode.Include)
    {
        IgnoreMessage = "This test requires macos to run";
    }

    public override string GroupName => nameof(MacOsOnlyAttribute);

    public override bool IsConditionMet => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class PlatformSpecificAttribute : ConditionBaseAttribute
{
    private const Architecture NoArchitectureFilter = (Architecture)(-1);

    private readonly TestPlatforms _platforms;
    private readonly TestPlatforms _skipPlatforms;
    private readonly Architecture _architecture;
    private readonly Architecture _skipArchitecture;
    private readonly string? _skipReason;

    public PlatformSpecificAttribute(
        TestPlatforms platforms = TestPlatforms.Any,
        TestPlatforms skipPlatforms = 0,
        Architecture architecture = NoArchitectureFilter,
        Architecture skipArchitecture = NoArchitectureFilter,
        string? skipReason = null)
        : base(ConditionMode.Include)
    {
        _platforms = platforms;
        _skipPlatforms = skipPlatforms;
        _architecture = architecture;
        _skipArchitecture = skipArchitecture;
        _skipReason = skipReason;
        IgnoreMessage = "This test is not supported on this platform.";
    }

    public override string GroupName => nameof(PlatformSpecificAttribute);

    public override bool IsConditionMet
    {
        get
        {
            if (EvaluateSkip(_platforms, _skipPlatforms, _architecture, _skipArchitecture, _skipReason) is { } skip)
            {
                IgnoreMessage = skip;
                return false;
            }

            return true;
        }
    }

    private static string? EvaluateSkip(
        TestPlatforms platforms,
        TestPlatforms skipPlatforms,
        Architecture architecture,
        Architecture skipArchitecture,
        string? skipReason)
    {
        if (!PlatformsMatchCurrentOS(platforms))
        {
            return "This test is not supported on this platform.";
        }

        if (architecture != NoArchitectureFilter && RuntimeInformation.ProcessArchitecture != architecture)
        {
            return $"This test is not supported on {RuntimeInformation.ProcessArchitecture} architecture.";
        }

        bool skipPlatformMatches = skipPlatforms != 0 && PlatformsMatchCurrentOS(skipPlatforms);
        bool skipArchMatches = skipArchitecture != NoArchitectureFilter && RuntimeInformation.ProcessArchitecture == skipArchitecture;

        if (skipPlatforms != 0 && skipArchitecture != NoArchitectureFilter)
        {
            if (skipPlatformMatches && skipArchMatches)
            {
                return skipReason ?? "Test skipped on this platform and architecture.";
            }
        }
        else if (skipPlatformMatches)
        {
            return skipReason ?? "Test skipped on this platform.";
        }
        else if (skipArchMatches)
        {
            return skipReason ?? "Test skipped on this architecture.";
        }

        return null;
    }

    private static bool PlatformsMatchCurrentOS(TestPlatforms platforms) =>
        (platforms.HasFlag(TestPlatforms.Windows) && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            || (platforms.HasFlag(TestPlatforms.Linux) && RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            || (platforms.HasFlag(TestPlatforms.OSX) && RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            || (platforms.HasFlag(TestPlatforms.FreeBSD) && RuntimeInformation.IsOSPlatform(OSPlatform.Create("FREEBSD")));
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

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class WindowsOnlyRequiresMSBuildVersionAttribute : ConditionBaseAttribute
{
    private readonly string _version;

    public WindowsOnlyRequiresMSBuildVersionAttribute(string version)
        : base(ConditionMode.Include)
    {
        _version = version;
        IgnoreMessage = "This test requires Windows to run";
    }

    public string? Reason { get; set; }

    public override string GroupName => nameof(WindowsOnlyRequiresMSBuildVersionAttribute);

    public override bool IsConditionMet
    {
        get
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                IgnoreMessage = "This test requires Windows to run";
                return false;
            }

            return IsRequiredMSBuildVersionAvailable(_version);
        }
    }

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
