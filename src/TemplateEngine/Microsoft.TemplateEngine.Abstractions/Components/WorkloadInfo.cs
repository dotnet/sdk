// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Abstractions.Components
{
    /// <summary>
    /// SDK workload descriptor.
    /// Analogous to SDK type Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver.WorkloadInfo
    ///  (https://github.com/dotnet/sdk/blob/main/src/Resolvers/Microsoft.NET.Sdk.WorkloadManifestReader/WorkloadResolver.cs#L645).
    /// </summary>
    public class WorkloadInfo
    {
        /// <summary>
        /// Creates new instance of <see cref="WorkloadInfo"/>.
        /// </summary>
        /// <param name="id">Workload identifier.</param>
        /// <param name="description">Workload description string - expected to be localized.</param>
        public WorkloadInfo(string id, string description)
        {
            Id = id;
            Description = description;
        }

        /// <summary>
        /// Workload identifier (from manifest).
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Workload description string - expected to be localized.
        /// </summary>
        public string Description { get; }
    }
}
