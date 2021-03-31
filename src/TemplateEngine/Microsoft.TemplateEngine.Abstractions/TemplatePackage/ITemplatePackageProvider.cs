// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.Abstractions.TemplatePackage
{
    /// <summary>
    /// Provides set of <see cref="ITemplatePackage"/>s available to the host.
    /// </summary>
    public interface ITemplatePackageProvider
    {
        /// <summary>
        /// Raised when template packages have been changed. Indicates that caller should refresh the list of template packages in use.
        /// </summary>
        event Action TemplatePackagesChanged;

        /// <summary>
        /// Gets <see cref="ITemplatePackageProviderFactory"/> that created the provider.
        /// </summary>
        ITemplatePackageProviderFactory Factory { get; }

        /// <summary>
        /// Gets the list of template packages available for the provider.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>The list of <see cref="ITemplatePackage"/>s.</returns>
        Task<IReadOnlyList<ITemplatePackage>> GetAllTemplatePackagesAsync(CancellationToken cancellationToken);
    }
}
