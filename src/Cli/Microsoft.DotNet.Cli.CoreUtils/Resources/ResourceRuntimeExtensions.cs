// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;

namespace Microsoft.DotNet.Cli.Resources.Internal;

internal static class ResourceRuntimeExtensions
{
    internal static bool AreFlagsSet(
        this StringResourceManagerOptions value,
        StringResourceManagerOptions flags) => (value & flags) == flags;

    internal static bool AreFlagsSet(this AssemblyFlags value, AssemblyFlags flags) =>
        (value & flags) == flags;
}

internal static class ResourceNameHash
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Compute(ReadOnlySpan<char> value)
    {
        uint hash = 5381;
        for (int index = 0; index < value.Length; index++)
        {
            hash = ((hash << 5) + hash) ^ value[index];
        }

        return (int)hash;
    }
}
