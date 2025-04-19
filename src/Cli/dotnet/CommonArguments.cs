// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli
{
    internal class CommonArguments
    {
        #region PackageIdentityArgument
        public static Argument<PackageIdentity?> PackageIdentityArgument(bool requireArgument = true) =>
            new("packageId")
            {
                HelpName = "PACKAGE_ID",
                Description = CliStrings.PackageIdentityArgumentDescription,
                CustomParser = ParsePackageIdentity,
                Arity = requireArgument ? ArgumentArity.ExactlyOne : ArgumentArity.ZeroOrOne,
            };

        private static PackageIdentity? ParsePackageIdentity(ArgumentResult argumentResult)
        {
            if (argumentResult.Tokens.Count == 0)
            {
                return null;
            }

            string[] splitToken = argumentResult.Tokens[0].Value.Split('@');
            var (packageId, versionString) = (splitToken.ElementAtOrDefault(0), splitToken.ElementAtOrDefault(1));

            if (string.IsNullOrEmpty(packageId))
            {
                throw new GracefulException(CliStrings.PackageIdentityArgumentIdOrVersionIsNull);
            }

            if (string.IsNullOrEmpty(versionString))
            {
                return new PackageIdentity(packageId, null);
            }

            if (!NuGetVersion.TryParse(versionString, out var version))
            {
                throw new GracefulException(string.Format(CliStrings.InvalidVersion, versionString));
            }

            return new PackageIdentity(packageId, new NuGetVersion(version));
        }
        #endregion
    }
}
