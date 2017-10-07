using System;
using System.Globalization;

namespace Microsoft.TemplateEngine.Utils
{
    /// <summary>
    /// Wrapper for working with Semantic Versioning v2.0 (http://semver.org/)
    /// </summary>
    public class SemanticVersion : IEquatable<SemanticVersion>, IComparable
    {
        private readonly int _hashCode;

        /// <summary>
        /// Creates a new semantic version object
        /// </summary>
        /// <param name="originalText">The original text that had been parsed</param>
        /// <param name="major">The interpreted major version number</param>
        /// <param name="minor">The interpreted minor version number</param>
        /// <param name="patch">The interpreted patch number</param>
        /// <param name="prereleaseInfo">The prerelease information section, if any (should be null if none was found)</param>
        /// <param name="buildMetadata">The build metadata section, if any (should be null if none was found)</param>
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

        /// <summary>
        /// Gets the build metadata section (if any), null if no build metadata has been specified
        /// </summary>
        public string BuildMetadata { get; }

        /// <summary>
        /// Gets the interpreted major version number
        /// </summary>
        public int Major { get; }

        /// <summary>
        /// Gets the interpreted minor version number
        /// </summary>
        public int Minor { get; }

        /// <summary>
        /// Gets the original text that this object represents
        /// </summary>
        public string OriginalText { get; }

        /// <summary>
        /// Gets the patch number
        /// </summary>
        public int Patch { get; }

        /// <summary>
        /// Gets the prerelease section (if any), null if no prerelease information was specified
        /// </summary>
        public string PrereleaseInfo { get; }

        /// <summary>
        /// Compares the current instance with another object of the same type and returns an
        /// integer that indicates whether the current instance precedes, follows, or occurs
        /// in the same position in the sort order as the other object.
        /// </summary>
        /// <param name="obj">An object to compare with this instance.</param>
        /// <returns>A value that indicates the relative order of the objects being compared.</returns>
        public int CompareTo(object obj)
        {
            SemanticVersion other = obj as SemanticVersion;
            return CompareTo(other);
        }

        /// <summary>
        /// Compares the current instance with another object of the same type and returns an
        /// integer that indicates whether the current instance precedes, follows, or occurs
        /// in the same position in the sort order as the other object.
        /// </summary>
        /// <param name="other">An object to compare with this instance.</param>
        /// <returns>A value that indicates the relative order of the objects being compared.</returns>
        public int CompareTo(SemanticVersion other)
        {
            return CompareTo(other, out bool ignored);
        }

