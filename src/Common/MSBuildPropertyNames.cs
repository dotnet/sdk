﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Cli
{
    static class MSBuildPropertyNames
    {
        public static readonly string PUBLISH_RELEASE = "PublishRelease";
        public static readonly string PACK_RELEASE = "PackRelease";
        public static readonly string CONFIGURATION = "Configuration";
        public static readonly string TARGET_FRAMEWORK = "TargetFramework";
        public static readonly string TARGET_FRAMEWORK_NUMERIC_VERSION = "TargetFrameworkVersion";
        public static readonly string TARGET_FRAMEWORKS = "TargetFrameworks";
        public static readonly string CONFIGURATION_RELEASE_VALUE = "Release";
        public static readonly string CONFIGURATION_DEBUG_VALUE = "Debug";
        public static readonly string OUTPUT_TYPE = "OutputType";
        public static readonly string OUTPUT_TYPE_EXECUTABLE = "Exe"; // Note that even on Unix when we don't produce exe this is still an exe, same for ASP
    }
}
