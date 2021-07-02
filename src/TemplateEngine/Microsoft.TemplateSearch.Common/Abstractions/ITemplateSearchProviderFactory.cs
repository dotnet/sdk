// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateSearch.Common.Abstractions
{
    public interface ITemplateSearchProviderFactory : IIdentifiedComponent
    {
        string DisplayName { get; }

        ITemplateSearchProvider CreateProvider(IEngineEnvironmentSettings environmentSettings, IReadOnlyDictionary<string, Func<object, object>> additionalDataReaders);
    }
}
