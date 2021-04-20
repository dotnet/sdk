// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateSearch.Common
{
    public class TemplateNameSearchResult : ITemplateNameSearchResult
    {
        public TemplateNameSearchResult(ITemplateInfo template, PackInfo packInfo)
        {
            Template = template;
            PackInfo = packInfo;
        }

        public ITemplateInfo Template { get; }

        public PackInfo PackInfo { get; }
    }
}
