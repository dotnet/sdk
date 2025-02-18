// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;

namespace Microsoft.DotNet.UnifiedBuild.Tasks.ManifestAssets
{
    public class JoinVerticalsConfig
    {
        public required string PriorityVertical { get; init; }

        public static JoinVerticalsConfig GetDefaultConfig()
        {
            JoinVerticalsConfig joinVerticalsConfig = new JoinVerticalsConfig
            {
                PriorityVertical = "Windows_x64"
            };
            return joinVerticalsConfig;
        }
    }
}
