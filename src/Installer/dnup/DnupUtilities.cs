// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Tools.Bootstrapper;

internal static class DnupUtilities
{
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
}
