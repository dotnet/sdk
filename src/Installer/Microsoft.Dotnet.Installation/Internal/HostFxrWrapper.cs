// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.NativeWrapper;

namespace Microsoft.Dotnet.Installation.Internal;

internal partial class HostFxrWrapper
{
    public static NetEnvironmentInfo getInfo(string installRoot)
    {
        if (!Directory.Exists(installRoot))
        {
            return new NetEnvironmentInfo();
        }
        PreloadHostFxrLibrary(installRoot);

        var info = new NetEnvironmentInfo();
        StatusCode status = Interop.hostfxr_get_dotnet_environment_info(installRoot, default, info.Initialize, default);
        if (status != StatusCode.Success)
        {
            throw new InvalidOperationException($"hostfxr_get_dotnet_environment_info failed with status 0x{(uint)status:X8}.");
        }

        return info;
    }

    public static IEnumerable<DotnetInstall> getInstalls(string installRoot)
    {
        if (!Directory.Exists(installRoot))
        {
            return Enumerable.Empty<DotnetInstall>();
        }
        NetEnvironmentInfo environmentInfo;
        try
        {
            environmentInfo = getInfo(installRoot);
        }
        catch (HostFxrEnvironmentInfoUnavailableException) when (HasInstallationLayout(installRoot))
        {
            return [];
        }
        var installs = new List<DotnetInstall>();
        foreach (var sdk in environmentInfo.SdkInfo.ToList())
        {
            installs.Add(new DotnetInstall(
                new DotnetInstallRoot(installRoot, InstallerUtilities.GetDefaultInstallArchitecture()),
                sdk.Version,
                InstallComponent.SDK));
        }

        foreach (var runtime in environmentInfo.RuntimeInfo.ToList())
        {
            var component = InstallComponentExtensions.FromFrameworkName(runtime.Name) ?? InstallComponent.Runtime;
            installs.Add(new DotnetInstall(
                new DotnetInstallRoot(installRoot, InstallerUtilities.GetDefaultInstallArchitecture()),
                runtime.Version,
                component));
        }

        return installs;
    }

    // lpFileName passed to LoadLibraryEx must be a full path.
    private const int LOAD_WITH_ALTERED_SEARCH_PATH = 0x8;

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    private static partial IntPtr LoadLibraryExW(string lpFileName, IntPtr hFile, int dwFlags);

    private static void PreloadHostFxrLibrary(string dotnetExeDirectory)
    {
        string hostFxrPath = FindHostFxrLibrary(dotnetExeDirectory)
            ?? throw new DllNotFoundException($"No hostfxr library was found under '{Path.Combine(dotnetExeDirectory, "host", "fxr")}'.");
        IntPtr handle;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            handle = LoadLibraryExW(hostFxrPath, IntPtr.Zero, LOAD_WITH_ALTERED_SEARCH_PATH);
            if (handle == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), $"Could not load hostfxr from '{hostFxrPath}'.");
            }
        }
        else
        {
            AppContext.SetData("HOSTFXR_PATH", hostFxrPath);
            handle = NativeLibrary.Load(hostFxrPath);
        }

        if (!NativeLibrary.TryGetExport(handle, "hostfxr_get_dotnet_environment_info", out _))
        {
            NativeLibrary.Free(handle);
            throw new HostFxrEnvironmentInfoUnavailableException();
        }
    }

    private static string? FindHostFxrLibrary(string installRoot)
    {
        string hostFxrDirectory = Path.Combine(installRoot, "host", "fxr");
        if (!Directory.Exists(hostFxrDirectory))
        {
            return null;
        }

        string libraryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "hostfxr.dll"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? "libhostfxr.dylib"
                : "libhostfxr.so";

        return Directory.EnumerateDirectories(hostFxrDirectory)
            .Where(directory => ReleaseVersion.TryParse(Path.GetFileName(directory), out _))
            .Select(directory => Path.Combine(directory, libraryName))
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static bool HasInstallationLayout(string installRoot)
    {
        if (!File.Exists(Path.Combine(installRoot, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet")))
        {
            return false;
        }

        string[] componentDirectories =
        [
            Path.Combine(installRoot, "sdk"),
            Path.Combine(installRoot, "shared", InstallComponentExtensions.RuntimeFrameworkName),
            Path.Combine(installRoot, "shared", InstallComponentExtensions.AspNetCoreFrameworkName),
            Path.Combine(installRoot, "shared", InstallComponentExtensions.WindowsDesktopFrameworkName),
        ];
        return componentDirectories.Where(Directory.Exists).Any(directory =>
            Directory.EnumerateDirectories(directory).Any(versionDirectory =>
                ReleaseVersion.TryParse(Path.GetFileName(versionDirectory), out _) &&
                Directory.EnumerateFileSystemEntries(versionDirectory).Any()));
    }
}

internal sealed class HostFxrEnvironmentInfoUnavailableException()
    : Exception("The loaded hostfxr does not export hostfxr_get_dotnet_environment_info.");
