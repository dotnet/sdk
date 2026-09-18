// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>Exposes the immutable local version/RID record belonging to the loaded image.</summary>
internal static partial class DotnetupVersionMetadata
{
    public static string Current { get; } = DotnetupVersionMetadataReader.ReadRecord(Record);
}