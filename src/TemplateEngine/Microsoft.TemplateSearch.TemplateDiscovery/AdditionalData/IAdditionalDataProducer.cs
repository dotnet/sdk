using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateSearch.TemplateDiscovery.PackProviders;

namespace Microsoft.TemplateSearch.TemplateDiscovery.AdditionalData
{
    public interface IAdditionalDataProducer
    {
        string DataUniqueName { get; }

        void CreateDataForTemplatePack(IDownloadedPackInfo packInfo, IReadOnlyList<ITemplateInfo> templates, IEngineEnvironmentSettings environment);

        string Serialized { get; }

        object Data { get; }
    }
}