        /// <summary>
        /// Compares the current instance with another object of the same type and returns an
        /// integer that indicates whether the current instance precedes, follows, or occurs
        /// in the same position in the sort order as the other object.
        /// </summary>
        /// <param name="other">An object to compare with this instance.</param>
        /// <param name="differentInBuildMetadataOnly">[Out] Whether this object and the one to compare with differ only by build metadata.</param>
        /// <returns>A value that indicates the relative order of the objects being compared.</returns>
        public int CompareTo(SemanticVersion other, out bool differentInBuildMetadataOnly)
        {
            differentInBuildMetadataOnly = false;

            if (other is null)
            {
                return -1;
            }

            int versionCompare = CompareVersionInformation(other);

            if (versionCompare != 0)
            {
                return versionCompare;
            }

            int prereleaseCompare = ComparePrereleaseInfo(other);

            if (prereleaseCompare != 0)
            {
                return prereleaseCompare;
            }

            int buildMetadataCompare = CompareBuildMetadata(other);

            if (buildMetadataCompare != 0)
            {
                differentInBuildMetadataOnly = true;
                return buildMetadataCompare;
            }

            return 0;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            return Equals(obj as SemanticVersion);
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="other">The object to compare with the current object.</param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        public bool Equals(SemanticVersion other)
        {
            if (other is null)
            {
                return false;
            }

            return CompareTo(other, out bool differentInBuildMetadataOnly) == 0 || differentInBuildMetadataOnly;
        }

        /// <summary>
        /// Serves as the default hash function.
        /// </summary>
        /// <returns>A hash code for the current object.</returns>
        public override int GetHashCode()
        {
            return _hashCode;
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
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

        /// <summary>
        /// Attempts to parse a string as a semantic version
        /// </summary>
        /// <param name="source">The text to attempt to parse</param>
        /// <param name="version">[Out] The resulting version object if the parse was successful.</param>
        /// <returns>true if the parse was successful, false otherwise.</returns>
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

            if (!IsolateVersionRange(source, -1, ref majorEnd))
            {
                version = null;
                return false;
            }

            if (majorEnd == source.Length || source[majorEnd] != '.')
            {
                tail = majorEnd;
                return TryParsePrereleaseAndBuildMetadata(source, majorEnd, minorEnd, patchEnd, tail, out version);
            }

            if (!IsolateVersionRange(source, majorEnd, ref minorEnd))
            {
                version = null;
                return false;
            }

            if (minorEnd == source.Length || source[minorEnd] != '.')
            {
                tail = minorEnd;
                return TryParsePrereleaseAndBuildMetadata(source, majorEnd, minorEnd, patchEnd, tail, out version);
            }

            if (!IsolateVersionRange(source, minorEnd, ref patchEnd))
            {
                version = null;
                return false;
            }

            tail = patchEnd;
            return TryParsePrereleaseAndBuildMetadata(source, majorEnd, minorEnd, patchEnd, tail, out version);
        }

        /// <summary>
        /// Determines whether two semantic versions are value equal.
        /// </summary>
        /// <param name="left">The first value to compare.</param>
        /// <param name="right">The second value to compare.</param>
        /// <returns>true if the versions are value equal, false otherwise.</returns>
        /// <remarks>
        /// This respects the portion of the specification that indicates
        /// that build metadata should not impact precedence by returning
        /// true for versions that differ only in that field.
        /// </remarks>
        public static bool operator ==(SemanticVersion left, SemanticVersion right)
        {
            return !(left is null) && left.Equals(right);
        }

        /// <summary>
        /// Determines whether two semantic versions are not value equal.
        /// </summary>
        /// <param name="left">The first value to compare.</param>
        /// <param name="right">The second value to compare.</param>
        /// <returns>true if the versions are value equal, false otherwise.</returns>
        /// <remarks>
        /// This respects the portion of the specification that indicates
        /// that build metadata should not impact precedence by returning
        /// false for versions that differ only in that field.
        /// </remarks>
        public static bool operator !=(SemanticVersion left, SemanticVersion right)
        {
            return !(left == right);
        }

        /// <summary>
        /// Determines whether the first version has lower precedence than the second version.
        /// </summary>
        /// <param name="left">The first value to compare.</param>
        /// <param name="right">The second value to compare.</param>
        /// <returns>true if the first version has lower precedence than the second, false otherwise.</returns>
        /// <remarks>
        /// This respects the portion of the specification that indicates
        /// that build metadata should not impact precedence by returning
        /// false for versions that differ only in that field.
        /// </remarks>
        public static bool operator <(SemanticVersion left, SemanticVersion right)
        {
            if (left is null)
            {
                return !(right is null);
            }

            return left.CompareTo(right, out bool isDifferentInBuildMetadataOnly) < 0 && !isDifferentInBuildMetadataOnly;
        }

        /// <summary>
        /// Determines whether the first version has lower or equal precedence than the second version.
        /// </summary>
        /// <param name="left">The first value to compare.</param>
        /// <param name="right">The second value to compare.</param>
        /// <returns>true if the first version has lower or equal precedence than the second, false otherwise.</returns>
        /// <remarks>
        /// This respects the portion of the specification that indicates
        /// that build metadata should not impact precedence by returning
        /// true for versions that differ only in that field.
        /// </remarks>
        public static bool operator <=(SemanticVersion left, SemanticVersion right)
        {
            if (left is null)
            {
                return !(right is null);
            }

            return left.CompareTo(right, out bool isDifferentInBuildMetadataOnly) <= 0 || isDifferentInBuildMetadataOnly;
        }

        /// <summary>
        /// Determines whether the first version has higher precedence than the second version.
        /// </summary>
        /// <param name="left">The first value to compare.</param>
        /// <param name="right">The second value to compare.</param>
        /// <returns>true if the first version has higher precedence than the second, false otherwise.</returns>
        /// <remarks>
        /// This respects the portion of the specification that indicates
        /// that build metadata should not impact precedence by returning
        /// false for versions that differ only in that field.
        /// </remarks>
        public static bool operator >(SemanticVersion left, SemanticVersion right)
        {
            if (right is null)
            {
                return !(left is null);
            }

            return left.CompareTo(right, out bool isDifferentInBuildMetadataOnly) > 0 && !isDifferentInBuildMetadataOnly;
        }

        /// <summary>
        /// Determines whether the first version has higher or equal precedence than the second version.
        /// </summary>
        /// <param name="left">The first value to compare.</param>
        /// <param name="right">The second value to compare.</param>
        /// <returns>true if the first version has higher or equal precedence than the second, false otherwise.</returns>
        /// <remarks>
        /// This respects the portion of the specification that indicates
        /// that build metadata should not impact precedence by returning
        /// true for versions that differ only in that field.
        /// </remarks>
        public static bool operator >=(SemanticVersion left, SemanticVersion right)
        {
            if (right is null)
            {
                return !(left is null);
            }

            return left.CompareTo(right, out bool isDifferentInBuildMetadataOnly) >= 0 || isDifferentInBuildMetadataOnly;
        }

        private int CompareBuildMetadata(SemanticVersion other)
        {
            //Even though the spec says to ignore this (http://semver.org/#spec-item-10),
            //  a differentiation on this value is required in order to make a comparison
            //  sorts stable when using CompareTo(object) or CompareTo(SemanticVersion).
            //  However, the result of this portion of the comparison is ignored by the
            //  >, >=, <, <=, == and != operators
            //  
            if (BuildMetadata != null)
            {
                if (other.BuildMetadata != null)
                {
                    return StringComparer.OrdinalIgnoreCase.Compare(BuildMetadata, other.BuildMetadata);
                }

                return 1;
            }
            else if (other.BuildMetadata != null)
            {
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
                int segmentCompare = CompareToPrereleaseInfoSegment(thisPrerelease[i], otherPrerelease[i]);

                if (segmentCompare != 0)
                {
                    return segmentCompare;
                }
            }

            return thisPrerelease.Length.CompareTo(otherPrerelease.Length);
        }

        private static int CompareToPrereleaseInfoSegment(string left, string right)
        {
            if (IsNumericSegment(left))
            {
                if (IsNumericSegment(right))
                {
                    int us = int.Parse(left, NumberStyles.None, CultureInfo.InvariantCulture);
                    int them = int.Parse(right, NumberStyles.None, CultureInfo.InvariantCulture);
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
            else if (IsNumericSegment(right))
            {
                return 1;
            }

            int segmentCompare = StringComparer.OrdinalIgnoreCase.Compare(left, right);

            if (segmentCompare != 0)
            {
                return segmentCompare;
            }

            return 0;
        }

        private int CompareVersionInformation(SemanticVersion other)
        {
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

            return 0;
        }

        private int ComparePrereleaseInfo(SemanticVersion other)
        {
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

            return 0;
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

        private static bool IsolateVersionRange(string source, int start, ref int end)
        {
            for (end = start + 1; end < source.Length && source[end] >= '0' && source[end] <= '9'; ++end)
            {
            }

            return !(end == start + 1 || (source[start + 1] == '0' && end - start - 1 > 1));
        }

        private static bool IsCharacterValidForMetadataSection(char c)
        {
            return c == '-' || c == '.' ||
                   (c >= 'A' && c <= 'Z') ||
                   (c >= 'a' && c <= 'z') ||
                   (c >= '0' && c <= '9');
        }

        private static bool IsValidMetadataSection(string source, int start, int afterEnd)
        {
            for (int i = start; i < afterEnd; ++i)
            {
                if (!IsCharacterValidForMetadataSection(source[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryParsePrereleaseAndBuildMetadata(string source, int majorEnd, int minorEnd, int patchEnd, int tail, out SemanticVersion version)
        {
            int major = int.Parse(source.Substring(0, majorEnd), NumberStyles.None, CultureInfo.InvariantCulture);
            int minor = minorEnd > majorEnd ? int.Parse(source.Substring(majorEnd + 1, minorEnd - majorEnd - 1), NumberStyles.None, CultureInfo.InvariantCulture) : 0;
            int patch = patchEnd > minorEnd ? int.Parse(source.Substring(minorEnd + 1, patchEnd - minorEnd - 1), NumberStyles.None, CultureInfo.InvariantCulture) : 0;
            string prerelease = null;
            string metadata = null;

            if (!ValidateAndExtractPrereleaseSection(source, ref tail, out prerelease))
            {
                version = null;
                return false;
            }

            if (!ValidateAndExtractBuildMetadataSection(source, tail, out metadata))
            {
                version = null;
                return false;
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

        private static bool ValidateAndExtractBuildMetadataSection(string source, int tail, out string metadata)
        {
            if (tail < source.Length && source[tail] == '+')
            {
                if (tail == source.Length - 1 || !IsValidMetadataSection(source, tail + 1, source.Length))
                {
                    metadata = null;
                    return false;
                }

                metadata = source.Substring(tail + 1);
                return true;
            }

            metadata = null;
            return true;
        }

        private static bool ValidateAndExtractPrereleaseSection(string source, ref int tail, out string prerelease)
        {
            if (tail < source.Length && source[tail] == '-')
            {
                int end = source.IndexOf('+', tail);

                if (end < 0)
                {
                    end = source.Length;
                }

                if (tail == end - 1)
                {
                    prerelease = null;
                    return false;
                }

                if (!IsValidMetadataSection(source, tail + 1, end))
                {
                    prerelease = null;
                    return false;
                }

                prerelease = source.Substring(tail + 1, end - tail - 1);
                tail = end;
                return true;
            }

            prerelease = null;
            return true;
        }
    }
}
