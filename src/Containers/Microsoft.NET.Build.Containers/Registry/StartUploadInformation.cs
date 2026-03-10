// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Data derived from the 'start upload' call that is used to determine how perform the upload.
/// </summary>
internal record StartUploadInformation(Uri UploadUri);
