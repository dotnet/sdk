// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Utils;
using NuGet.Versioning;

namespace Microsoft.TemplateEngine.Edge.Constraints
{
    internal class NuGetVersionRangeSpecification : IVersionSpecification
    {
        private readonly VersionRange _versionRange;

        internal NuGetVersionRangeSpecification(VersionRange versionRange)
        {
            _versionRange = versionRange;
        }

        public bool CheckIfVersionIsValid(string versionToCheck)
        {
            if (NuGetVersion.TryParse(versionToCheck, out NuGetVersion? nuGetVersion2))
            {
                return _versionRange.Satisfies(nuGetVersion2);
            }
            return false;
        }

        public override string ToString() => _versionRange.ToString();

        internal static bool TryParse(string value, out NuGetVersionRangeSpecification? version)
        {
            if (VersionRange.TryParse(value, out VersionRange? versionRange))
            {
                version = new NuGetVersionRangeSpecification(versionRange!);
                return true;
            }
            version = null;
            return false;
        }
    }
}
