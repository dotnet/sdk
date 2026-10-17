// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Mocks;

/// <summary>An in-memory machine policy store that records every write.</summary>
internal sealed class FakeDotnetupPolicyStore : IDotnetupPolicyStore
{
    public Dictionary<string, bool> Values { get; } = new(StringComparer.Ordinal);

    public List<Dictionary<string, bool>> Writes { get; } = [];

    /// <summary>When set, every read throws this exception.</summary>
    public Exception? ReadFailure { get; init; }

    /// <summary>When set, every write throws this exception and changes nothing.</summary>
    public Exception? WriteFailure { get; init; }

    public bool ReadFlag(string valueName)
    {
        if (ReadFailure is not null)
        {
            throw ReadFailure;
        }

        return Values.TryGetValue(valueName, out bool value) && value;
    }

    public void WriteFlags(IReadOnlyDictionary<string, bool> values)
    {
        if (WriteFailure is not null)
        {
            throw WriteFailure;
        }

        Writes.Add(new Dictionary<string, bool>(values));
        foreach (var (name, value) in values)
        {
            Values[name] = value;
        }
    }
}
