// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli;
using Microsoft.TemplateSearch.Common.Abstractions;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking;

namespace Microsoft.TemplateSearch.TemplateDiscovery.AdditionalData
{
    internal class CliHostDataProducer : IAdditionalDataProducer
    {
        private const string CliHostDataName = "cliHostData";

        private Dictionary<string, HostSpecificTemplateData> _hostDataForPackByTemplate = new(StringComparer.OrdinalIgnoreCase);

        private Dictionary<ITemplatePackageInfo, Dictionary<string, HostSpecificTemplateData>> _hostDataForPack = new(new ITemplatePackageInfoComparer());

        internal CliHostDataProducer()
        {
            _hostDataForPackByTemplate = new Dictionary<string, HostSpecificTemplateData>();
        }

        public string DataUniqueName => CliHostDataName;

        public object? Data => _hostDataForPackByTemplate;

        public object? CreateDataForTemplate(ITemplateInfo template, IEngineEnvironmentSettings environment)
        {
            IHostSpecificDataLoader hostDataLoader = new HostSpecificDataLoader(environment);
            HostSpecificTemplateData hostData = hostDataLoader.ReadHostSpecificTemplateData(template);
            // store the host data if it has any info that could affect searching for this template.
            if (hostData.IsHidden || hostData.SymbolInfo.Count > 0)
            {
                return hostData;
            }
            return null;
        }

        public void CreateDataForTemplatePack(IDownloadedPackInfo packInfo, IReadOnlyList<ITemplateInfo> templateList, IEngineEnvironmentSettings environment)
        {
            IHostSpecificDataLoader hostDataLoader = new HostSpecificDataLoader(environment);
            Dictionary<string, HostSpecificTemplateData> dataForPack = new Dictionary<string, HostSpecificTemplateData>(StringComparer.OrdinalIgnoreCase);

            foreach (ITemplateInfo template in templateList)
            {
                HostSpecificTemplateData hostData = hostDataLoader.ReadHostSpecificTemplateData(template);

                // store the host data if it has any info that could affect searching for this template.
                if (hostData.IsHidden || hostData.SymbolInfo.Count > 0)
                {
                    _hostDataForPackByTemplate[template.Identity] = hostData;
                    dataForPack[template.Identity] = hostData;
                }
            }
            _hostDataForPack[packInfo] = dataForPack;
        }

        public object? CreateDataForTemplatePackage(ITemplatePackageInfo packInfo) => null;

        public object? GetDataForPack(ITemplatePackageInfo packInfo)
        {
            return null;
        }

        public object? GetDataForTemplate(ITemplatePackageInfo packInfo, string templateIdentity)
        {
            if (!_hostDataForPack.TryGetValue(packInfo, out Dictionary<string, HostSpecificTemplateData>? packData))
            {
                return null;
            }
            if (!packData.TryGetValue(templateIdentity, out HostSpecificTemplateData? data))
            {
                return null;
            }
            return data;
        }

        private class ITemplatePackageInfoComparer : IEqualityComparer<ITemplatePackageInfo>
        {
            public bool Equals(ITemplatePackageInfo? x, ITemplatePackageInfo? y)
            {
                if (x == null && y == null)
                {
                    return true;
                }

                if (x == null || y == null)
                {
                    return false;
                }

                return x.Name.Equals(y.Name, StringComparison.OrdinalIgnoreCase)
                    && (x.Version?.Equals(y.Version, StringComparison.OrdinalIgnoreCase) ?? x.Version == y.Version);
            }

            public int GetHashCode([DisallowNull] ITemplatePackageInfo obj)
            {
                return new
                {
                    a = obj.Version?.ToLowerInvariant(),
                    b = obj.Name.ToLowerInvariant()
                }.GetHashCode();
            }
        }
    }
}
