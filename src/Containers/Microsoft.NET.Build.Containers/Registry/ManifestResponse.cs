// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

internal readonly record struct ManifestResponse(ReadOnlyMemory<byte> Content, string? KnownDigest, string? MediaType);
