using System;
using System.Globalization;

namespace Microsoft.TemplateEngine.Utils
{
    public class SemanticVersion : IEquatable<SemanticVersion>, IComparable
    {
        private readonly int _hashCode;

        private SemanticVersion(string originalText, int major, int minor, int patch, string prereleaseInfo, string buildMetadata)
        {
            OriginalText = originalText;
            Major = major;
            Minor = minor;
            Patch = patch;
            PrereleaseInfo = prereleaseInfo;
            BuildMetadata = buildMetadata;
            _hashCode = major ^ minor ^ patch ^ (prereleaseInfo ?? string.Empty).GetHashCode() ^ (buildMetadata ?? string.Empty).GetHashCode();
        }

        public int Major { get; }

        public int Minor { get; }

        public int Patch { get; }

        public string PrereleaseInfo { get; }

        public string BuildMetadata { get; }

        public string OriginalText { get; }

        public static bool TryParse(string source, out SemanticVersion version)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                version = null;
                return false;
            }

            source = source.Trim();
            int majorEnd = 0,
                minorEnd = 0,
                patchEnd = 0,
                tail = 0;

            for(majorEnd = 0; majorEnd < source.Length && source[majorEnd] >= '0' && source[majorEnd] <= '9'; ++majorEnd)
            {
            }

            //If the version part was empty or multiple digits that started with zero,
            //  this isn't a valid version string
            if (majorEnd == 0 || (source[0] == '0' && majorEnd > 1))
            {
                version = null;
                return false;
            }

            if (majorEnd == source.Length || source[majorEnd] != '.')
            {
                tail = majorEnd;
                goto HandlePostVersion;
            }

            for (minorEnd = majorEnd + 1; minorEnd < source.Length && source[minorEnd] >= '0' && source[minorEnd] <= '9'; ++minorEnd)
            {
            }

            if (minorEnd == majorEnd + 1 || (source[majorEnd + 1] == '0' && minorEnd - majorEnd - 1 > 1))
            {
                version = null;
                return false;
            }

            if (minorEnd == source.Length || source[minorEnd] != '.')
            {
                tail = minorEnd;
                goto HandlePostVersion;
            }

            for (patchEnd = minorEnd + 1; patchEnd < source.Length && source[patchEnd] >= '0' && source[patchEnd] <= '9'; ++patchEnd)
            {
            }

            if (patchEnd == minorEnd + 1 || (source[minorEnd + 1] == '0' && patchEnd - minorEnd - 1 > 1))
            {
                version = null;
                return false;
            }

            tail = patchEnd;

HandlePostVersion:
            int major = int.Parse(source.Substring(0, majorEnd), NumberStyles.None, CultureInfo.InvariantCulture);
            int minor = minorEnd > majorEnd ? int.Parse(source.Substring(majorEnd + 1, minorEnd - majorEnd - 1), NumberStyles.None, CultureInfo.InvariantCulture) : 0;
            int patch = patchEnd > minorEnd ? int.Parse(source.Substring(minorEnd + 1, patchEnd - minorEnd - 1), NumberStyles.None, CultureInfo.InvariantCulture) : 0;
            string prerelease = null;
            string metadata = null;

            if (tail < source.Length && source[tail] == '-')
            {
                int end = source.IndexOf('+', tail);

                if (end < 0)
                {
                    end = source.Length;
                }

                if (tail == end - 1)
                {
                    version = null;
                    return false;
                }

                for (int i = tail + 1; i < end; ++i)
                {
                    if (!(
                        source[i] == '-' || source[i] == '.' ||
                        (source[i] >= 'A' && source[i] <= 'Z') ||
                        (source[i] >= 'a' && source[i] <= 'z') ||
                        (source[i] >= '0' && source[i] <= '9')
                        ))
                    {
                        version = null;
                        return false;
                    }
                }

                prerelease = source.Substring(tail + 1, end - tail - 1);
                tail = end;
            }

