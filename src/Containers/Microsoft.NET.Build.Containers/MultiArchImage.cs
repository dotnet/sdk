// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Represents constructed image ready for further processing.
/// </summary>
internal readonly struct MultiArchImage
{
    internal required string ImageIndex { get; init; }

    internal required string ImageIndexMediaType { get; init; }

    internal BuiltImage[]? Images { get; init; }
}