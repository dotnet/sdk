// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Mocks;

/// <summary>An in-memory machine policy store.</summary>
internal sealed class FakeDotnetupPolicyStore : IDotnetupPolicyStore
{
    public Dictionary<string, bool> Values { get; } = new(StringComparer.Ordinal);

    /// <summary>When set, every read throws this exception.</summary>
    public Exception? ReadFailure { get; init; }

    public bool ReadFlag(string valueName)
    {
        if (ReadFailure is not null)
        {
            throw ReadFailure;
        }

        return Values.TryGetValue(valueName, out bool value) && value;
    }
}
