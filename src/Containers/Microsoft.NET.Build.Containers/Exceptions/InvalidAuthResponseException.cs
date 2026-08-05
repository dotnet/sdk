// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Thrown when a container registry returns an authentication response that fails
/// security or protocol validation.
/// </summary>
internal sealed class InvalidAuthResponseException : Exception
{
    public InvalidAuthResponseException(string registry, string reason, Exception? innerException = null)
        : base(Resource.FormatString(nameof(Strings.InvalidRegistryAuthResponse), registry, reason), innerException)
    {
        Registry = registry;
        Reason = reason;
    }

    /// <summary>The registry hostname (host[:port]) that returned the invalid auth response.</summary>
    public string Registry { get; }

    /// <summary>A short, human-readable description of why the auth response was rejected.</summary>
    public string Reason { get; }
}
