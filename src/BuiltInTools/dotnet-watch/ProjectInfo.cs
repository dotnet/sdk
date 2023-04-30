// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;

namespace Microsoft.DotNet.Watcher
{
    internal sealed record ProjectInfo
    (
        string ProjectPath,
        bool IsNetCoreApp,
        Version? TargetFrameworkVersion,
        string RuntimeIdentifier,
        string DefaultAppHostRuntimeIdentifier,
        string RunCommand,
        string RunArguments,
        string RunWorkingDirectory
    )
    {
        private static readonly Version Version3_1 = new Version(3, 1);
        private static readonly Version Version6_0 = new Version(6, 0);

        public bool IsNetCoreApp31OrNewer()
        {
            return IsNetCoreApp && TargetFrameworkVersion is not null && TargetFrameworkVersion >= Version3_1;
        }

        public bool IsNetCoreApp60OrNewer()
        {
            return IsNetCoreApp && TargetFrameworkVersion is not null && TargetFrameworkVersion >= Version6_0;
        }
    }
}
