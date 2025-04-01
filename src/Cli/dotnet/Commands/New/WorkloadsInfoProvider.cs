// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Workloads.Workload.List;
using Microsoft.TemplateEngine.Abstractions.Components;

namespace Microsoft.DotNet.Tools.New;

internal class WorkloadsInfoProvider(Lazy<IWorkloadsRepositoryEnumerator> workloadsRepositoryEnumerator) : IWorkloadsInfoProvider
{
    private readonly Lazy<IWorkloadsRepositoryEnumerator> _workloadsRepositoryEnumerator = workloadsRepositoryEnumerator;
    public Guid Id { get; } = Guid.Parse("{F8BA5B13-7BD6-47C8-838C-66626526817B}");

    public Task<IEnumerable<WorkloadInfo>> GetInstalledWorkloadsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(
            _workloadsRepositoryEnumerator.Value.InstalledAndExtendedWorkloads.Select(w => new WorkloadInfo(w.Id, w.Description))
            );
    }

    public string ProvideConstraintRemedySuggestion(IReadOnlyList<string> supportedWorkloads) => LocalizableStrings.WorkloadInfoProvider_Message_AddWorkloads;
}
