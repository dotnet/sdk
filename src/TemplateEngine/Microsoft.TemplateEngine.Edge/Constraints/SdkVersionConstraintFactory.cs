// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Components;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Edge.Constraints
{
    public sealed class SdkVersionConstraintFactory : ITemplateConstraintFactory
    {
        Guid IIdentifiedComponent.Id { get; } = Guid.Parse("{4E9721EF-0C02-4C09-A5A4-56C3D29BFC8E}");

        string ITemplateConstraintFactory.Type => "sdk-version";

        async Task<ITemplateConstraint> ITemplateConstraintFactory.CreateTemplateConstraintAsync(IEngineEnvironmentSettings environmentSettings, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // need to await due to lack of covariance on Tasks
            return await SdkVersionConstraint.CreateAsync(environmentSettings, this, cancellationToken).ConfigureAwait(false);
        }

        internal class SdkVersionConstraint : ConstraintBase
        {
            private readonly NuGetVersionSpecification _currentSdkVersion;
            private readonly IReadOnlyList<NuGetVersionSpecification> _installedSdkVersion;
            private readonly Func<IReadOnlyList<string>, IReadOnlyList<string>, string> _remedySuggestionFactory;

            private SdkVersionConstraint(
                IEngineEnvironmentSettings environmentSettings,
                ITemplateConstraintFactory factory,
                NuGetVersionSpecification currentSdkVersion,
                IEnumerable<NuGetVersionSpecification> installedSdkVersions,
                Func<IReadOnlyList<string>, IReadOnlyList<string>, string> remedySuggestionFactory)
                : base(environmentSettings, factory)
            {
                _currentSdkVersion = currentSdkVersion;
                _installedSdkVersion = installedSdkVersions.ToList();
                _remedySuggestionFactory = remedySuggestionFactory;
            }

            public override string DisplayName => LocalizableStrings.SdkVersionConstraint_Name;

            internal static async Task<SdkVersionConstraint> CreateAsync(IEngineEnvironmentSettings environmentSettings, ITemplateConstraintFactory factory, CancellationToken cancellationToken)
            {
                (NuGetVersionSpecification currentSdkVersion, IEnumerable<NuGetVersionSpecification> installedVersions, Func<IReadOnlyList<string>, IReadOnlyList<string>, string> remedySuggestionFactory) =
                    await ExtractInstalledSdkVersionAsync(
                        environmentSettings.Components.OfType<ISdkInfoProvider>(),
                        cancellationToken).ConfigureAwait(false);
                return new SdkVersionConstraint(environmentSettings, factory, currentSdkVersion, installedVersions, remedySuggestionFactory);
            }

            protected override TemplateConstraintResult EvaluateInternal(string? args)
            {
                IReadOnlyList<IVersionSpecification> supportedSdks = ParseArgs(args).ToList();

                foreach (IVersionSpecification supportedSdk in supportedSdks)
                {
                    if (supportedSdk.CheckIfVersionIsValid(_currentSdkVersion.ToString()))
                    {
                        return TemplateConstraintResult.CreateAllowed(this);
                    }
                }

                string cta = _remedySuggestionFactory(
                    VersionSpecificationsToStrings(supportedSdks),
                    VersionSpecificationsToStrings(_installedSdkVersion.Where(installed =>
                        supportedSdks.Any(supported => supported.CheckIfVersionIsValid(installed.ToString())))));

                return TemplateConstraintResult.CreateRestricted(
                    this,
                    string.Format(LocalizableStrings.SdkConstraint_Message_Restricted, _currentSdkVersion.ToString(), supportedSdks.ToCsvString()),
                    cta);
            }

            private static IReadOnlyList<string> VersionSpecificationsToStrings(IEnumerable<IVersionSpecification> versions)
            {
                return versions.Select(v => v.ToString()).ToList();
            }

            //supported configuration:
            // "args": "[7-*]"  // single semver nuget compatible version expression
            // "args": [ "5.0.100", "6.0.100" ] // multiple version expression - all expressing supported versions
            private static IEnumerable<IVersionSpecification> ParseArgs(string? args)
            {
                return args.ParseArrayOfConstraintStrings().Select(Extensions.ParseVersionSpecification);
            }

            private static async
                Task<(NuGetVersionSpecification CurrentSdkVersion, IEnumerable<NuGetVersionSpecification> InstalledVersions, Func<IReadOnlyList<string>, IReadOnlyList<string>, string> RemedySuggestionFactory)>
                ExtractInstalledSdkVersionAsync(IEnumerable<ISdkInfoProvider> sdkInfoProviders, CancellationToken cancellationToken)
            {
                List<ISdkInfoProvider> providers = sdkInfoProviders.ToList();

                if (providers.Count == 0)
                {
                    throw new ConfigurationException(LocalizableStrings.SdkConstraint_Error_MissingProvider);
                }

                if (providers.Count > 1)
                {
                    throw new ConfigurationException(
                        string.Format(LocalizableStrings.SdkConstraint_Error_MismatchedProviders, providers.Select(p => p.Id).ToCsvString()));
                }

                cancellationToken.ThrowIfCancellationRequested();
                string version = await providers[0].GetCurrentVersionAsync(cancellationToken).ConfigureAwait(false);
                NuGetVersionSpecification currentSdkVersion = ParseVersion(version);

                cancellationToken.ThrowIfCancellationRequested();
                IEnumerable<NuGetVersionSpecification> versions = (await providers[0].GetInstalledVersionsAsync(cancellationToken).ConfigureAwait(false)).Select(ParseVersion);

                return (currentSdkVersion, versions, providers[0].ProvideConstraintRemedySuggestion);
            }

            private static NuGetVersionSpecification ParseVersion(string version)
            {
                if (!NuGetVersionSpecification.TryParse(version, out NuGetVersionSpecification? sdkVersion))
                {
                    throw new ConfigurationException(string.Format(LocalizableStrings.SdkConstraint_Error_InvalidVersion, version));
                }

                return sdkVersion!;
            }
        }
    }
}
