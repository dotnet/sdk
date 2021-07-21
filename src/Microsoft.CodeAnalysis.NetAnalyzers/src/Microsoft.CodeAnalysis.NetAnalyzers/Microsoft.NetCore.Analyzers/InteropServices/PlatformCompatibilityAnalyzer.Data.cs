// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Analyzer.Utilities;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    public sealed partial class PlatformCompatibilityAnalyzer
    {
        /// <summary>
        /// Class used for keeping platform information of an API, all properties are optional.
        ///
        /// We need to keep only 2 values for [SupportedOSPlatform] attribute, first one will be the lowest version found, mostly for assembly level
        /// attribute which denotes when the API first introduced, second one would keep new APIs added later and requires higher platform version
        /// (if there is multiple version found in the API parents chain we will keep only highest version)
        ///
        /// Same for [UnsupportedOSPlatform] attribute, an API could be unsupported at first and then start supported from some version then eventually removed.
        /// So we only keep at most 2 versions of [UnsupportedOSPlatform] first one will be the lowest version found, second one will be second lowest if there is any
        ///
        /// Properties:
        ///  - SupportedFirst - keeps lowest version of [SupportedOSPlatform] attribute found
        ///  - SupportedSecond - keeps the highest version of [SupportedOSPlatform] attribute if there is any
        ///  - UnsupportedFirst - keeps the lowest version of [UnsupportedOSPlatform] attribute found
        ///  - UnsupportedSecond - keeps the second lowest version of [UnsupportedOSPlatform] attribute found
        /// </summary>
        private class Versions
        {
            public Version? SupportedFirst { get; set; }
            public Version? SupportedSecond { get; set; }
            public Version? UnsupportedFirst { get; set; }
            public Version? UnsupportedSecond { get; set; }
            public bool IsSet() => SupportedFirst != null || UnsupportedFirst != null ||
                        SupportedSecond != null || UnsupportedSecond != null;
        }

        private sealed class PlatformAttributes
        {
            public PlatformAttributes() { }

            public PlatformAttributes(Callsite callsite, SmallDictionary<string, Versions> platforms)
            {
                Callsite = callsite;
                Platforms = platforms;
            }

            public SmallDictionary<string, Versions>? Platforms { get; set; }
            public Callsite Callsite { get; set; }
        }
    }
}
