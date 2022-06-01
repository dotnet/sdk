// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.Abstractions.Components
{
    /// <summary>
    /// Provider of SDK installation info.
    /// </summary>
    public interface ISdkInfoProvider : IIdentifiedComponent
    {
        /// <summary>
        /// Current SDK installation semver version string.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>SDK version.</returns>
        public Task<string> GetCurrentVersionAsync(CancellationToken cancellationToken);

        /// <summary>
        /// All installed SDK installations semver version strings.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>SDK version strings.</returns>
        public Task<IEnumerable<string>> GetInstalledVersionsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Provides localized suggestion on action to be taken so that constraints requiring specified workloads can be met.
        /// This should be specific for current host (e.g. action to be taken for VS will differ from CLI host action.)
        /// This method should not perform any heavy processing (external services or file system queries) - as it's being
        ///   synchronously executed as part of constraint evaluation.
        /// </summary>
        /// <param name="supportedVersions">SDK versions required by a constraint (in an 'OR' relationship).</param>
        /// <param name="viableInstalledVersions">SDK versions installed, that can meet the constraint - instructions should be provided to switch to any of those.</param>
        /// <returns>Localized string with remedy suggestion specific to current host.</returns>
        public string ProvideConstraintRemedySuggestion(IReadOnlyList<string> supportedVersions, IReadOnlyList<string> viableInstalledVersions);
    }
}
