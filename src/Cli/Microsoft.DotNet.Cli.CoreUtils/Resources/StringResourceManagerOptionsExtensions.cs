// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Resources.Internal;

/// <summary>
///  Validation helpers for <see cref="StringResourceManagerOptions"/>.
/// </summary>
internal static class StringResourceManagerOptionsExtensions
{
    /// <summary>
    ///  Validates that <paramref name="options"/> contains only defined flags.
    /// </summary>
    /// <param name="options">The options to validate.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="options"/> contains an unknown flag.</exception>
    internal static void Validate(this StringResourceManagerOptions options)
    {
        if (options is not (StringResourceManagerOptions.None
            or StringResourceManagerOptions.IgnoreNonStringResources))
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
    }
}
