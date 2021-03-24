using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;
using Microsoft.TemplateSearch.Common;
using Microsoft.TemplateSearch.TemplateDiscovery.AdditionalData;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking.Reporting;
using Microsoft.TemplateSearch.TemplateDiscovery.PackProviders;

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackChecking
{
    public class PackChecker
    {
        private static readonly string HostIdentifierBase = "dotnetcli-discovery-";

        public PackChecker()
        {
        }

        public PackCheckResult TryGetTemplatesInPack(IDownloadedPackInfo packInfo, IReadOnlyList<IAdditionalDataProducer> additionalDataProducers, HashSet<string> alreadySeenTemplateIdentities, bool persistHive = false)
        {
            ITemplateEngineHost host = CreateHost(packInfo);
            EngineEnvironmentSettings environment = new EngineEnvironmentSettings(host, x => new SettingsLoader(x));
            PackCheckResult checkResult;

            try
            {
                if (TryInstallPackage(packInfo.Path, environment, out IReadOnlyList<ITemplateInfo> installedTemplates))
                {
                    IReadOnlyList<ITemplateInfo> filteredInstalledTemplates = installedTemplates.Where(t => !alreadySeenTemplateIdentities.Contains(t.Identity)).ToList();
                    checkResult = new PackCheckResult(packInfo, filteredInstalledTemplates);
                    ProduceAdditionalDataForPack(additionalDataProducers, checkResult, environment);
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

            if (!persistHive)
            {
                TryCleanup(environment);
            }

            return checkResult;
        }

        private void ProduceAdditionalDataForPack(IReadOnlyList<IAdditionalDataProducer> additionalDataProducers, PackCheckResult packCheckResult, EngineEnvironmentSettings environment)
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

        private bool TryInstallPackage(string packageFile, EngineEnvironmentSettings environment, out IReadOnlyList<ITemplateInfo> installedTemplates)
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

        private static ITemplateEngineHost CreateHost(IDownloadedPackInfo packInfo)
        {
            string hostIdentifier = HostIdentifierBase + packInfo.Id;

            ITemplateEngineHost host = TemplateEngineHostHelper.CreateHost(hostIdentifier);

            return host;
        }

        private void TryCleanup(EngineEnvironmentSettings environment)
        {
            Paths paths = new Paths(environment);

            try
            {
                paths.Delete(paths.User.BaseDir);
            }
            catch
            {
                // do nothing.
            }

            // remove the temporary hive
            string hiveDir = Directory.GetParent(paths.User.BaseDir).FullName;
            try
            {
                paths.Delete(hiveDir);
            }
            catch
            {
                // do nothing.
            }
        }
    }
}
