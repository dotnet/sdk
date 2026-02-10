// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Workload;

internal static class WorkloadUtilities
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

        var modifiedFirst = new ReleaseVersion(1, 1, 1, firstDash == first.Length ? null : first.Substring(firstDash));
        var modifiedSecond = new ReleaseVersion(1, 1, 1, secondDash == second.Length ? null : second.Substring(secondDash));

        return modifiedFirst.CompareTo(modifiedSecond);
    }

    /// <summary>
    /// Determines whether workload packs and installer signatures should be verified based on whether
    /// dotnet is signed, the skip option was specified, and whether a global policy enforcing verification
    /// was set.
    /// </summary>
    /// <returns><see langword="true"/> if signatures of packages and installers should be verified.</returns>
    /// <exception cref="GracefulException" />
    public static bool ShouldVerifySignatures(bool skipSignCheck)
    {
#if DOT_NET_BUILD_FROM_SOURCE
        // Never signed on Unix
        return false;
#else
        if (!SignCheck.IsDotNetSigned())
        {
            // Can't enforce anything if we already allowed an unsigned dotnet to be installed.
            return false;
        }

        bool policyEnabled = SignCheck.IsWorkloadSignVerificationPolicySet();
        if (skipSignCheck && policyEnabled)
        {
            // Can't override the global policy by using the skip option.
            throw new GracefulException(CliCommandStrings.SkipSignCheckInvalidOption);
        }

        return !skipSignCheck;
#endif
    }
}
