// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateSearch.TemplateDiscovery.AdditionalData;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking.Reporting;
using Microsoft.TemplateSearch.TemplateDiscovery.PackProviders;

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackChecking
{
    public class PackChecker
    {
        private const string HostIdentifierBase = "dotnetcli-discovery-";

        public PackChecker()
        {
        }

        public PackCheckResult TryGetTemplatesInPack(IDownloadedPackInfo packInfo, IReadOnlyList<IAdditionalDataProducer> additionalDataProducers, HashSet<string> alreadySeenTemplateIdentities)
        {
            ITemplateEngineHost host = CreateHost(packInfo);
            using EngineEnvironmentSettings environmentSettings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            PackCheckResult checkResult;

            try
            {
                if (TryInstallPackage(packInfo.Path, environmentSettings, out IReadOnlyList<ITemplateInfo> installedTemplates))
                {
                    IReadOnlyList<ITemplateInfo> filteredInstalledTemplates = installedTemplates.Where(t => !alreadySeenTemplateIdentities.Contains(t.Identity)).ToList();
                    checkResult = new PackCheckResult(packInfo, filteredInstalledTemplates);
                    ProduceAdditionalDataForPack(additionalDataProducers, checkResult, environmentSettings);
                }
                else
                {
                    IReadOnlyList<ITemplateInfo> foundTemplates = new List<ITemplateInfo>();
                    checkResult = new PackCheckResult(packInfo, foundTemplates);
                }
            }
            catch
            {
                IReadOnlyList<ITemplateInfo> foundTemplates = new List<ITemplateInfo>();
                checkResult = new PackCheckResult(packInfo, foundTemplates);
            }
            return checkResult;
        }

        private static ITemplateEngineHost CreateHost(IDownloadedPackInfo packInfo)
        {
            string hostIdentifier = HostIdentifierBase + packInfo.Id;

            ITemplateEngineHost host = TemplateEngineHostHelper.CreateHost(hostIdentifier);

            return host;
        }

        private void ProduceAdditionalDataForPack(IReadOnlyList<IAdditionalDataProducer> additionalDataProducers, PackCheckResult packCheckResult, IEngineEnvironmentSettings environment)
        {
            if (!packCheckResult.AnyTemplates)
            {
                return;
            }

            foreach (IAdditionalDataProducer dataProducer in additionalDataProducers)
            {
                dataProducer.CreateDataForTemplatePack(packCheckResult.PackInfo, packCheckResult.FoundTemplates, environment);
            }
        }

        private bool TryInstallPackage(string packageFile, IEngineEnvironmentSettings environment, out IReadOnlyList<ITemplateInfo> installedTemplates)
        {
            var scanner = new Scanner(environment);
            var scanResult = scanner.Scan(packageFile);

            if (scanResult.Templates.Count > 0)
            {
                installedTemplates = scanResult.Templates;
            }
            else
            {
                installedTemplates = new List<ITemplateInfo>();
            }

            return installedTemplates.Count > 0;
        }
    }
}
