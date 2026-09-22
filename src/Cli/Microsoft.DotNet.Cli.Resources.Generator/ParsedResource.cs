// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.DotNet.Cli.Resources.Generator;

/// <summary>
///  Represents the string entries parsed from a resource file.
/// </summary>
internal sealed class ParsedResource
{
    /// <summary>
    ///  Initializes a new instance of the <see cref="ParsedResource"/> class.
    /// </summary>
    /// <param name="entries">The parsed resource entries.</param>
    internal ParsedResource(ImmutableArray<ResourceEntry> entries) => Entries = entries;

    /// <summary>
    ///  Gets the parsed resource entries.
    /// </summary>
    internal ImmutableArray<ResourceEntry> Entries { get; }
}