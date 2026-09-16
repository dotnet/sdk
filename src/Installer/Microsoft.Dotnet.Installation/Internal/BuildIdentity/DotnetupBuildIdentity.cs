// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>Exposes only the immutable build record belonging to the loaded assembly.</summary>
internal static partial class DotnetupBuildIdentity
{
    public static string Current { get; } = DotnetupBuildIdentityReader.ReadRecord(Record);
}