// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Cli.ToolPackage;

internal struct PackageId(string id) : IEquatable<PackageId>, IComparable<PackageId>
{
    private readonly string _id = id?.ToLowerInvariant() ?? throw new ArgumentNullException(nameof(id));

    public bool Equals(PackageId other)
    {
        return ToString() == other.ToString();
    }

    public int CompareTo(PackageId other)
    {
        return string.Compare(ToString(), other.ToString(), StringComparison.Ordinal);
    }

    public override bool Equals(object obj)
    {
        return obj is PackageId id && Equals(id);
    }

    public override int GetHashCode()
    {
        return ToString().GetHashCode();
    }

    public override string ToString()
    {
        return _id ?? "";
    }
}
