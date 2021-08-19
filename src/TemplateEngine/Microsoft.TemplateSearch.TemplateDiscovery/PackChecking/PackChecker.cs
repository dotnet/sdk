// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateSearch.TemplateDiscovery.AdditionalData;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking.Reporting;
using Microsoft.TemplateSearch.TemplateDiscovery.PackProviders;

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackChecking
{
    internal class PackChecker
    {
        private const string HostIdentifierBase = "dotnetcli-discovery-";

        internal PackCheckResult TryGetTemplatesInPack(IDownloadedPackInfo packInfo, IReadOnlyList<IAdditionalDataProducer> additionalDataProducers, HashSet<string> alreadySeenTemplateIdentities)
        {
            ITemplateEngineHost host = TemplateEngineHostHelper.CreateHost(HostIdentifierBase + packInfo.Name);
            EngineEnvironmentSettings environmentSettings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            Scanner scanner = new Scanner(environmentSettings);
            PackCheckResult checkResult;
            try
            {
                using var scanResult = scanner.Scan(packInfo.Path, scanForComponents: false);
                foreach (ITemplateInfo templateInfo in scanResult.Templates.Where(t => alreadySeenTemplateIdentities.Contains(t.Identity)))
                {
                    Verbose.WriteLine($"[{packInfo.Name}::{packInfo.Version}] {templateInfo.ShortNameList[0]}({templateInfo.Name}) is skipped because template with same identity {templateInfo.Identity} was already found in other package.");
                }
                checkResult = new PackCheckResult(packInfo, scanResult.Templates.Where(t => !alreadySeenTemplateIdentities.Contains(t.Identity)).ToList());
                ProduceAdditionalDataForPack(additionalDataProducers, checkResult, environmentSettings);
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to read package {0}::{1}, details: {2}. The package will be skipped.", packInfo.Name, packInfo.Version, ex);
                checkResult = new PackCheckResult(packInfo, Array.Empty<ITemplateInfo>());
            }
            return checkResult;
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
    }
}
