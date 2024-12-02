// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
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

    public ITaskItem[] AssetFileDetails { get; set; }

    [Output]
    public ITaskItem[] Endpoints { get; set; }

    public Func<string, int> TestLengthResolver;
    public Func<string, DateTime> TestLastWriteResolver;

    private Dictionary<string, ITaskItem> _assetFileDetails;

    public override bool Execute()
    {
        if (AssetFileDetails != null)
        {
            _assetFileDetails = new(AssetFileDetails.Length, OSPath.PathComparer);
            for (var i = 0; i < AssetFileDetails.Length; i++)
            {
                var item = AssetFileDetails[i];
                _assetFileDetails[item.ItemSpec] = item;
            }
        }

        var existingEndpointsByAssetFile = CreateEndpointsByAssetFile();
        var contentTypeMappings = ContentTypeMappings.Select(ContentTypeMapping.FromTaskItem).OrderByDescending(m => m.Priority).ToArray();
        var contentTypeProvider = new ContentTypeProvider(contentTypeMappings);
        var endpoints = new List<StaticWebAssetEndpoint>();

        Parallel.For(
            0,
            CandidateAssets.Length,
            () => new ParallelWorker(
                endpoints,
                new List<StaticWebAssetEndpoint>(),
                CandidateAssets,
                existingEndpointsByAssetFile,
                Log,
                contentTypeProvider,
                _assetFileDetails,
                TestLengthResolver,
                TestLastWriteResolver),
            static (i, loop, state) => state.Process(i, loop),
            static worker => worker.Finally());

        Endpoints = StaticWebAssetEndpoint.ToTaskItems(endpoints);

        return !Log.HasLoggedErrors;
    }

    private Dictionary<string, HashSet<string>> CreateEndpointsByAssetFile()
    {
        if (ExistingEndpoints != null && ExistingEndpoints.Length > 0)
        {
            Dictionary<string, HashSet<string>> existingEndpointsByAssetFile = new(OSPath.PathComparer);
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
        ContentTypeProvider contentTypeProvider,
        Dictionary<string, ITaskItem> assetDetails,
        Func<string, int> testLengthResolver,
        Func<string, DateTime> testLastWriteResolver)
    {
        public List<StaticWebAssetEndpoint> CollectedEndpoints { get; } = collectedEndpoints;
        public List<StaticWebAssetEndpoint> CurrentEndpoints { get; } = currentEndpoints;
        public ITaskItem[] CandidateAssets { get; } = candidateAssets;
        public Dictionary<string, HashSet<string>> ExistingEndpointsByAssetFile { get; } = existingEndpointsByAssetFile;
        public TaskLoggingHelper Log { get; } = log;
        public ContentTypeProvider ContentTypeProvider { get; } = contentTypeProvider;
        public Dictionary<string, ITaskItem> AssetDetails { get; } = assetDetails;
        public Func<string, int> TestLengthResolver { get; } = testLengthResolver;
        public Func<string, DateTime> TestLastWriteResolver { get; } = testLastWriteResolver;

        private List<StaticWebAssetEndpoint> CreateEndpoints(
            List<StaticWebAsset.StaticWebAssetResolvedRoute> routes,
            StaticWebAsset asset,
            StaticWebAssetGlobMatcher.MatchContext matchContext)
        {
            var (length, lastModified) = ResolveDetails(asset);
            var result = new List<StaticWebAssetEndpoint>();
            foreach (var (label, route, values) in routes)
            {
                var (mimeType, cacheSetting) = ResolveContentType(asset, ContentTypeProvider, matchContext, Log);
                List<StaticWebAssetEndpointResponseHeader> headers = [
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
                        Value = lastModified
                    },
                ];

                if (values.ContainsKey("fingerprint"))
                {
                    // max-age=31536000 is one year in seconds. immutable means that the asset will never change.
                    // max-age is for browsers that do not support immutable.
                    headers.Add(new() { Name = "Cache-Control", Value = "max-age=31536000, immutable" });
                }
                else
                {
                    // Force revalidation on non-fingerprinted assets. We can be more granular here and have rules based on the content type.
                    // These values can later be changed at runtime by modifying the endpoint. For example, it might be safer to cache images
                    // for a longer period of time than scripts or stylesheets.
                    headers.Add(new() { Name = "Cache-Control", Value = !string.IsNullOrEmpty(cacheSetting) ? cacheSetting : "no-cache" });
                }

                var properties = values.Select(v => new StaticWebAssetEndpointProperty { Name = v.Key, Value = v.Value });
                if (values.Count > 0)
                {
                    // If an endpoint has values from its route replaced, we add a label to the endpoint so that it can be easily identified.
                    // The combination of label and list of values should be unique.
                    // In this way, we can identify an endpoint resource.fingerprint.ext by its label (for example resource.ext) and its values
                    // (fingerprint).
                    properties = properties.Append(new StaticWebAssetEndpointProperty { Name = "label", Value = label });
                }

                // We append the integrity in the format expected by the browser so that it can be opaque to the runtime.
                // If in the future we change it to sha384 or sha512, the runtime will not need to be updated.
                properties = properties.Append(new StaticWebAssetEndpointProperty { Name = "integrity", Value = $"sha256-{asset.Integrity}" });

                var finalRoute = asset.IsProject() || asset.IsPackage() ? StaticWebAsset.Normalize(Path.Combine(asset.BasePath, route)) : route;

                var endpoint = new StaticWebAssetEndpoint()
                {
                    Route = finalRoute,
                    AssetFile = asset.Identity,
                    EndpointProperties = [.. properties],
                    ResponseHeaders = [.. headers]
                };
                result.Add(endpoint);
            }

            return result;
        }

        // Last-Modified: <day-name>, <day> <month> <year> <hour>:<minute>:<second> GMT
        // Directives
        // <day-name>
        // One of "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", or "Sun" (case-sensitive).
        //
        // <day>
        // 2 digit day number, e.g. "04" or "23".
        //
        // <month>
        // One of "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" (case sensitive).
        //
        // <year>
        // 4 digit year number, e.g. "1990" or "2016".
        //
        // <hour>
        // 2 digit hour number, e.g. "09" or "23".
        //
        // <minute>
        // 2 digit minute number, e.g. "04" or "59".
        //
        // <second>
        // 2 digit second number, e.g. "04" or "59".
        //
        // GMT
        // Greenwich Mean Time.HTTP dates are always expressed in GMT, never in local time.
        private (string length, string lastModified) ResolveDetails(StaticWebAsset asset)
        {
            if (AssetDetails != null && AssetDetails.TryGetValue(asset.Identity, out var details))
            {
                return (length: details.GetMetadata("FileLength"), lastModified: details.GetMetadata("LastWriteTimeUtc"));
            }
            else if (AssetDetails != null && AssetDetails.TryGetValue(asset.OriginalItemSpec, out var originalDetails))
            {
                return (length: originalDetails.GetMetadata("FileLength"), lastModified: originalDetails.GetMetadata("LastWriteTimeUtc"));
            }
            else if (TestLastWriteResolver != null || TestLengthResolver != null)
            {
                return (length: GetTestFileLength(asset), lastModified: GetTestFileLastModified(asset));
            }
            else
            {
                Log.LogMessage(MessageImportance.Normal, $"No details found for {asset.Identity}. Using file system to resolve details.");
                var fileInfo = StaticWebAsset.ResolveFile(asset.Identity, asset.OriginalItemSpec);
                var length = fileInfo.Length.ToString(CultureInfo.InvariantCulture);
                var lastModified = fileInfo.LastWriteTimeUtc.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture);
                return (length, lastModified);
            }
        }

        // Only used for testing
        private string GetTestFileLastModified(StaticWebAsset asset)
        {
            var lastWrite = TestLastWriteResolver != null ? TestLastWriteResolver(asset.Identity) : asset.ResolveFile().LastWriteTimeUtc;
            return lastWrite.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture);
        }

        // Only used for testing
        private string GetTestFileLength(StaticWebAsset asset)
        {
            if (TestLengthResolver != null)
            {
                return TestLengthResolver(asset.Identity).ToString(CultureInfo.InvariantCulture);
            }

            var fileInfo = asset.ResolveFile();
            return fileInfo.Length.ToString(CultureInfo.InvariantCulture);
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
            var routes = asset.ComputeRoutes().ToList();
            var matchContext = StaticWebAssetGlobMatcher.CreateMatchContext();

            if (ExistingEndpointsByAssetFile != null && ExistingEndpointsByAssetFile.TryGetValue(asset.Identity, out var set))
            {
                for (var j = routes.Count - 1; j >= 0; j--)
                {
                    var (_, route, _) = routes[j];
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
                        routes.RemoveAt(j);
                    }
                }
            }

            foreach (var endpoint in CreateEndpoints(routes, asset, matchContext))
            {
                Log.LogMessage(MessageImportance.Low, $"Adding endpoint {endpoint.Route} for asset {asset.Identity}.");
                CurrentEndpoints.Add(endpoint);
            }

            return this;
        }
    }
}
