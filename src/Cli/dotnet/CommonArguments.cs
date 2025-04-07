using System.CommandLine;
using System.CommandLine.Parsing;
using NuGet.Packaging.Core;
using NuGet.Versioning;


namespace Microsoft.DotNet.Cli
{
    internal class CommonArguments
    {
        #region PackageIdentityArgument
        public static CliArgument<PackageIdentity> PackageIdentityArgument(bool requireArgument = true) =>
            new("packageId")
            {
                HelpName = "PACKAGE_ID",
                Description = CommonLocalizableStrings.PackageIdentityArgumentDescription,
                CustomParser = ParsePackageIdentity,
                Arity = requireArgument ? ArgumentArity.ExactlyOne : ArgumentArity.ZeroOrOne,
            };

        private static PackageIdentity ParsePackageIdentity(ArgumentResult argumentResult)
        {
            var token = argumentResult.Tokens[0].Value;
            var versionSeparatorIndex = token.IndexOf('@');
            if (versionSeparatorIndex == -1)
            {
                return new(token, null);
            }
            var packageId = token.Substring(0, versionSeparatorIndex);
            var versionString = token.Substring(versionSeparatorIndex + 1);
            if (string.IsNullOrEmpty(packageId) || string.IsNullOrEmpty(versionString))
            {
                throw new ArgumentException(CommonLocalizableStrings.PackageIdentityArgumentIdOrVersionIsNull);
            }
            if (!NuGetVersion.TryParse(versionString, out var version))
            {
                throw new ArgumentException(string.Format(CommonLocalizableStrings.InvalidVersion, versionString));
            }
            return new(packageId, new NuGetVersion(version));
        }
        public static void EnsureNoConflictPackageIdentityVersionOption(ParseResult parseResult)
        {
            if (!string.IsNullOrEmpty(parseResult.GetValue(PackageIdentityArgument(false)).Version.ToString()) &&
                !string.IsNullOrEmpty(parseResult.GetValue(new CliOption<string>("--version"))))
            {
                throw new ArgumentException(CommonLocalizableStrings.PackageIdentityArgumentVersionOptionConflict);
            }
        }
        #endregion
    }
}
