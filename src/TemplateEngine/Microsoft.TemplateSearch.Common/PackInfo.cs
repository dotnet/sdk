// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateSearch.Common
{
    public class PackInfo
    {
        public PackInfo(string name, string version)
        {
            Name = name;
            Version = version;
        }

        public PackInfo(string name, string version, long totalDownloads)
        {
            Name = name;
            Version = version;
            TotalDownloads = totalDownloads;
        }

        public static PackInfo Empty { get; set; } = new PackInfo(string.Empty, string.Empty);

        public string Name { get; }

        public string Version { get; }

        public long TotalDownloads { get; }
    }

    public class PackInfoEqualityComparer : IEqualityComparer<PackInfo>
    {
        public bool Equals(PackInfo x, PackInfo y)
        {
            return string.Equals(x.Name, y.Name) && string.Equals(x.Version, y.Version);
        }

        public int GetHashCode(PackInfo info)
        {
            return info.Name.GetHashCode() ^ info.Version.GetHashCode();
        }
    }
}
