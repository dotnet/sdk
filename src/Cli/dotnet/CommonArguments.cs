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
        public static DynamicArgument<PackageIdentity?> OptionalPackageIdentityArgument() =>
            new("packageId")
            {
                HelpName = "PACKAGE_ID",
                Description = CliStrings.PackageIdentityArgumentDescription,
                CustomParser = (ArgumentResult argumentResult) => ParsePackageIdentityWithVersionSeparator(argumentResult.Tokens[0]?.Value),
                Arity = ArgumentArity.ZeroOrOne,
            };

        public static DynamicArgument<PackageIdentity> RequiredPackageIdentityArgument() =>
            new("packageId")
            {
                HelpName = "PACKAGE_ID",
                Description = CliStrings.PackageIdentityArgumentDescription,
                CustomParser = (ArgumentResult argumentResult) => ParsePackageIdentityWithVersionSeparator(argumentResult.Tokens[0]?.Value),
                Arity = ArgumentArity.ExactlyOne,
            };


        private static PackageIdentity? ParsePackageIdentityWithVersionSeparator(string? packageIdentity, char versionSeparator = '@')
        {
            if (string.IsNullOrEmpty(packageIdentity))
            {
                return null;
            }

            string[] splitPackageIdentity = packageIdentity.Split(versionSeparator);
            var (packageId, versionString) = (splitPackageIdentity.ElementAtOrDefault(0), splitPackageIdentity.ElementAtOrDefault(1));

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
    }
}
