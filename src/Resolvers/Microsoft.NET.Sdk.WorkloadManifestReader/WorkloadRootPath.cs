﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable IDE0240
#nullable enable
#pragma warning restore IDE0240

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public record WorkloadRootPath(string? Path, bool Installable);
}
