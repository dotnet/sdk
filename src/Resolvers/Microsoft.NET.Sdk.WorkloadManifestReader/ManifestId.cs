// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public struct ManifestId : IEquatable<ManifestId>, IComparable<ManifestId>
    {
        private string _id;

        public ManifestId(string id)
        {
            if (!PathComponentUtilities.IsValidPathComponent(id))
            {
                throw new ArgumentException($"'{nameof(id)}' must be a valid path component", nameof(id));
            }

            _id = id.ToLowerInvariant();
        }

        public bool Equals(ManifestId other)
        {
            return ToString() == other.ToString();
        }

        public int CompareTo(ManifestId other)
        {
            return string.Compare(ToString(), other.ToString(), StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is ManifestId id && Equals(id);
        }

        public override int GetHashCode()
        {
            return _id.GetHashCode();
        }

        public override string ToString()
        {
            return _id ?? "";
        }
    }
}
