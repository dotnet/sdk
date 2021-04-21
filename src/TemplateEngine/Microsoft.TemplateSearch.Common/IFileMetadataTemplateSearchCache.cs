// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateSearch.Common
{
    public interface IFileMetadataTemplateSearchCache
    {
        IReadOnlyDictionary<string, PackToTemplateEntry> GetInfoForNamedPacks(IReadOnlyList<string> packNameList);

        IReadOnlyList<ITemplateInfo> GetNameMatchedTemplates(string searchName);

        IReadOnlyDictionary<string, PackInfo> GetTemplateToPackMapForTemplateIdentities(IReadOnlyList<string> identities);
    }
}
