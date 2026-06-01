// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !CLI_AOT
using System.Collections.Frozen;
using System.Net.NetworkInformation;
#endif
using Microsoft.DotNet.Cli.Utils;
#if !CLI_AOT
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Utilities;
using RuntimeEnvironment = Microsoft.DotNet.Cli.Utils.RuntimeEnvironment;
#endif
using RuntimeInformation = System.Runtime.InteropServices.RuntimeInformation;

namespace Microsoft.DotNet.Cli.Telemetry;

internal class TelemetryCommonProperties(
#if !CLI_AOT
    Func<string>? getCurrentDirectory = null,
    Func<string, string>? hasher = null,
    Func<string?>? getMACAddress = null,
    Func<string>? getDeviceId = null,
    IDockerContainerDetector? dockerContainerDetector = null,
    IUserLevelCacheWriter? userLevelCacheWriter = null,
    ICIEnvironmentDetector? ciEnvironmentDetector = null,
    ILLMEnvironmentDetector? llmEnvironmentDetector = null
#endif
    )
{
#if !CLI_AOT
    private readonly IDockerContainerDetector _dockerContainerDetector = dockerContainerDetector ?? new DockerContainerDetectorForTelemetry();
    private readonly ICIEnvironmentDetector _ciEnvironmentDetector = ciEnvironmentDetector ?? new CIEnvironmentDetectorForTelemetry();
    private readonly ILLMEnvironmentDetector _llmEnvironmentDetector = llmEnvironmentDetector ?? new LLMEnvironmentDetectorForTelemetry();
    private readonly Func<string> _getCurrentDirectory = getCurrentDirectory ?? Directory.GetCurrentDirectory;
    private readonly Func<string, string> _hasher = hasher ?? Sha256Hasher.Hash;
    private readonly Func<string?> _getMACAddress = getMACAddress ?? MacAddressGetter.GetMacAddress;
    private readonly Func<string> _getDeviceId = getDeviceId ?? DeviceIdGetter.GetDeviceId;
    private readonly IUserLevelCacheWriter _userLevelCacheWriter = userLevelCacheWriter ?? new UserLevelCacheWriter();
#endif

    private const string OSVersion = "OS Version";
    private const string OSPlatform = "OS Platform";
    private const string OSArchitecture = "OS Architecture";
    private const string OutputRedirected = "Output Redirected";
    private const string RuntimeId = "Runtime Id";
    private const string ProductVersion = "Product Version";
    private const string TelemetryProfile = "Telemetry Profile";
#if !CLI_AOT
    private const string CurrentPathHash = "Current Path Hash";
    private const string DeviceId = "devdeviceid";
    private const string MachineId = "Machine ID";
    private const string MachineIdOld = "Machine ID Old";
    private const string DockerContainer = "Docker Container";
#endif
    private const string KernelVersion = "Kernel Version";
#if !CLI_AOT
    private const string InstallationType = "Installation Type";
    private const string ProductType = "Product Type";
    private const string LibcRelease = "Libc Release";
    private const string LibcVersion = "Libc Version";
#endif
    private const string SessionId = "SessionId";
#if !CLI_AOT
    private const string CI = "Continuous Integration";
    private const string LLM = "llm";
#endif
    private const string TelemetryProfileEnvironmentVariable = "DOTNET_CLI_TELEMETRY_PROFILE";
#if !CLI_AOT
    private const string MachineIdCacheKey = "MachineId";
    private const string IsDockerContainerCacheKey = "IsDockerContainer";
#endif

#if CLI_AOT
    public Dictionary<string, string?> GetTelemetryCommonProperties(string? currentSessionId) => new Dictionary<string, string?>
    {
        { OSVersion,        RuntimeInformation.OSDescription },
        { OSPlatform,       (OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsMacOS() ? "macOS" : "Linux") },
        { OSArchitecture,   RuntimeInformation.OSArchitecture.ToString() },
        { OutputRedirected, Console.IsOutputRedirected.ToString() },
        { RuntimeId,        RuntimeInformation.RuntimeIdentifier },
        { ProductVersion,   Product.Version },
        { TelemetryProfile, Environment.GetEnvironmentVariable(TelemetryProfileEnvironmentVariable) },
        { KernelVersion,    GetKernelVersion() },
        { SessionId,        currentSessionId }
    };
#else
    public FrozenDictionary<string, string?> GetTelemetryCommonProperties(string? currentSessionId) => new Dictionary<string, string?>
    {
        { OSVersion,        RuntimeEnvironment.OperatingSystemVersion },
        { OSPlatform,       RuntimeEnvironment.OperatingSystemPlatform.ToString() },
        { OSArchitecture,   RuntimeInformation.OSArchitecture.ToString() },
        { OutputRedirected, Console.IsOutputRedirected.ToString() },
        { RuntimeId,        RuntimeInformation.RuntimeIdentifier },
        { ProductVersion,   Product.Version },
        { TelemetryProfile, Environment.GetEnvironmentVariable(TelemetryProfileEnvironmentVariable) },
        { DockerContainer,  _userLevelCacheWriter.RunWithCache(IsDockerContainerCacheKey, () => _dockerContainerDetector.IsDockerContainer().ToString("G") ) },
        { CI,               _ciEnvironmentDetector.IsCIEnvironment().ToString() },
        { LLM,              _llmEnvironmentDetector.GetLLMEnvironment() },
        { CurrentPathHash,  _hasher(_getCurrentDirectory()) },
        { MachineIdOld,     _userLevelCacheWriter.RunWithCache(MachineIdCacheKey, GetMachineId) },
        // We don't want to recalcuate a new id for every new SDK version. Reuse the same path across versions.
        // If we change the format of the cache later, we need to rename the cache from v1 to v2.
        { MachineId,        _userLevelCacheWriter.RunWithCacheInFilePath(Path.Combine(CliFolderPathCalculator.DotnetUserProfileFolderPath, $"{MachineIdCacheKey}.v1.dotnetUserLevelCache"), GetMachineId) },
        { DeviceId,         _getDeviceId() },
        { KernelVersion,    GetKernelVersion() },
        { InstallationType, ExternalTelemetryProperties.GetInstallationType() },
        { ProductType,      ExternalTelemetryProperties.GetProductType() },
        { LibcRelease,      ExternalTelemetryProperties.GetLibcRelease() },
        { LibcVersion,      ExternalTelemetryProperties.GetLibcVersion() },
        { SessionId,        currentSessionId }
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private string GetMachineId()
    {
        try
        {
            return _getMACAddress() is { } macAddress ? _hasher(macAddress) : Guid.NewGuid().ToString();
        }
        catch (NetworkInformationException)
        {
            return Guid.NewGuid().ToString();
        }
    }
#endif

    /// <summary>
    /// Returns a string identifying the OS kernel.
    /// For Unix this currently comes from "uname -srv".
    /// For Windows this currently comes from RtlGetVersion().
    /// </summary>
    private static string GetKernelVersion() => RuntimeInformation.OSDescription;
}
