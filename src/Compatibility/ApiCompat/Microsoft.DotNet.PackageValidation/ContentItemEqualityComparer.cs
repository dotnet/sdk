// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.ContentModel;

namespace Microsoft.DotNet.PackageValidation
{
    internal class ContentItemEqualityComparer : IEqualityComparer<ContentItem>
    {
        public static readonly ContentItemEqualityComparer Instance = new();

        private ContentItemEqualityComparer()
        {
        }

        public bool Equals(ContentItem? x, ContentItem? y) => string.Equals(x?.Path, y?.Path);

        public int GetHashCode(ContentItem obj) => obj.Path.GetHashCode();
    }
}
