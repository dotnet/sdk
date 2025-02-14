// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;

namespace Microsoft.DotNet.UnifiedBuild.Tasks.ManifestAssets
{
    public class JoinVerticalsConfig
    {
        public IReadOnlyList<string> PriorityVerticals { get; set; } = [];

        public static JoinVerticalsConfig GetDefaultConfig()
        {
            JoinVerticalsConfig joinVerticalsConfig = new JoinVerticalsConfig
            {
                PriorityVerticals = [
                    "Windows_x64_BuildPass2",
                    "Windows_x64",
                    "OSX_arm64",
                    "AzureLinux_x64_Cross_x64",
                    "AzureLinux_x64_Cross_arm64",
                    "Android_Shortstack_x64",
                    "Browser_Shortstack_wasm",
                    "iOS_Shortstack_arm64"
                ]
            };
            return joinVerticalsConfig;
        }
    }
}
