// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateSearch.Common
{
    public class NupkgHigherVersionInstalledPackFilter : ISearchPackFilter
    {
        private readonly IReadOnlyList<IManagedTemplatePackage> _existingTemplatePackage;
        private IReadOnlyDictionary<string, string> _existingTemplatePackageFilterData;
        private bool _isInitialized;

        public NupkgHigherVersionInstalledPackFilter(IReadOnlyList<IManagedTemplatePackage> existingInstallDecriptors)
        {
            _existingTemplatePackage = existingInstallDecriptors;
            _isInitialized = false;
        }

        public bool ShouldPackBeFiltered(string candidatePackName, string candidatePackVersion)
        {
            EnsureInitialized();

            if (!_existingTemplatePackageFilterData.TryGetValue(candidatePackName, out string existingPackVersion))
            {
                // no existing install of this pack - don't filter it
                return false;
            }

            return existingPackVersion != candidatePackVersion;
        }

        private void EnsureInitialized()
        {
            if (_isInitialized)
            {
                return;
            }

            Dictionary<string, string> filterData = new Dictionary<string, string>();

            foreach (IManagedTemplatePackage descriptor in _existingTemplatePackage)
            {
                filterData[descriptor.Identifier] = descriptor.Version;
            }

            _existingTemplatePackageFilterData = filterData;

            _isInitialized = true;
        }
    }
}
