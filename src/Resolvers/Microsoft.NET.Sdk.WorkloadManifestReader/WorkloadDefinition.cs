// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
#if INTERNALIZE_SHARED_TYPES
    internal
#else
    public
#endif
    abstract class BaseWorkloadDefinition
    {
        public BaseWorkloadDefinition(WorkloadId id)
        {
            Id = id;
        }

        public WorkloadId Id { get; }
    }

#if INTERNALIZE_SHARED_TYPES
    internal
#else
    public
#endif
    class WorkloadDefinition : BaseWorkloadDefinition
    {
        public WorkloadDefinition(
            WorkloadId id, bool isAbstract, string? description, WorkloadDefinitionKind kind, List<WorkloadId>? extends,
            List<WorkloadPackId>? packs, List<string>? platforms
            ) : base(id)
        {
            IsAbstract = isAbstract;
            Description = description;
            Kind = kind;
            Extends = extends;
            Packs = packs;
            Platforms = platforms;
        }

        public bool IsAbstract { get; }
        public string? Description { get; }
        public WorkloadDefinitionKind Kind { get; }
        public List<WorkloadId>? Extends { get; }
        public List<WorkloadPackId>? Packs { get; }
        public List<string>? Platforms { get; }
    }

#if INTERNALIZE_SHARED_TYPES
    internal
#else
    public
#endif
    enum WorkloadDefinitionKind
    {
        Dev,
        Build
    }

#if INTERNALIZE_SHARED_TYPES
    internal
#else
    public
#endif
    class WorkloadRedirect : BaseWorkloadDefinition
    {
        public WorkloadRedirect(WorkloadId id, WorkloadId replaceWith) : base(id)
        {
            ReplaceWith = replaceWith;
        }

        public WorkloadId ReplaceWith { get; }
    }
}
