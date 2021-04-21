// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Utils
{
    public class ExactVersionSpecification : IVersionSpecification
    {
        public ExactVersionSpecification(string version)
        {
            RequiredVersion = version;
        }

        public string RequiredVersion { get; }

        public static bool TryParse(string version, out IVersionSpecification specification)
        {
            if (!VersionStringHelpers.IsVersionWellFormed(version))
            {
                specification = null;
                return false;
            }

            specification = new ExactVersionSpecification(version);
            return true;
        }

        public bool CheckIfVersionIsValid(string versionToCheck)
        {
            int? result = VersionStringHelpers.CompareVersions(RequiredVersion, versionToCheck);
            return result.HasValue && result.Value == 0;
        }
    }
}
