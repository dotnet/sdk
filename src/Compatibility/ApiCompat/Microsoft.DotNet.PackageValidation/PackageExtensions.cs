// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.ContentModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.PackageValidation
{
    internal static class PackageExtensions
    {
        private const string PlaceholderFile = "_._";

        public static bool IsPlaceholderFile(this ContentItem contentItem) =>
            Path.GetFileName(contentItem.Path) == PlaceholderFile;

        public static bool IsPlaceholderFile(this IReadOnlyList<ContentItem> contentItems) =>
            contentItems.Count == 1 && contentItems[0].IsPlaceholderFile();

        public static bool SupportsRuntimeIdentifier(this NuGetFramework tfm, string rid) =>
            tfm.Framework != ".NETFramework" || rid.StartsWith("win", StringComparison.OrdinalIgnoreCase);
    }
}
