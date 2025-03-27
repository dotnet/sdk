// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable
using Microsoft.Build.Framework;
using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class DefineStaticWebAssetEndpoints : Task
{
    [Required]
    public ITaskItem[] CandidateAssets { get; set; }

    public ITaskItem[] ExistingEndpoints { get; set; }

    [Required]
    public ITaskItem[] ContentTypeMappings { get; set; }

    [Output]
    public ITaskItem[] Endpoints { get; set; }

    public override bool Execute()
    {
        var existingEndpointsByAssetFile = CreateEndpointsByAssetFile();
        var contentTypeMappings = ContentTypeMappings.Select(ContentTypeMapping.FromTaskItem).OrderByDescending(m => m.Priority).ToArray();
        var contentTypeProvider = new ContentTypeProvider(contentTypeMappings);
        var endpoints = new List<StaticWebAssetEndpoint>();

        Parallel.For(
            0,
            CandidateAssets.Length,
            () => new ParallelWorker(
                endpoints,
                new List<StaticWebAssetEndpoint>(512),
                CandidateAssets,
                existingEndpointsByAssetFile,
                Log,
                contentTypeProvider),
            static (i, loop, state) => state.Process(i, loop),
            static worker => worker.Finally());

        Endpoints = StaticWebAssetEndpoint.ToTaskItems(endpoints);

        return !Log.HasLoggedErrors;
    }

    private Dictionary<string, HashSet<string>> CreateEndpointsByAssetFile()
    {
        if (ExistingEndpoints != null && ExistingEndpoints.Length > 0)
        {
            Dictionary<string, HashSet<string>> existingEndpointsByAssetFile = new(ExistingEndpoints.Length, OSPath.PathComparer);
            var assets = new HashSet<string>(CandidateAssets.Length, OSPath.PathComparer);
            foreach (var asset in CandidateAssets)
            {
                assets.Add(asset.ItemSpec);
            }

            for (var i = 0; i < ExistingEndpoints.Length; i++)
            {
                var endpointCandidate = ExistingEndpoints[i];
                var assetFile = endpointCandidate.GetMetadata(nameof(StaticWebAssetEndpoint.AssetFile));
                if (!assets.Contains(assetFile))
                {
                    Log.LogMessage(MessageImportance.Low, $"Removing endpoints for asset '{assetFile}' because it no longer exists.");
                    continue;
                }

                if (!existingEndpointsByAssetFile.TryGetValue(assetFile, out var set))
                {
                    set = new HashSet<string>(OSPath.PathComparer);
                    existingEndpointsByAssetFile[assetFile] = set;
                }

                // Add the route
                set.Add(endpointCandidate.ItemSpec);
            }

            return existingEndpointsByAssetFile;
        }

        return null;
    }

    private readonly struct ParallelWorker(
        List<StaticWebAssetEndpoint> collectedEndpoints,
        List<StaticWebAssetEndpoint> currentEndpoints,
        ITaskItem[] candidateAssets,
        Dictionary<string, HashSet<string>> existingEndpointsByAssetFile,
        TaskLoggingHelper log,
        ContentTypeProvider contentTypeProvider)
    {
        public List<StaticWebAssetEndpoint> CollectedEndpoints { get; } = collectedEndpoints;
        public List<StaticWebAssetEndpoint> CurrentEndpoints { get; } = currentEndpoints;
        public ITaskItem[] CandidateAssets { get; } = candidateAssets;
        public Dictionary<string, HashSet<string>> ExistingEndpointsByAssetFile { get; } = existingEndpointsByAssetFile;
        public TaskLoggingHelper Log { get; } = log;
        public ContentTypeProvider ContentTypeProvider { get; } = contentTypeProvider;

        private readonly List<StaticWebAsset.StaticWebAssetResolvedRoute> _resolvedRoutes = new(2);

        private void CreateAnAddEndpoints(
            StaticWebAsset asset,
            string length,
            string lastModified,
            StaticWebAssetGlobMatcher.MatchContext matchContext)
        {
            foreach (var (label, route, values) in _resolvedRoutes)
            {
                var (mimeType, cacheSetting) = ResolveContentType(asset, ContentTypeProvider, matchContext, Log);
                var headers = new StaticWebAssetEndpointResponseHeader[6]
                {
                    new()
                    {
                        Name = "Accept-Ranges",
                        Value = "bytes"
                    },
                    new()
                    {
                        Name = "Content-Length",
                        Value = length,
                    },
                    new()
                    {
                        Name = "Content-Type",
                        Value = mimeType,
                    },
                    new()
                    {
                        Name = "ETag",
                        Value = $"\"{asset.Integrity}\"",
                    },
                    new()
                    {
                        Name = "Last-Modified",
                        Value = lastModified,
                    },
                    default
                };

                if (values.ContainsKey("fingerprint"))
                {
                    // max-age=31536000 is one year in seconds. immutable means that the asset will never change.
                    // max-age is for browsers that do not support immutable.
                    headers[5] = new() { Name = "Cache-Control", Value = "max-age=31536000, immutable" };
                }
                else
                {
                    // Force revalidation on non-fingerprinted assets. We can be more granular here and have rules based on the content type.
                    // These values can later be changed at runtime by modifying the endpoint. For example, it might be safer to cache images
                    // for a longer period of time than scripts or stylesheets.
                    headers[5] = new() { Name = "Cache-Control", Value = !string.IsNullOrEmpty(cacheSetting) ? cacheSetting : "no-cache" };
                }

                var properties = new StaticWebAssetEndpointProperty[values.Count + (values.Count > 0 ? 2 : 1)];
                var i = 0;
                foreach (var value in values)
                {
                    properties[i++] = new StaticWebAssetEndpointProperty { Name = value.Key, Value = value.Value };
                }

                if (values.Count > 0)
                {
                    // If an endpoint has values from its route replaced, we add a label to the endpoint so that it can be easily identified.
                    // The combination of label and list of values should be unique.
                    // In this way, we can identify an endpoint resource.fingerprint.ext by its label (for example resource.ext) and its values
                    // (fingerprint).
                    properties[i++] = new StaticWebAssetEndpointProperty { Name = "label", Value = label };
                }

                // We append the integrity in the format expected by the browser so that it can be opaque to the runtime.
                // If in the future we change it to sha384 or sha512, the runtime will not need to be updated.
                properties[i++] = new StaticWebAssetEndpointProperty { Name = "integrity", Value = $"sha256-{asset.Integrity}" };

                var finalRoute = asset.IsProject() || asset.IsPackage() ? StaticWebAsset.Normalize(Path.Combine(asset.BasePath, route)) : route;

                var endpoint = new StaticWebAssetEndpoint()
                {
                    Route = finalRoute,
                    AssetFile = asset.Identity,
                    EndpointProperties = properties,
                    ResponseHeaders = headers
                };

                Log.LogMessage(MessageImportance.Low, $"Adding endpoint {endpoint.Route} for asset {asset.Identity}.");
                CurrentEndpoints.Add(endpoint);
            }
        }

        private static (string mimeType, string cache) ResolveContentType(StaticWebAsset asset, ContentTypeProvider contentTypeProvider, StaticWebAssetGlobMatcher.MatchContext matchContext, TaskLoggingHelper log)
        {
            var relativePath = asset.ComputePathWithoutTokens(asset.RelativePath);
            matchContext.SetPathAndReinitialize(relativePath);

            var mapping = contentTypeProvider.ResolveContentTypeMapping(matchContext, log);

            if (mapping.MimeType != null)
            {
                return (mapping.MimeType, mapping.Cache);
            }

            log.LogMessage(MessageImportance.Low, $"No match for {relativePath}. Using default content type 'application/octet-stream'");

            return ("application/octet-stream", null);
        }

        internal void Finally()
        {
            lock (CollectedEndpoints)
            {
                CollectedEndpoints.AddRange(CurrentEndpoints);
            }
        }

        internal ParallelWorker Process(int i, ParallelLoopState _)
        {
            var asset = StaticWebAsset.FromTaskItem(CandidateAssets[i]);
            asset.ComputeRoutes(_resolvedRoutes);
            // We extract these from the metadata because we avoid the conversion to their typed version and then back to string.
            var length = CandidateAssets[i].GetMetadata(nameof(StaticWebAsset.FileLength));
            var lastWriteTime = CandidateAssets[i].GetMetadata(nameof(StaticWebAsset.LastWriteTime));
            var matchContext = StaticWebAssetGlobMatcher.CreateMatchContext();

            if (ExistingEndpointsByAssetFile != null && ExistingEndpointsByAssetFile.TryGetValue(asset.Identity, out var set))
            {
                for (var j = _resolvedRoutes.Count - 1; j >= 0; j--)
                {
                    var (_, route, _) = _resolvedRoutes[j];
                    // StaticWebAssets has this behavior where the base path for an asset only gets applied if the asset comes from a
                    // package or a referenced project and ignored if it comes from the current project.
                    // When we define the endpoint, we apply the path to the asset as if it was coming from the current project.
                    // If the endpoint is then passed to a referencing project or packaged into a nuget package, the path will be
                    // adjusted at that time.
                    var finalRoute = asset.IsProject() || asset.IsPackage() ? StaticWebAsset.Normalize(Path.Combine(asset.BasePath, route)) : route;

                    // Check if the endpoint we are about to define already exists. This can happen during publish as assets defined
                    // during the build will have already defined endpoints and we only want to add new ones.
                    if (set.Contains(finalRoute))
                    {
                        Log.LogMessage(MessageImportance.Low, $"Skipping asset {asset.Identity} because an endpoint for it already exists at {route}.");
                        _resolvedRoutes.RemoveAt(j);
                    }
                }
            }

            CreateAnAddEndpoints(asset, length, lastWriteTime, matchContext);

            return this;
        }
    }
}
