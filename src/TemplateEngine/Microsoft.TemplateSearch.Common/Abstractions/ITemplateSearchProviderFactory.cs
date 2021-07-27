// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateSearch.Common.Abstractions
{
    /// <summary>
    /// The factory to create <see cref="ITemplateSearchProvider"/>s.
    /// This is a template engine component that can be installed for <see cref="ITemplateEngineHost"/>.
    /// </summary>
    public interface ITemplateSearchProviderFactory : IIdentifiedComponent
    {
        /// <summary>
        /// Gets the display name of the component.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Creates <see cref="ITemplateSearchProvider"/>.
        /// </summary>
        /// <param name="environmentSettings">template engine environment settings.</param>
        /// <param name="additionalDataReaders">additional readers to be used to read the information from the source (optional).</param>
        /// <returns>Created <see cref="ITemplateSearchProvider"/>.</returns>
        ITemplateSearchProvider CreateProvider(IEngineEnvironmentSettings environmentSettings, IReadOnlyDictionary<string, Func<object, object>> additionalDataReaders);
    }
}
