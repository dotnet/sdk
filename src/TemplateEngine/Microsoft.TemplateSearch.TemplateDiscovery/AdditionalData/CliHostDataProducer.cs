// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli;
using Microsoft.TemplateSearch.TemplateDiscovery.PackProviders;
using Newtonsoft.Json;

namespace Microsoft.TemplateSearch.TemplateDiscovery.AdditionalData
{
    internal class CliHostDataProducer : IAdditionalDataProducer
    {
        private const string CliHostDataName = "cliHostData";

        private Dictionary<string, HostSpecificTemplateData> _hostDataForPackByTemplate = new Dictionary<string, HostSpecificTemplateData>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<IPackInfo, Dictionary<string, HostSpecificTemplateData>> _hostDataForPack = new Dictionary<IPackInfo, Dictionary<string, HostSpecificTemplateData>>(new IPackInfoComparer());

        internal CliHostDataProducer()
        {
            _hostDataForPackByTemplate = new Dictionary<string, HostSpecificTemplateData>();
        }

        public string DataUniqueName => CliHostDataName;

        public string Serialized => JsonConvert.SerializeObject(_hostDataForPackByTemplate, Formatting.Indented);

        public object? Data => _hostDataForPackByTemplate;

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

        public object? GetDataForPack(IPackInfo packInfo)
        {
            return null;
        }

        public object? GetDataForTemplate(IPackInfo packInfo, string templateIdentity)
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

        private class IPackInfoComparer : IEqualityComparer<IPackInfo>
        {
            public bool Equals(IPackInfo? x, IPackInfo? y)
            {
                if (x == null && y == null)
                {
                    return true;
                }

                if (x == null || y == null)
                {
                    return false;
                }

                return x.Id.Equals(y.Id, StringComparison.OrdinalIgnoreCase) && x.Version.Equals(y.Version, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode([DisallowNull] IPackInfo obj)
            {
                return new
                {
                    a = obj.Version.ToLowerInvariant(),
                    b = obj.Id.ToLowerInvariant()
                }.GetHashCode();
            }
        }
    }
}
