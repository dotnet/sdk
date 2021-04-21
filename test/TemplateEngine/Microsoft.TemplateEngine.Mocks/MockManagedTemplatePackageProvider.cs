// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockManagedTemplatePackageProvider
        : IManagedTemplatePackageProvider
    {
        public event Action TemplatePackagesChanged
        {
            add { throw new NotSupportedException(); }
            remove { }
        }

        public ITemplatePackageProviderFactory Factory => throw new NotImplementedException();

        public Task<IReadOnlyList<ITemplatePackage>> GetAllTemplatePackagesAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<IReadOnlyList<CheckUpdateResult>> GetLatestVersionsAsync(IEnumerable<IManagedTemplatePackage> managedSources, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<IReadOnlyList<InstallResult>> InstallAsync(IEnumerable<InstallRequest> installRequests, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<IReadOnlyList<UninstallResult>> UninstallAsync(IEnumerable<IManagedTemplatePackage> managedSources, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<IReadOnlyList<UpdateResult>> UpdateAsync(IEnumerable<UpdateRequest> updateRequests, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
