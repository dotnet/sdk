// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateSearch.Common.Abstractions
{
    public interface IPackageInfo
    {
        public string Name { get; }

        public string? Version { get; }

        public long TotalDownloads { get; }
    }
}
