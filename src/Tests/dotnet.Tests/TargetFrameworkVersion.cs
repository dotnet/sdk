// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;

namespace dotnet.Tests
{
    internal static class TargetFrameworkVersion
    {
        public static Version NetCoreApp20 => new Version(2, 0);
        public static Version NetCoreApp21 => new Version(2, 1);
        public static Version NetCoreApp30 => new Version(3, 0);
        public static Version Net50 => new Version(5, 0);
        public static Version Net60 => new Version(6, 0);
    }
}
