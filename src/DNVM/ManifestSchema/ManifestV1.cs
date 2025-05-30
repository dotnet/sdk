
using System.Collections.Immutable;
using Serde;

namespace Dnvm;

[GenerateDeserialize]
internal sealed partial record ManifestV1
{
    public ImmutableArray<Workload> Workloads { get; init; } = ImmutableArray<Workload>.Empty;

    [GenerateSerde]
    internal sealed partial record Workload(string Version);
}