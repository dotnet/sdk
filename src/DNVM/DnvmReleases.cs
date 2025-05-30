
using System.Collections.Generic;
using Serde;
using static Dnvm.DnvmReleases;

namespace Dnvm;

[GenerateSerde]
public partial record DnvmReleases(Release LatestVersion)
{
    public Release? LatestPreview { get; init; }

    [GenerateSerde]
    public partial record Release(
        string Version,
        Dictionary<string, string> Artifacts);
}