            if (tail < source.Length && source[tail] == '+')
            {
                if (tail == source.Length - 1)
                {
                    version = null;
                    return false;
                }

                for (int i = tail + 1; i < source.Length; ++i)
                {
                    if (!(
                        source[i] == '-' || source[i] == '.' ||
                        (source[i] >= 'A' && source[i] <= 'Z') ||
                        (source[i] >= 'a' && source[i] <= 'z') ||
                        (source[i] >= '0' && source[i] <= '9')
                        ))
                    {
                        version = null;
                        return false;
                    }
                }

                metadata = source.Substring(tail + 1);
            }

            version = new SemanticVersion(source, major, minor, patch, prerelease, metadata);
            return true;
        }

        private static bool TryParseSegment(string source, ref int nextDot, out int value)
        {
            nextDot = source.IndexOf('.', nextDot);

            if (nextDot < 0 || nextDot == source.Length - 1)
            {
                value = 0;
                return false;
            }

            string segment = source.Substring(0, nextDot);

            if (string.IsNullOrWhiteSpace(segment)
                || (segment.Length > 1 && segment[0] == '0')
                || !int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out value)
                || value < 0)
            {
                value = 0;
                return false;
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as SemanticVersion);
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        public bool Equals(SemanticVersion other)
        {
            if (other is null)
            {
                return false;
            }

            return CompareTo(other, out bool differentInBuildMetadataOnly) == 0 || differentInBuildMetadataOnly;
        }

        public int CompareTo(object obj)
        {
            SemanticVersion other = obj as SemanticVersion;
            return CompareTo(other);
        }

        public int CompareTo(SemanticVersion other)
        {
            return CompareTo(other, out bool ignored);
        }

        public int CompareTo(SemanticVersion other, out bool differentInBuildMetadataOnly)
        {
            differentInBuildMetadataOnly = false;

            if (other is null)
            {
                return -1;
            }

            int majorCompare = Major.CompareTo(other.Major);

            if (majorCompare != 0)
            {
                return majorCompare;
            }

            int minorCompare = Minor.CompareTo(other.Minor);

            if (minorCompare != 0)
            {
                return minorCompare;
            }

            int patchCompare = Patch.CompareTo(other.Patch);

            if (patchCompare != 0)
            {
                return patchCompare;
            }

            if (PrereleaseInfo != null)
            {
                if (other.PrereleaseInfo != null)
                {
                    int prereleaseCompare = CompareToPrereleaseInfo(other.PrereleaseInfo);

                    if (prereleaseCompare != 0)
                    {
                        return prereleaseCompare;
                    }
                }
                else
                {
                    return -1;
                }
            }
            else if (other.PrereleaseInfo != null)
            {
                return 1;
            }

            //Even though the spec says to ignore this (http://semver.org/#spec-item-10),
            //  a differentiation on this value is required in order to make a comparison
            //  sorts stable when using CompareTo(object) or CompareTo(SemanticVersion).
            //  However, the result of this portion of the comparison is ignored by the
            //  >, >=, <, <=, == and != operators
            //  
            if (BuildMetadata != null)
            {
                differentInBuildMetadataOnly = true;

                if (other.BuildMetadata != null)
                {
                    return StringComparer.OrdinalIgnoreCase.Compare(BuildMetadata, other.BuildMetadata);
                }

                return 1;
            }
            else if (other.BuildMetadata != null)
            {
                differentInBuildMetadataOnly = true;
                return -1;
            }

            return 0;
        }

        //Must apply the following rules to the prerelease section (per http://semver.org/#spec-item-11)
        //  * Identifiers consisting only of digits are compared numerically, others lexically
        //      * There's a slight divergence here from the spec in that digit-only segments
        //        that have a leading 0 (and more than one digit) are compared lexically
        //        instead of numerically as they're in violation of what's considered to
        //        be a valid numeric segment as defined in http://semver.org/#spec-item-9
        //  * If a numeric segment is compared to a non-numeric segment, the numeric segment
        //    is considered to be the lesser
        //  * If the two prerelease values, when divided into sections by periods, have a
        //    different number of segments (m and n, where m is a smaller number than n)
        //    and the first m segments of each prerelease value compare as being equal,
        //    the value with the greater number of segments (the one with n segments) is
        //    considered greater
        private int CompareToPrereleaseInfo(string other)
        {
            if (string.Equals(PrereleaseInfo, other, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            string[] thisPrerelease = PrereleaseInfo.Split('.');
            string[] otherPrerelease = other.Split('.');

            for (int i = 0; i < thisPrerelease.Length && i < otherPrerelease.Length; ++i)
            {
                if (IsNumericSegment(thisPrerelease[i]))
                {
                    if (IsNumericSegment(otherPrerelease[i]))
                    {
                        int us = int.Parse(thisPrerelease[i], NumberStyles.None, CultureInfo.InvariantCulture);
                        int them = int.Parse(otherPrerelease[i], NumberStyles.None, CultureInfo.InvariantCulture);
                        int numericCompare = us.CompareTo(them);

                        if (numericCompare != 0)
                        {
                            return numericCompare;
                        }
                    }
                    else
                    {
                        return -1;
                    }
                }
                else if (IsNumericSegment(otherPrerelease[i]))
                {
                    return 1;
                }

                int segmentCompare = StringComparer.OrdinalIgnoreCase.Compare(thisPrerelease[i], otherPrerelease[i]);

                if (segmentCompare != 0)
                {
                    return segmentCompare;
                }
            }

            return thisPrerelease.Length.CompareTo(otherPrerelease.Length);
        }

        //Determines whether a numeric segment is valid per the rules in
        //  http://semver.org/#spec-item-9 and http://semver.org/#spec-item-2
        private static bool IsNumericSegment(string segment)
        {
            if (segment.Length == 0)
            {
                return false;
            }

            bool isNumeric = true;
            for (int i = 0; isNumeric && i < segment.Length; ++i)
            {
                isNumeric &= segment[i] >= '0' && segment[i] <= '9';
            }

            return isNumeric && (segment.Length == 1 || segment[0] != '0');
        }

        public static bool operator ==(SemanticVersion left, SemanticVersion right)
        {
            return !(left is null) && left.Equals(right);
        }

        public static bool operator !=(SemanticVersion left, SemanticVersion right)
        {
            return !(left == right);
        }

        public static bool operator <(SemanticVersion left, SemanticVersion right)
        {
            if (left is null)
            {
                return !(right is null);
            }

            return left.CompareTo(right, out bool isDifferentInBuildMetadataOnly) < 0 && !isDifferentInBuildMetadataOnly;
        }

        public static bool operator >(SemanticVersion left, SemanticVersion right)
        {
            if (right is null)
            {
                return !(left is null);
            }

            return left.CompareTo(right, out bool isDifferentInBuildMetadataOnly) > 0 && !isDifferentInBuildMetadataOnly;
        }

        public static bool operator <=(SemanticVersion left, SemanticVersion right)
        {
            if (left is null)
            {
                return !(right is null);
            }

            return left.CompareTo(right, out bool isDifferentInBuildMetadataOnly) <= 0 || isDifferentInBuildMetadataOnly;
        }

        public static bool operator >=(SemanticVersion left, SemanticVersion right)
        {
            if (right is null)
            {
                return !(left is null);
            }

            return left.CompareTo(right, out bool isDifferentInBuildMetadataOnly) >= 0 || isDifferentInBuildMetadataOnly;
        }

        public override string ToString()
        {
            string result = $"{Major}.{Minor}.{Patch}";

            if (PrereleaseInfo != null)
            {
                result += $"-{PrereleaseInfo}";
            }

            if (BuildMetadata != null)
            {
                result += $"+{BuildMetadata}";
            }

            return result + $" ({OriginalText})";
        }
    }
}
