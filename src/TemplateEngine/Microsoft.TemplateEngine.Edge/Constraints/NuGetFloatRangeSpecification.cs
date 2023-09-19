// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Utils;
using NuGet.Versioning;

namespace Microsoft.TemplateEngine.Edge.Constraints
{
    internal class NuGetFloatRangeSpecification : IVersionSpecification
    {
        private readonly FloatRange _version;

        internal NuGetFloatRangeSpecification(FloatRange version)
        {
            _version = version;
        }

        public bool CheckIfVersionIsValid(string versionToCheck)
        {
            if (NuGetVersion.TryParse(versionToCheck, out NuGetVersion? nuGetVersion2))
            {
                return _version.Satisfies(nuGetVersion2);
            }
            return false;
        }

        public override string ToString() => _version.ToString();

        internal static bool TryParse(string value, out NuGetFloatRangeSpecification? version)
        {
            if (FloatRange.TryParse(value, out FloatRange? versionRange))
            {
                version = new NuGetFloatRangeSpecification(versionRange!);
                return true;
            }
            version = null;
            return false;
        }
    }
}
