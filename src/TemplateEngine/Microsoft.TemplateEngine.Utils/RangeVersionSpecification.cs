// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.TemplateEngine.Utils
{
    public class RangeVersionSpecification : IVersionSpecification
    {
        public RangeVersionSpecification(string min, string max, bool isStartInclusive, bool isEndInclusive)
        {
            MinVersion = min;
            MaxVersion = max;
            IsStartInclusive = isStartInclusive;
            IsEndInclusive = isEndInclusive;
        }

        public string MinVersion { get; }

        public string MaxVersion { get; }

        public bool IsStartInclusive { get; }

        public bool IsEndInclusive { get; }

        public static bool TryParse(string range, out IVersionSpecification? specification)
        {
            bool startInclusive = false;
            bool endInclusive = false;

            if (range.StartsWith("["))
            {
                startInclusive = true;
            }
            else if (!range.StartsWith("("))
            {
                specification = null;
                return false;
            }

            if (range.EndsWith("]"))
            {
                endInclusive = true;
            }
            else if (!range.EndsWith(")"))
            {
                specification = null;
                return false;
            }

            string[] parts = range.Split('-');
            if (parts.Length != 2)
            {
                specification = null;
                return false;
            }

            string startVersion = parts[0].Substring(1);
            string endVersion = parts[1].Substring(0, parts[1].Length - 1);

            if (IsWildcardVersion(startVersion) && IsWildcardVersion(endVersion))
            {
                specification = null;
                return false;
            }
            else if (!IsWildcardVersion(startVersion) && !VersionStringHelpers.IsVersionWellFormed(startVersion))
            {
                specification = null;
                return false;
            }
            else if (!IsWildcardVersion(endVersion) && !VersionStringHelpers.IsVersionWellFormed(endVersion))
            {
                specification = null;
                return false;
            }

            specification = new RangeVersionSpecification(startVersion, endVersion, startInclusive, endInclusive);
            return true;
        }

        public bool CheckIfVersionIsValid(string versionToCheck)
        {
            bool isStartValid;
            bool isEndValid;

            if (!IsWildcardVersion(MinVersion))
            {
                int? startComparison = VersionStringHelpers.CompareVersions(MinVersion, versionToCheck);

                if (startComparison == null)
                {
                    return false;
                }

                if (IsStartInclusive)
                {
                    isStartValid = startComparison.Value <= 0;
                }
                else
                {
                    isStartValid = startComparison.Value < 0;
                }
            }
            else
            {
                isStartValid = true;
            }

            if (!IsWildcardVersion(MaxVersion))
            {
                int? endComparison = VersionStringHelpers.CompareVersions(versionToCheck, MaxVersion);

                if (endComparison == null)
                {
                    return false;
                }

                if (IsEndInclusive)
                {
                    isEndValid = endComparison.Value <= 0;
                }
                else
                {
                    isEndValid = endComparison.Value < 0;
                }
            }
            else
            {
                isEndValid = true;
            }

            return isStartValid && isEndValid;
        }

        private static bool IsWildcardVersion(string version)
        {
            return string.Equals(version, "*");
        }
    }
}
