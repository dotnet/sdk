// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using NuGet.Packaging.Core;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.DotNet.UnifiedBuild.Tasks.UsageReport
{
    internal static class XmlParsingHelpers
    {
        public static XElement ToXElement(this PackageIdentity ident) => new XElement(
            "PackageIdentity",
            new XAttribute("Id", ident.Id),
            new XAttribute("Version", ident.Version.OriginalVersion));

        public static XAttribute ToXAttributeIfNotNull(this object value, string name) =>
            value == null ? null : new XAttribute(name, value);

        public static XAttribute ToXAttributeIfTrue(this bool value, string name) =>
            value == false ? null : new XAttribute(name, value);

        public static PackageIdentity ParsePackageIdentity(XElement xml) => new PackageIdentity(
            xml.Attribute("Id").Value,
            NuGetVersion.Parse(xml.Attribute("Version").Value));

        public static IOrderedEnumerable<T> OrderByOrdinal<T>(
            this IEnumerable<T> source,
            Func<T, string> selector)
        {
            return source.OrderBy(selector, StringComparer.Ordinal);
        }

        public static IOrderedEnumerable<T> ThenByOrdinal<T>(
            this IOrderedEnumerable<T> source,
            Func<T, string> selector)
        {
            return source.ThenBy(selector, StringComparer.Ordinal);
        }
    }
}
