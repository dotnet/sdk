using NuGet.RuntimeModel;

namespace Microsoft.NET.Build.Containers;

internal interface IManifestPicker
{
    public PlatformSpecificManifest? PickBestManifestForRid(IReadOnlyDictionary<string, PlatformSpecificManifest> manifestList, string runtimeIdentifier);
    public PlatformSpecificOciManifest? PickBestManifestForRid(IReadOnlyDictionary<string, PlatformSpecificOciManifest> manifestList, string runtimeIdentifier);
}

internal sealed class RidGraphManifestPicker : IManifestPicker
{
    private readonly RuntimeGraph _runtimeGraph;

    public RidGraphManifestPicker(string runtimeIdentifierGraphPath)
    {
        _runtimeGraph = GetRuntimeGraphForDotNet(runtimeIdentifierGraphPath);
    }
    public PlatformSpecificManifest? PickBestManifestForRid(IReadOnlyDictionary<string, PlatformSpecificManifest> ridManifestDict, string runtimeIdentifier)
    {
        var bestManifestRid = GetBestMatchingRid(_runtimeGraph, runtimeIdentifier, ridManifestDict.Keys);
        if (bestManifestRid is null)
        {
            return null;
        }
        return ridManifestDict[bestManifestRid];
    }

    public PlatformSpecificOciManifest? PickBestManifestForRid(IReadOnlyDictionary<string, PlatformSpecificOciManifest> ridManifestDict, string runtimeIdentifier)
    {
        var bestManifestRid = GetBestMatchingRid(_runtimeGraph, runtimeIdentifier, ridManifestDict.Keys);
        if (bestManifestRid is null)
        {
            return null;
        }
        return ridManifestDict[bestManifestRid];
    }

    private static string? GetBestMatchingRid(RuntimeGraph runtimeGraph, string runtimeIdentifier, IEnumerable<string> availableRuntimeIdentifiers)
    {
        HashSet<string> availableRids = new HashSet<string>(availableRuntimeIdentifiers, StringComparer.Ordinal);
        foreach (var candidateRuntimeIdentifier in runtimeGraph.ExpandRuntime(runtimeIdentifier))
        {
            if (availableRids.Contains(candidateRuntimeIdentifier))
            {
                return candidateRuntimeIdentifier;
            }
        }

        return null;
    }

    private static RuntimeGraph GetRuntimeGraphForDotNet(string ridGraphPath) => JsonRuntimeFormat.ReadRuntimeGraph(ridGraphPath);
}

public static class RidMapping
{
    public static IReadOnlyDictionary<string, PlatformSpecificManifest> GetManifestsByRid(PlatformSpecificManifest[] manifestList)
    {
        var ridDict = new Dictionary<string, PlatformSpecificManifest>();
        foreach (var manifest in manifestList)
        {
            if (CreateRidForPlatform(manifest.platform) is { } rid)
            {
                ridDict.TryAdd(rid, manifest);
            }
        }

        return ridDict;
    }

    public static IReadOnlyDictionary<string, PlatformSpecificOciManifest> GetManifestsByRid(PlatformSpecificOciManifest[] manifestList)
    {
        var ridDict = new Dictionary<string, PlatformSpecificOciManifest>();
        foreach (var manifest in manifestList)
        {
            if (manifest.platform is PlatformInformation platformInfo && CreateRidForPlatform(platformInfo) is { } rid)
            {
                ridDict.TryAdd(rid, manifest);
            }
        }

        return ridDict;
    }

    public static string? CreateRidForPlatform(PlatformInformation platform)
    {
        // we only support linux and windows containers explicitly, so anything else we should skip past.
        var osPart = platform.os switch
        {
            "linux" => "linux",
            "windows" => "win",
            _ => null
        };
        // TODO: this part needs a lot of work, the RID graph isn't super precise here and version numbers (especially on windows) are _whack_
        // TODO: we _may_ need OS-specific version parsing. Need to do more research on what the field looks like across more manifest lists.
        var versionPart = platform.osVersion?.Split('.') switch
        {
        [var major, ..] => major,
            _ => null
        };
        var platformPart = platform.architecture switch
        {
            "amd64" => "x64",
            "x386" => "x86",
            "arm" => $"arm{(platform.variant != "v7" ? platform.variant : "")}",
            "arm64" => "arm64",
            "ppc64le" => "ppc64le",
            "s390x" => "s390x",
            "riscv64" => "riscv64",
            "loongarch64" => "loongarch64",
            _ => null
        };

        if (osPart is null || platformPart is null) return null;
        return $"{osPart}{versionPart ?? ""}-{platformPart}";
    }

}
