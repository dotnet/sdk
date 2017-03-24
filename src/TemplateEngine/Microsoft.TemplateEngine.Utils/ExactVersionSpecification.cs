using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Utils
{
    public class ExactVersionSpecification : IVersionSpecification
    {
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

        public ExactVersionSpecification(string version)
        {
            RequiredVersion = version;
        }

        public string RequiredVersion { get; }

        public bool CheckIfVersionIsValid(string versionToCheck)
        {
            int? result = VersionStringHelpers.CompareVersions(RequiredVersion, versionToCheck);
            return result.HasValue && result.Value == 0;
        }
    }
}
