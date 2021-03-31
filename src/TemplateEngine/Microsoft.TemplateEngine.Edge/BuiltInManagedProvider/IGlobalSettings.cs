// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions.Installer;

namespace Microsoft.TemplateEngine.Edge.BuiltInManagedProvider
{
    /// <summary>
    /// Interface that represents loading/storing data into settings.json file.
    /// That is shared between multiple different hosts of TemplateEngine.
    /// </summary>
    internal interface IGlobalSettings
    {
        /// <summary>
        /// Triggered every time when settings change.
        /// </summary>
        event Action SettingsChanged;

        /// <summary>
        /// Returns uncached list of the template packages.
        /// </summary>
        Task<IReadOnlyList<TemplatePackageData>> GetInstalledTemplatePackagesAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Stores list of the template packages.
        /// </summary>
        Task SetInstalledTemplatePackagesAsync(IReadOnlyList<TemplatePackageData> packages, CancellationToken cancellationToken);

        /// <summary>
        /// This method must be called before making any modifications to settings to ensure other processes on system
        /// don't override or lose changes done by this process.
        /// </summary>
        /// <returns><see cref="IDisposable"/> object that needs to be disposed once modifying of settings is finished.</returns>
        Task<IDisposable> LockAsync(CancellationToken token);
    }
}
