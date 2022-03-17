// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.Tools.Test
{
    // !!! FEATURES MUST BE KEPT IN SYNC WITH https://github.com/microsoft/vstest/blob/main/src/Microsoft.TestPlatform.CoreUtilities/FeatureFlag/FeatureFlag.cs !!!
    internal class FeatureFlag
    {
        private static readonly Dictionary<string, bool> FeatureFlags = new();

        private const string VSTEST_ = nameof(VSTEST_);

        public static FeatureFlag Instance { get; } = new FeatureFlag();

        static FeatureFlag()
        {
            FeatureFlags.Add(DISABLE_ARTIFACTS_POSTPROCESSING, false);
        }

        // Added for artifact post-processing, it enable/disable the post processing.
        // Added in 17.2-preview 7.0-preview
        public const string DISABLE_ARTIFACTS_POSTPROCESSING = VSTEST_ + "_" + nameof(DISABLE_ARTIFACTS_POSTPROCESSING);

        // For now we're checking env var.
        // We could add it also to some section inside the runsettings.
        public bool IsDisabled(string featureName) =>
            int.TryParse(Environment.GetEnvironmentVariable(featureName), out int disabled) ?
            disabled == 1 :
            FeatureFlags.TryGetValue(featureName, out bool isDisabled) && isDisabled;

        public void PrintFlagFeatureState()
        {
            if (VSTestTrace.TraceEnabled)
            {
                foreach (KeyValuePair<string, bool> flag in FeatureFlags)
                {
                    VSTestTrace.SafeWriteTrace(() => $"Feature {flag.Key}: {IsDisabled(flag.Key)}");
                }
            }
        }
    }
}
