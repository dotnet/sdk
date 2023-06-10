// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Handles mapping between .NET RIDs and Docker platform names/structures
/// </summary>
public static class PlatformMapping {

    public static bool TryGetRidForDockerPlatform(string golangPlatform, bool isMuslBased, [NotNullWhen(true)] out string? runtimeIdentifier) {
        runtimeIdentifier = null;

        runtimeIdentifier = golangPlatform.Split('/') switch {
            ["linux", "amd64"] when isMuslBased => "linux-musl-x64",
            ["linux", "amd64"] => "linux-x64",

            ["linux", "amd64", var _amd64Version] when isMuslBased => "linux-musl-x64",
            ["linux", "amd64", var _amd64Version] => "linux-x64",

            ["linux", "arm64"] when isMuslBased => "linux-musl-arm64",
            ["linux", "arm64"] => "linux-arm64",

            ["linux", "arm64", var _arm64Version ] when isMuslBased => "linux-musl-arm64",
            ["linux", "arm64", var _arm64Version ] => "linux-arm64",

            ["linux", "arm" ] or ["linux", "arm", "v7" ] when isMuslBased => "linux-musl-arm",
            ["linux", "arm" ] or ["linux", "arm", "v7" ] => "linux-arm",

            ["linux", "arm", "v6" ] when isMuslBased => "linux-musl-armv6",
            ["linux", "arm", "v6" ]   => "linux-armv6",

            ["linux", "riscv64" ] when isMuslBased => "linux-musl-riscv64",
            ["linux", "riscv64" ] => "linux-riscv64",

            ["linux", "ppc64le" ] when isMuslBased => "linux-musl-ppc64le",
            ["linux", "ppc64le" ] => "linux-ppc64le",

            ["linux", "s390x" ] when isMuslBased => "linux-musl-s390x",
            ["linux", "s390x" ] => "linux-s390x",

            ["linux", "386" ] when isMuslBased => "linux-musl-x86",
            ["linux", "386" ] => "linux-x86",

            ["windows", "amd64"] => "win-x64",
            ["windows", "arm64"] => "win-arm64",

            _ => null // other golang platforms are not supported
        };
        return runtimeIdentifier != null;
    }

    public static bool TryGetDockerPlatformForRid(string runtimeIdentifier, [NotNullWhen(true)] out string? dockerPlatform) {
        dockerPlatform = null;

        runtimeIdentifier = runtimeIdentifier.Replace("-musl-", "-"); // we lose musl information in the docker platform name

        dockerPlatform = runtimeIdentifier.Split('-') switch {
            ["linux", "x64"] => "linux/amd64",
            ["linux", "arm64"] => "linux/arm64",
            ["linux", "arm"] => "linux/arm/v7",
            ["linux", "armv6"] => "linux/arm/v6",
            ["linux", "riscv64"] => "linux/riscv64",
            ["linux", "ppc64le"] => "linux/ppc64le",
            ["linux", "s390x"] => "linux/s390x",
            ["linux", "x86"] => "linux/386",
            ["win", "x64"] => "windows/amd64",
            ["win", "arm64"] => "windows/arm64",
            _ => null
        };
        return dockerPlatform != null;
    }

    public static bool TryGetDockerImageTagForRid(string runtimeIdentifier, [NotNullWhen(true)] out string? dockerPlatformTag) {
        dockerPlatformTag = null;

        runtimeIdentifier = runtimeIdentifier.Replace("-musl-", "-"); // we lose musl information in the docker platform name

        dockerPlatformTag = runtimeIdentifier.Split('-') switch {
            ["linux", "x64"] => "amd64",
            ["linux", "arm64"] => "arm64v8",
            ["linux", "arm"] => "arm32v7",
            ["linux", "armv6"] => "arm32v6",
            ["linux", "riscv64"] => "riscv64",
            ["linux", "ppc64le"] => "ppc64le",
            ["linux", "s390x"] => "s390x",
            ["linux", "x86"] => "386",
            _ => null // deliberately not trying to make tag names for windows containers
        };
        return dockerPlatformTag != null;
    }
}
