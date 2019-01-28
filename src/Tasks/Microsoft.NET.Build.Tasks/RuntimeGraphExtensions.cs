// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.RuntimeModel;

namespace Microsoft.NET.Build.Tasks
{
    internal static class RuntimeGraphExtensions
    {
        internal static string GetBestRuntimeIdentifier(this RuntimeGraph runtimeGraph, string targetRuntimeIdentifier,
            string availableRuntimeIdentifiers,
            out bool wasInGraph)
        {
            if (targetRuntimeIdentifier == null || availableRuntimeIdentifiers == null)
            {
                wasInGraph = false;
                return null;
            }

            return NuGetUtils.GetBestMatchingRid(
                runtimeGraph,
                targetRuntimeIdentifier,
                availableRuntimeIdentifiers.Split(';'),
                out wasInGraph);
        }
    }
}
