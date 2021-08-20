// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateSearch.Common.Abstractions;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking;

namespace Microsoft.TemplateSearch.TemplateDiscovery.AdditionalData
{
    internal interface IAdditionalDataProducer
    {
        string DataUniqueName { get; }

        [Obsolete]
        object? Data { get; }

        object? CreateDataForTemplatePackage(ITemplatePackageInfo packInfo);

        object? CreateDataForTemplate(ITemplateInfo templates, IEngineEnvironmentSettings environment);

        [Obsolete]
        void CreateDataForTemplatePack(IDownloadedPackInfo packInfo, IReadOnlyList<ITemplateInfo> templates, IEngineEnvironmentSettings environment);

        [Obsolete]
        object? GetDataForPack(ITemplatePackageInfo packInfo);

        [Obsolete]
        object? GetDataForTemplate(ITemplatePackageInfo packInfo, string templateIdentity);

    }
}
