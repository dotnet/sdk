// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Abstractions.Components
{
    /// <summary>
    /// Provider of descriptors of SDK workloads available to particular host (that is usually providing this component).
    /// </summary>
    public interface IWorkloadsInfoProvider : IIdentifiedComponent
    {
        /// <summary>
        /// Fetches set of installed workloads.
        /// </summary>
        /// <param name="token"></param>
        /// <returns>Set of installed workloads.</returns>
        Task<IEnumerable<WorkloadInfo>> GetInstalledWorkloadsAsync(CancellationToken token);

        /// <summary>
        /// Provides localized suggestion on action to be taken so that constraints requiring specified workloads can be met.
        /// This should be specific for current host (e.g. action to be taken for VS will differ from CLI host action.)
        /// This method should not perform any heavy processing (external services or file system queries) - as it's being
        ///   synchronously executed as part of constraint evaluation.
        /// </summary>
        /// <param name="supportedWorkloads">Workloads required by a constraint (in an 'OR' relationship).</param>
        /// <returns>Localized string with remedy suggestion specific to current host.</returns>
        string ProvideConstraintRemedySuggestion(IReadOnlyList<string> supportedWorkloads);
    }
}
