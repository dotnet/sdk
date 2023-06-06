// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.Registry;

/// <summary>
/// Captures the data needed to finalize an upload
/// </summary>
internal record FinalizeUploadInformation(Uri uploadUri);
