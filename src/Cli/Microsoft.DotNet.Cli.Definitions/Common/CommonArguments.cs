// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.StaticCompletions;
using System.Diagnostics.CodeAnalysis;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli
{
    internal static class CommonArguments
    {
        public const string PackageIdArgumentName = "packageId";

        public static Argument<PackageIdentityWithRange?> CreateOptionalPackageIdentityArgument(string examplePackage = "Newtonsoft.Json", string exampleVersion = "13.0.3") =>
            new(PackageIdArgumentName)
            {
                Description = string.Format(CommandDefinitionStrings.PackageIdentityArgumentDescription, examplePackage, exampleVersion),
                CustomParser = argumentResult => ParsePackageIdentityWithVersionSeparator(argumentResult.Tokens[0]?.Value),
                Arity = ArgumentArity.ZeroOrOne,
                IsDynamic = true
            };

        public static Argument<PackageIdentityWithRange> CreateRequiredPackageIdentityArgument(string examplePackage = "Newtonsoft.Json", string exampleVersion = "13.0.3") =>
            new(PackageIdArgumentName)
            {
                Description = string.Format(CommandDefinitionStrings.PackageIdentityArgumentDescription, examplePackage, exampleVersion),
                CustomParser = argumentResult => ParsePackageIdentityWithVersionSeparator(argumentResult.Tokens[0]?.Value)!.Value,
                Arity = ArgumentArity.ExactlyOne,
                IsDynamic = true
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
                throw new GracefulException(CommandDefinitionStrings.PackageIdentityArgumentIdOrVersionIsNull);
            }

            if (string.IsNullOrEmpty(versionString))
            {
                return new PackageIdentityWithRange(packageId, null);
            }

            if (!VersionRange.TryParse(versionString, out var versionRange))
            {
                throw new GracefulException(string.Format(CommandDefinitionStrings.InvalidVersion, versionString));
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
