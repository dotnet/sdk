// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.DotNet.UnifiedBuild.Tasks.UsageReport
{
    public class Usage : IEquatable<Usage>
    {
        public PackageIdentity PackageIdentity { get; set; }

        public string AssetsFile { get; set; }

        public bool IsDirectDependency { get; set; }

        public bool IsAutoReferenced { get; set; }

        /// <summary>
        /// The Runtime ID this package is for, or null. Runtime packages (are assumed to) have the
        /// id 'runtime.{rid}.{rest of id}'. We can't use a simple regex to grab this value since
        /// the RID may have '.' in it, so this is saved in the build context where possible RIDs
        /// are read from Microsoft.NETCore.Platforms.
        /// </summary>
        public string RuntimePackageRid { get; set; }

        public XElement ToXml() => new XElement(
            nameof(Usage),
            PackageIdentity.ToXElement().Attributes(),
            AssetsFile.ToXAttributeIfNotNull("File"),
            IsDirectDependency.ToXAttributeIfTrue(nameof(IsDirectDependency)),
            IsAutoReferenced.ToXAttributeIfTrue(nameof(IsAutoReferenced)),
            RuntimePackageRid.ToXAttributeIfNotNull("Rid"));

        public static Usage Parse(XElement xml) => new Usage
        {
            PackageIdentity = XmlParsingHelpers.ParsePackageIdentity(xml),
            AssetsFile = xml.Attribute("File")?.Value,
            IsDirectDependency = Convert.ToBoolean(xml.Attribute(nameof(IsDirectDependency))?.Value),
            IsAutoReferenced = Convert.ToBoolean(xml.Attribute(nameof(IsAutoReferenced))?.Value),
            RuntimePackageRid = xml.Attribute("Rid")?.Value
        };

        public static Usage Create(
            string assetsFile,
            PackageIdentity identity,
            bool isDirectDependency,
            bool isAutoReferenced,
            IEnumerable<string> possibleRuntimePackageRids)
        {
            return new Usage
            {
                AssetsFile = assetsFile,
                PackageIdentity = identity,
                IsDirectDependency = isDirectDependency,
                IsAutoReferenced = isAutoReferenced,
                RuntimePackageRid = possibleRuntimePackageRids
                    .Where(rid => identity.Id.StartsWith($"runtime.{rid}.", StringComparison.Ordinal))
                    .OrderByDescending(rid => rid.Length)
                    .FirstOrDefault()
            };
        }

        public PackageIdentity GetIdentityWithoutRid()
        {
            if (!string.IsNullOrEmpty(RuntimePackageRid))
            {
                string prefix = $"runtime.{RuntimePackageRid}.";
                if (PackageIdentity.Id.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return new PackageIdentity(
                        PackageIdentity.Id
                            .Remove(0, prefix.Length)
                            .Insert(0, "runtime.placeholder-rid."),
                        PackageIdentity.Version);
                }
            }
            return PackageIdentity;
        }

        public override string ToString() =>
            $"{PackageIdentity.Id} {PackageIdentity.Version} " +
            (string.IsNullOrEmpty(RuntimePackageRid) ? "" : $"({RuntimePackageRid}) ") +
            (string.IsNullOrEmpty(AssetsFile) ? "with unknown use" : $"used by '{AssetsFile}'");

        public bool Equals(Usage other)
        {
            if (other is null)
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return
                Equals(PackageIdentity, other.PackageIdentity) &&
                string.Equals(AssetsFile, other.AssetsFile, StringComparison.Ordinal) &&
                string.Equals(RuntimePackageRid, other.RuntimePackageRid, StringComparison.Ordinal);
        }

        public override int GetHashCode() => (
            PackageIdentity,
            AssetsFile,
            RuntimePackageRid
        ).GetHashCode();
    }
}
