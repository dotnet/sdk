﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using NuGet.Versioning;

namespace Microsoft.DotNet.Configurer
{
    public class DotnetFirstTimeUseConfigurer
    {
        private readonly IReporter _reporter;
        private readonly DotnetFirstRunConfiguration _dotnetFirstRunConfiguration;
        private readonly IFirstTimeUseNoticeSentinel _firstTimeUseNoticeSentinel;
        private readonly IAspNetCertificateSentinel _aspNetCertificateSentinel;
        private readonly IAspNetCoreCertificateGenerator _aspNetCoreCertificateGenerator;
        private readonly IFileSentinel _toolPathSentinel;
        private readonly IEnvironmentPath _pathAdder;
        private readonly Dictionary<string, double>? _performanceMeasurements;
        private readonly bool _skipFirstTimeUseCheck;

        public DotnetFirstTimeUseConfigurer(
            IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel,
            IAspNetCertificateSentinel aspNetCertificateSentinel,
            IAspNetCoreCertificateGenerator aspNetCoreCertificateGenerator,
            IFileSentinel toolPathSentinel,
            DotnetFirstRunConfiguration dotnetFirstRunConfiguration,
            IReporter reporter,
            IEnvironmentPath pathAdder,
            Dictionary<string, double>? performanceMeasurements = null,
            bool skipFirstTimeUseCheck = false)
        {
            _firstTimeUseNoticeSentinel = firstTimeUseNoticeSentinel;
            _aspNetCertificateSentinel = aspNetCertificateSentinel;
            _aspNetCoreCertificateGenerator = aspNetCoreCertificateGenerator;
            _toolPathSentinel = toolPathSentinel;
            _dotnetFirstRunConfiguration = dotnetFirstRunConfiguration;
            _reporter = reporter;
            _pathAdder = pathAdder ?? throw new ArgumentNullException(nameof(pathAdder));
            _performanceMeasurements ??= performanceMeasurements;
            _skipFirstTimeUseCheck = skipFirstTimeUseCheck;
        }

        public void Configure()
        {
            if (_dotnetFirstRunConfiguration.AddGlobalToolsToPath && !_toolPathSentinel.Exists())
            {
                using (new PerformanceMeasurement(_performanceMeasurements, "AddPackageExecutablePath Time"))
                {
                    _pathAdder.AddPackageExecutablePathToUserPath();
                    _toolPathSentinel.Create();
                }
            }

            var isFirstTimeUse = !_skipFirstTimeUseCheck && !_firstTimeUseNoticeSentinel.Exists();
            var canShowFirstUseMessages = isFirstTimeUse && !_dotnetFirstRunConfiguration.NoLogo;
            if (isFirstTimeUse)
            {
                using (new PerformanceMeasurement(_performanceMeasurements, "FirstTimeUseNotice Time"))
                {
                    // Migrate the NuGet state from earlier SDKs
                    NuGet.Common.Migrations.MigrationRunner.Run();

                    if (canShowFirstUseMessages)
                    {
                        _reporter.WriteLine();
                        string productVersion = Product.Version;
                        _reporter.WriteLine(string.Format(LocalizableStrings.FirstTimeMessageWelcome, ParseDotNetVersion(productVersion), productVersion));

                        if (!_dotnetFirstRunConfiguration.TelemetryOptout)
                        {
                            _reporter.WriteLine();
                            _reporter.WriteLine(LocalizableStrings.TelemetryMessage);
                        }
                    }

                    _firstTimeUseNoticeSentinel.CreateIfNotExists();
                }
            }

            if (CanGenerateAspNetCertificate())
            {
                using (new PerformanceMeasurement(_performanceMeasurements, "GenerateAspNetCertificate Time"))
                {
                    _aspNetCoreCertificateGenerator.GenerateAspNetCoreDevelopmentCertificate();
                    _aspNetCertificateSentinel.CreateIfNotExists();

                    if (canShowFirstUseMessages)
                    {
                        // This message is slightly misleading for (e.g.) FreeBSD, which doesn't officially
                        // support `dotnet dev-certs https --trust`, but the link in the message should help
                        // users find the right steps for their platform.
                        _reporter.WriteLine();
                        _reporter.WriteLine(LocalizableStrings.FirstTimeMessageAspNetCertificate);
                    }
                }
            }

            if (canShowFirstUseMessages)
            {
                _reporter.WriteLine();
                _reporter.WriteLine(LocalizableStrings.FirstTimeMessageMoreInformation);
            }
        }

        private bool CanGenerateAspNetCertificate() =>
#if EXCLUDE_ASPNETCORE
            false;
#else
            _dotnetFirstRunConfiguration.GenerateAspNetCertificate && !_aspNetCertificateSentinel.Exists();
#endif

        internal static string ParseDotNetVersion(string productVersion) =>
            NuGetVersion.TryParse(productVersion, out var parsedVersion) ? $"{parsedVersion.Major}.{parsedVersion.Minor}" : string.Empty;
    }
}
