// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Tools.Bootstrapper;

internal static class DnupUtilities
{
    public static string ExeSuffix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";

    public static string GetDotnetExeName()
    {
        return "dotnet" + ExeSuffix;
    }

    public static bool PathsEqual(string? a, string? b)
    {
        if (a == null && b == null)
        {
            return true;
        }
        else if (a == null || b == null)
        {
            return false;
        }

        return string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                                Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                                StringComparison.OrdinalIgnoreCase);
    }

    public static InstallArchitecture GetInstallArchitecture(System.Runtime.InteropServices.Architecture architecture)
    {
        return architecture switch
        {
            System.Runtime.InteropServices.Architecture.X86 => InstallArchitecture.x86,
            System.Runtime.InteropServices.Architecture.X64 => InstallArchitecture.x64,
            System.Runtime.InteropServices.Architecture.Arm64 => InstallArchitecture.arm64,
            _ => throw new NotSupportedException($"Architecture {architecture} is not supported.")
        };
    }

    public static void ForceReplaceFile(string sourcePath, string destPath)
    {
        File.Copy(sourcePath, destPath, overwrite: true);

        // Copy file attributes
        var srcInfo = new FileInfo(sourcePath);
        var dstInfo = new FileInfo(destPath)
        {
            CreationTimeUtc = srcInfo.CreationTimeUtc,
            LastWriteTimeUtc = srcInfo.LastWriteTimeUtc,
            LastAccessTimeUtc = srcInfo.LastAccessTimeUtc,
            Attributes = srcInfo.Attributes
        };
    }

    public static string GetRuntimeIdentifier(InstallArchitecture architecture)
    {
        var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" :
                 RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" :
                 RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" : "unknown";

        var arch = architecture switch
        {
            InstallArchitecture.x64 => "x64",
            InstallArchitecture.x86 => "x86",
            InstallArchitecture.arm64 => "arm64",
            _ => "x64" // Default fallback
        };

        return $"{os}-{arch}";
    }

    public static string GetFileExtensionForPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ".zip"; // Windows typically uses zip archives
        }
        else
        {
            return ".tar.gz"; // Unix-like systems use tar.gz
        }
    }
}
