// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
<<<<<<< HEAD
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;
=======
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
>>>>>>> 5565e6b21b7a11560fb88e73dce4c097fac6260d

public static class AssertionHelper
{
    public static string[] AppendApphostOnNonMacOS(string ProjectName, string[] expectedFiles)
    {
        string apphost = $"{ProjectName}{Constants.ExeSuffix}";
        // No UseApphost is false by default on macOS
<<<<<<< HEAD
        return !RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
=======
        return RuntimeEnvironment.OperatingSystemPlatform != Platform.Darwin
>>>>>>> 5565e6b21b7a11560fb88e73dce4c097fac6260d
            ? expectedFiles.Append(apphost).ToArray()
            : expectedFiles;
    }
}
