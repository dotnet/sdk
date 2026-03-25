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

    public ITaskItem[] AdditionalEndpointDefinitions { get; set; }

    [Output]
    public ITaskItem[] Endpoints { get; set; }

    public override bool Execute()
    {
        var existingEndpointsByAssetFile = CreateEndpointsByAssetFile();
        var contentTypeMappings = CreateAdditionalContentTypeMappings();
        var contentTypeProvider = new ContentTypeProvider(contentTypeMappings);
        var additionalEndpointDefinitions = CreateAdditionalEndpointDefinitions();
        var endpoints = new List<StaticWebAssetEndpoint>(CandidateAssets.Length);

        Parallel.For(
            0,
            CandidateAssets.Length,
            () => new ParallelWorker(
                endpoints,
                new List<StaticWebAssetEndpoint>(512),
                CandidateAssets,
                existingEndpointsByAssetFile,
                Log,
                contentTypeProvider,
                additionalEndpointDefinitions),
            static (i, loop, state) => state.Process(i, loop),
            static worker => worker.Finally());

        Endpoints = StaticWebAssetEndpoint.ToTaskItems(endpoints);

        return !Log.HasLoggedErrors;
    }

    private ContentTypeMapping[] CreateAdditionalContentTypeMappings()
    {
        if (ContentTypeMappings == null || ContentTypeMappings.Length == 0)
        {
            return [];
        }
        var result = new ContentTypeMapping[ContentTypeMappings.Length];
        for (var i = 0; i < ContentTypeMappings.Length; i++)
        {
            var contentTypeMapping = ContentTypeMappings[i];
            result[i] = ContentTypeMapping.FromTaskItem(contentTypeMapping);
        }
        Array.Sort(result, (x, y) => x.Priority.CompareTo(y.Priority));
        return result;
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

    private AdditionalEndpointDefinition[] CreateAdditionalEndpointDefinitions()
    {
        if (AdditionalEndpointDefinitions == null || AdditionalEndpointDefinitions.Length == 0)
        {
            return [];
        }

        var result = new AdditionalEndpointDefinition[AdditionalEndpointDefinitions.Length];
        for (var i = 0; i < AdditionalEndpointDefinitions.Length; i++)
        {
            var item = AdditionalEndpointDefinitions[i];
            var pattern = item.GetMetadata("Pattern");
            var replacement = item.GetMetadata("Replacement");
            var order = item.GetMetadata("Order");

            var builder = new StaticWebAssetGlobMatcherBuilder();
            builder.AddIncludePatterns(pattern);
            var matcher = builder.Build();

            // Compute the suffix after the recursive wildcard so we can strip it
            // from the matched route to get the portion captured by **.
            // For "**/index.html" the suffix is "index.html".
            // For "index.html" (no **) the suffix is empty.
            var suffix = "";
            var rwcIndex = pattern.IndexOf("**", StringComparison.Ordinal);
            if (rwcIndex >= 0)
            {
                var afterRwc = pattern.AsSpan().Slice(rwcIndex + 2);
                if (afterRwc.Length > 0 && (afterRwc[0] == '/' || afterRwc[0] == '\\'))
                {
                    afterRwc = afterRwc.Slice(1);
                }
                suffix = afterRwc.ToString();
            }

            result[i] = new AdditionalEndpointDefinition(pattern, replacement, order, suffix, matcher);
        }

        return result;
    }

    internal readonly struct AdditionalEndpointDefinition(string pattern, string replacement, string order, string suffix, StaticWebAssetGlobMatcher matcher)
    {
        public string Pattern { get; } = pattern;
        public string Replacement { get; } = replacement;
        public string Order { get; } = order;
        public string Suffix { get; } = suffix;
        public StaticWebAssetGlobMatcher Matcher { get; } = matcher;
    }

    private readonly struct ParallelWorker(
        List<StaticWebAssetEndpoint> collectedEndpoints,
        List<StaticWebAssetEndpoint> currentEndpoints,
        ITaskItem[] candidateAssets,
        Dictionary<string, HashSet<string>> existingEndpointsByAssetFile,
        TaskLoggingHelper log,
        ContentTypeProvider contentTypeProvider,
        DefineStaticWebAssetEndpoints.AdditionalEndpointDefinition[] additionalEndpointDefinitions)
    {
        public List<StaticWebAssetEndpoint> CollectedEndpoints { get; } = collectedEndpoints;
        public List<StaticWebAssetEndpoint> CurrentEndpoints { get; } = currentEndpoints;
        public ITaskItem[] CandidateAssets { get; } = candidateAssets;
        public Dictionary<string, HashSet<string>> ExistingEndpointsByAssetFile { get; } = existingEndpointsByAssetFile;
        public TaskLoggingHelper Log { get; } = log;
        public ContentTypeProvider ContentTypeProvider { get; } = contentTypeProvider;
        public DefineStaticWebAssetEndpoints.AdditionalEndpointDefinition[] AdditionalEndpointDefinitions { get; } = additionalEndpointDefinitions;

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
                var headers = new StaticWebAssetEndpointResponseHeader[5]
                {
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
                    headers[4] = new() { Name = "Cache-Control", Value = "max-age=31536000, immutable" };
                }
                else
                {
                    // Force revalidation on non-fingerprinted assets. We can be more granular here and have rules based on the content type.
                    // These values can later be changed at runtime by modifying the endpoint. For example, it might be safer to cache images
                    // for a longer period of time than scripts or stylesheets.
                    headers[4] = new() { Name = "Cache-Control", Value = !string.IsNullOrEmpty(cacheSetting) ? cacheSetting : "no-cache" };
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

                // Generate additional endpoints from definitions
                if (AdditionalEndpointDefinitions.Length > 0)
                {
                    CreateAdditionalEndpoints(endpoint);
                }
            }
        }

        private void CreateAdditionalEndpoints(StaticWebAssetEndpoint sourceEndpoint)
        {
            var matchContext = StaticWebAssetGlobMatcher.CreateMatchContext();
            for (var d = 0; d < AdditionalEndpointDefinitions.Length; d++)
            {
                var definition = AdditionalEndpointDefinitions[d];
                matchContext.SetPathAndReinitialize(sourceEndpoint.Route);
                var match = definition.Matcher.Match(matchContext);
                if (!match.IsMatch)
                {
                    continue;
                }

                // The glob matcher's Stem captures everything from the ** start to the
                // end of the path, including the literal suffix of the pattern.
                // For example, **/index.html matching admin/index.html produces
                // stem="admin/index.html". We need to strip the suffix ("index.html")
                // to get the actual ** captured portion ("admin").
                var route = sourceEndpoint.Route;
                string stem;
                if (!string.IsNullOrEmpty(definition.Suffix))
                {
                    // Strip the suffix from the route to get the ** portion.
                    if (route.Length > definition.Suffix.Length &&
                        route.EndsWith(definition.Suffix, StringComparison.OrdinalIgnoreCase) &&
                        route[route.Length - definition.Suffix.Length - 1] == '/')
                    {
                        // e.g., "admin/index.html" → "admin"
                        stem = route.Substring(0, route.Length - definition.Suffix.Length - 1);
                    }
                    else if (route.Equals(definition.Suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        // e.g., "index.html" → ""
                        stem = "";
                    }
                    else
                    {
                        stem = "";
                    }
                }
                else
                {
                    stem = "";
                }

                // Build the new route from the captured stem and the replacement.
                string newRoute;
                if (string.IsNullOrEmpty(definition.Replacement))
                {
                    // When replacement is empty, the new route is just the stem (e.g., **/index.html -> the ** part)
                    newRoute = stem;
                }
                else if (string.IsNullOrEmpty(stem))
                {
                    // When there's no stem, the replacement becomes the full route (e.g., index.html -> {**fallback:nonfile})
                    newRoute = definition.Replacement;
                }
                else
                {
                    // Combine stem with replacement (e.g., stem=admin, replacement=something -> admin/something)
                    newRoute = $"{stem}/{definition.Replacement}";
                }

                // Normalize the route
                newRoute = StaticWebAsset.Normalize(newRoute);

                var additionalEndpoint = new StaticWebAssetEndpoint()
                {
                    Route = newRoute,
                    AssetFile = sourceEndpoint.AssetFile,
                    Selectors = sourceEndpoint.Selectors.ToArray(),
                    ResponseHeaders = sourceEndpoint.ResponseHeaders.ToArray(),
                    EndpointProperties = sourceEndpoint.EndpointProperties.ToArray(),
                    Order = !string.IsNullOrEmpty(definition.Order) ? definition.Order : null,
                };

                Log.LogMessage(MessageImportance.Low, $"Adding additional endpoint {additionalEndpoint.Route} (order={definition.Order}) for asset {sourceEndpoint.AssetFile} from pattern {definition.Pattern}.");
                CurrentEndpoints.Add(additionalEndpoint);
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
