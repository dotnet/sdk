// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.DotNet.Workloads.Workload
{
    internal class WorkloadUtilities
    {
        internal static int VersionCompare(string first, string second)
        {
            if (first.Equals(second))
            {
                return 0;
            }

            var firstDash = first.IndexOf('-');
            var secondDash = second.IndexOf('-');
            firstDash = firstDash < 0 ? first.Length : firstDash;
            secondDash = secondDash < 0 ? second.Length : secondDash;

            var firstVersion = new Version(first.Substring(0, firstDash));
            var secondVersion = new Version(second.Substring(0, secondDash));

            var comparison = firstVersion.CompareTo(secondVersion);
            if (comparison != 0)
            {
                return comparison;
            }

            var modifiedFirst = "1.1.1" + (firstDash == first.Length ? string.Empty : first.Substring(firstDash));
            var modifiedSecond = "1.1.1" + (secondDash == second.Length ? string.Empty : second.Substring(secondDash));

            return new ReleaseVersion(modifiedFirst).CompareTo(new ReleaseVersion(modifiedSecond));
        }
    }
}
