using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Cli
{
    internal class CommonArguments
    {
        #region PackageIdentityArgument
        public static readonly CliArgument<(string PackageId, string Version)> PackageIdentityArgument = new("packageId")
        {
            HelpName = "PACKAGE_ID",
            Description = CommonLocalizableStrings.PackageIdentityArgumentDescription,
            CustomParser = ParsePackageIdentity
        };
        private static (string PackageId, string Version) ParsePackageIdentity(ArgumentResult argumentResult)
        {
            if (argumentResult.Tokens.Count != 1)
            {
                throw new ArgumentException("Package id must be a single token.");
            }
            var token = argumentResult.Tokens[0].Value;
            var versionSeparatorIndex = token.IndexOf('@');
            if (versionSeparatorIndex == -1)
            {
                return (token, null);
            }
            var packageId = token.Substring(0, versionSeparatorIndex);
            var version = token.Substring(versionSeparatorIndex + 1);
            if (string.IsNullOrEmpty(packageId) || string.IsNullOrEmpty(version))
            {
                throw new ArgumentException("Package id and version must be non-empty.");
            }
            return (packageId, version);
        }
        public static void EnsureNoConflictPackageIdentityVersionOption(ParseResult parseResult)
        {
            if (!string.IsNullOrEmpty(parseResult.GetValue(PackageIdentityArgument).Version) &&
                !string.IsNullOrEmpty(parseResult.GetValue(new CliOption<string>("--version"))))
            {
                throw new ArgumentException("TODO: Cannot specify --version when the package argument already contains a version.");
            }
        }
        #endregion
    }
}
