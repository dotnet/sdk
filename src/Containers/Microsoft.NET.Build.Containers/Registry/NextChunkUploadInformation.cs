// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Captures the data needed to continue chunked upload
/// </summary>
internal record NextChunkUploadInformation(Uri UploadUri);
