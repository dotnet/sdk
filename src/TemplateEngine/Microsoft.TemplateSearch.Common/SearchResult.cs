// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateSearch.Common.Abstractions;

namespace Microsoft.TemplateSearch.Common
{
    public class SearchResult
    {
        internal SearchResult(
            ITemplateSearchProvider provider,
            bool success,
            string? errorMessage = null,
            IReadOnlyList<(IPackageInfo, IReadOnlyList<ITemplateInfo>)>? hits = null)
        {
            if (!success && string.IsNullOrWhiteSpace(errorMessage))
            {
                throw new ArgumentException($"{nameof(errorMessage)} cannot be empty when {nameof(success)} is false", nameof(errorMessage));
            }
            if (success && hits == null)
            {
                throw new ArgumentException($"{nameof(hits)} cannot be null when {nameof(success)} is true", nameof(hits));
            }
            Provider = provider;
            Success = success;
            ErrorMessage = errorMessage;
            if (!success)
            {
                SearchHits = new List<(IPackageInfo, IReadOnlyList<ITemplateInfo>)>();
            }
            else
            {
                SearchHits = hits!;
            }
        }

        public ITemplateSearchProvider Provider { get; }

        public bool Success { get; }

        public string? ErrorMessage { get; }

        public IReadOnlyList<(IPackageInfo PackageInfo, IReadOnlyList<ITemplateInfo> MatchedTemplates)> SearchHits { get; }
    }
}
