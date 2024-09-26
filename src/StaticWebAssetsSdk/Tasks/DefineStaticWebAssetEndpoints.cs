// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.NET.Sdk.StaticWebAssets.Tasks;
using System.Collections.Concurrent;
using System.Globalization;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
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
                for (int i = 0; i < AssetFileDetails.Length; i++)
                {
                    var item = AssetFileDetails[i];
                    _assetFileDetails[item.ItemSpec] = item;
                }
            }

            var existingEndpointsByAssetFile = CreateEndpointsByAssetFile();
            var contentTypeMappings = ContentTypeMappings.Select(ContentTypeMapping.FromTaskItem).OrderByDescending(m => m.Priority).ToArray();
            var contentTypeProvider = new ContentTypeProvider(contentTypeMappings);
            var endpoints = new ConcurrentBag<StaticWebAssetEndpoint>();

            Parallel.For(0, CandidateAssets.Length, i =>
            {
                var asset = StaticWebAsset.FromTaskItem(CandidateAssets[i]);
                var routes = asset.ComputeRoutes().ToList();

                if (existingEndpointsByAssetFile != null && existingEndpointsByAssetFile.TryGetValue(asset.Identity, out var set))
                {
                    for (var j = routes.Count; j >= 0; j--)
                    {
                        var (label, route, values) = routes[j];
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

                foreach (var endpoint in CreateEndpoints(routes, asset, contentTypeProvider))
                {
                    Log.LogMessage(MessageImportance.Low, $"Adding endpoint {endpoint.Route} for asset {asset.Identity}.");
                    endpoints.Add(endpoint);
                }
            });

            Endpoints = StaticWebAssetEndpoint.ToTaskItems(endpoints);

            return !Log.HasLoggedErrors;
        }

        private Dictionary<string, HashSet<string>> CreateEndpointsByAssetFile()
        {
            if (ExistingEndpoints != null && ExistingEndpoints.Length > 0)
            {
                Dictionary<string, HashSet<string>> existingEndpointsByAssetFile = new(OSPath.PathComparer);
                existingEndpointsByAssetFile = new(OSPath.PathComparer);
                var assets = new HashSet<string>();
                foreach (var asset in CandidateAssets)
                {
                    assets.Add(asset.ItemSpec);
                }

                for (int i = 0; i < ExistingEndpoints.Length; i++)
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
                        set = [];
                        existingEndpointsByAssetFile[assetFile] = set;
                    }

                    // Add the route
                    set.Add(endpointCandidate.ItemSpec);
                }
            }

            return null;
        }

        private List<StaticWebAssetEndpoint> CreateEndpoints(List<StaticWebAsset.StaticWebAssetResolvedRoute> routes, StaticWebAsset asset, ContentTypeProvider contentTypeMappings)
        {
            var (length, lastModified) = ResolveDetails(asset);
            var result = new List<StaticWebAssetEndpoint>();
            foreach (var (label, route, values) in routes)
            {
                var (mimeType, cacheSetting) = ResolveContentType(asset, contentTypeMappings);
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
            if (_assetFileDetails != null && _assetFileDetails.TryGetValue(asset.Identity, out var details))
            {
                return (length: details.GetMetadata("FileLength"), lastModified: details.GetMetadata("LastWriteTimeUtc"));
            }
            else if (_assetFileDetails != null && _assetFileDetails.TryGetValue(asset.OriginalItemSpec, out var originalDetails))
            {
                return (length: originalDetails.GetMetadata("FileLength"), lastModified: originalDetails.GetMetadata("LastWriteTimeUtc"));
            }
            else if (TestLastWriteResolver != null || TestLengthResolver != null)
            {
                return (length: GetTestFileLength(asset), lastModified: GetTestFileLastModified(asset));
            }
            else
            {
                Log.LogMessage(MessageImportance.High, $"No details found for {asset.Identity}. Using file system to resolve details.");
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

        private (string mimeType, string cache) ResolveContentType(StaticWebAsset asset, ContentTypeProvider contentTypeProvider)
        {
            var relativePath = asset.ComputePathWithoutTokens(asset.RelativePath);
            var mapping = contentTypeProvider.ResolveContentTypeMapping(relativePath, Log);

            if (mapping.MimeType != null)
            {
                return (mapping.MimeType, mapping.Cache);
            }

            Log.LogMessage(MessageImportance.Low, $"No match for {relativePath}. Using default content type 'application/octet-stream'");

            return ("application/octet-stream", null);
        }
    }
}
