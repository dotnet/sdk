// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.SdkCustomHelix.Sdk
{
    public static class TaskItemExtensions
    {
        public static bool TryGetMetadata(this ITaskItem item, string key, out string value)
        {
            value = item.GetMetadata(key);
            return !string.IsNullOrEmpty(value);
        }

        public static bool GetRequiredMetadata(this ITaskItem item, TaskLoggingHelper log, string key, out string value)
        {
            value = item.GetMetadata(key);
            if (string.IsNullOrEmpty(value))
            {
                log.LogError($"Item '{item.ItemSpec}' missing required metadata '{key}'.");
                return false;
            }

            return true;
        }
    }
}
