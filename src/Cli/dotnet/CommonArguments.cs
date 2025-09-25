// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli
{
    internal class CommonArguments
    {
        public static DynamicArgument<PackageIdentityWithRange?> OptionalPackageIdentityArgument() =>
            OptionalPackageIdentityArgument(CliStrings.PackageIdentityArgumentDescription);

        public static DynamicArgument<PackageIdentityWithRange?> OptionalPackageIdentityArgument(string description) =>
            new("packageId")
            {
                Description = description,
                CustomParser = (ArgumentResult argumentResult) => ParsePackageIdentityWithVersionSeparator(argumentResult.Tokens[0]?.Value),
                Arity = ArgumentArity.ZeroOrOne,
            };

        public static DynamicArgument<PackageIdentityWithRange> RequiredPackageIdentityArgument() =>
            RequiredPackageIdentityArgument(CliStrings.PackageIdentityArgumentDescription);

        public static DynamicArgument<PackageIdentityWithRange> RequiredPackageIdentityArgument(string description) =>
            new("packageId")
            {
                Description = description,
                CustomParser = (ArgumentResult argumentResult) => ParsePackageIdentityWithVersionSeparator(argumentResult.Tokens[0]?.Value)!.Value,
                Arity = ArgumentArity.ExactlyOne,
            };

        private static PackageIdentityWithRange? ParsePackageIdentityWithVersionSeparator(string? packageIdentity, char versionSeparator = '@')
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
                return new PackageIdentityWithRange(packageId, null);
            }

            if (!VersionRange.TryParse(versionString, out var versionRange))
            {
                throw new GracefulException(string.Format(CliStrings.InvalidVersion, versionString));
            }

            return new PackageIdentityWithRange(packageId, versionRange);
        }
    }

    public readonly record struct PackageIdentityWithRange(string Id, VersionRange? VersionRange)
    {
        [MemberNotNullWhen(returnValue: true, nameof(VersionRange))]
        public bool HasVersion => VersionRange != null;
    }
}
