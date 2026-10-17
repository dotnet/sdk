// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Mocks;

internal sealed class FakeDotnetupPolicyStore : IDotnetupPolicyStore
{
    public Dictionary<string, bool> Values { get; } = new(StringComparer.Ordinal);

    public List<Dictionary<string, bool>> Writes { get; } = [];

    public bool ReadFlag(string valueName) => Values.TryGetValue(valueName, out bool value) && value;

    public void WriteFlags(IReadOnlyDictionary<string, bool> values)
    {
        Writes.Add(new Dictionary<string, bool>(values));
        foreach (var (name, value) in values)
        {
            Values[name] = value;
        }
    }
}
