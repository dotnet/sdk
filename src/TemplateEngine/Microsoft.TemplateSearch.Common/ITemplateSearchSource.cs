// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateSearch.Common
{
    public interface ITemplateSearchSource : IIdentifiedComponent
    {
        string DisplayName { get; }

        Task<bool> TryConfigure(IEngineEnvironmentSettings environmentSettings, IReadOnlyList<IManagedTemplatePackage> existingTemplatePackage);

        Task<IReadOnlyList<ITemplateNameSearchResult>> CheckForTemplateNameMatchesAsync(string templateName);

        Task<IReadOnlyDictionary<string, PackToTemplateEntry>> CheckForTemplatePackMatchesAsync(IReadOnlyList<string> packNameList);
    }
}
