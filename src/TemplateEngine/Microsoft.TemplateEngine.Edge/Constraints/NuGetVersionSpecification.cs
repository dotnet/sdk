// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Utils;
using NuGet.Versioning;

namespace Microsoft.TemplateEngine.Edge.Constraints
{
    internal class NuGetVersionSpecification : IVersionSpecification
    {
        private readonly NuGetVersion _version;

        internal NuGetVersionSpecification(NuGetVersion version)
        {
            _version = version;
        }

        public bool CheckIfVersionIsValid(string versionToCheck)
        {
            if (NuGetVersion.TryParse(versionToCheck, out NuGetVersion? nuGetVersion2))
            {
                return _version == nuGetVersion2;
            }
            return false;
        }

        public override string ToString() => _version.ToString();

        internal static bool TryParse(string value, out NuGetVersionSpecification? version)
        {
            if (NuGetVersion.TryParse(value, out NuGetVersion? nuGetVersion))
            {
                version = new NuGetVersionSpecification(nuGetVersion!);
                return true;
            }
            version = null;
            return false;
        }

    }
}
