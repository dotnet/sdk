// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Build.Framework;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.NET.Sdk.StaticWebAssets.Tasks;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    public class DefineStaticWebAssetEndpoints : Task
    {
        [Required]
        public ITaskItem[] CandidateAssets { get; set; }

        [Required]
        public ITaskItem[] ExistingEndpoints { get; set; }

        [Required]
        public ITaskItem[] ContentTypeMappings { get; set; }

        [Output]
        public ITaskItem[] Endpoints { get; set; }

        public Func<string, int> TestLengthResolver;
        public Func<string, DateTime> TestLastWriteResolver;

        public override bool Execute()
        {
            var staticWebAssets = CandidateAssets.Select(StaticWebAsset.FromTaskItem).ToDictionary(a => a.Identity);
            var existingEndpoints = StaticWebAssetEndpoint.FromItemGroup(ExistingEndpoints);
            var existingEndpointsByAssetFile = existingEndpoints
                .GroupBy(e => e.AssetFile, OSPath.PathComparer)
                .ToDictionary(g => g.Key, g => new HashSet<StaticWebAssetEndpoint>(g, StaticWebAssetEndpoint.RouteAndAssetComparer));

            var assetsToRemove = new List<string>();
            foreach (var kvp in existingEndpointsByAssetFile)
            {
                var asset = kvp.Key;
                var set = kvp.Value;
                if (!staticWebAssets.ContainsKey(asset))
                {
                    assetsToRemove.Remove(asset);
                }
            }
            foreach (var asset in assetsToRemove)
            {
                Log.LogMessage(MessageImportance.Low, $"Removing endpoints for asset '{asset}' because it no longer exists.");
                existingEndpointsByAssetFile.Remove(asset);
            }

            var contentTypeMappings = ContentTypeMappings.Select(ContentTypeMapping.FromTaskItem).OrderByDescending(m => m.Priority).ToArray();
            var endpoints = new List<StaticWebAssetEndpoint>();

            foreach (var kvp in staticWebAssets)
            {
                var asset = kvp.Value;
                StaticWebAssetEndpoint endpoint = null;

                // StaticWebAssets has this behavior where the base path for an asset only gets applied if the asset comes from a
                // package or a referenced project and ignored if it comes from the current project.
                // When we define the endpoint, we apply the path to the asset as if it was coming from the current project.
                // If the endpoint is then passed to a referencing project or packaged into a nuget package, the path will be
                // adjusted at that time.
                endpoint = CreateEndpoint(asset, contentTypeMappings);

                // Check if the endpoint we are about to define already exists. This can happen during publish as assets defined
                // during the build will have already defined endpoints and we only want to add new ones.
                if (existingEndpointsByAssetFile.TryGetValue(asset.Identity, out var set) &&
                    set.TryGetValue(endpoint, out var existingEndpoint))
                {
                    Log.LogMessage(MessageImportance.Low, $"Skipping asset {asset.Identity} because an endpoint for it already exists at {existingEndpoint.Route}.");
                    continue;
                }

                Log.LogMessage(MessageImportance.Low, $"Adding endpoint {endpoint.Route} for asset {asset.Identity}.");
                endpoints.Add(endpoint);
            }

            Endpoints = StaticWebAssetEndpoint.ToTaskItems(endpoints);

            return !Log.HasLoggedErrors;
        }

        private StaticWebAssetEndpoint CreateEndpoint(StaticWebAsset asset, ContentTypeMapping[] contentTypeMappings) =>
            new()
            {
                Route = asset.ComputeTargetPath("", '/'),
                AssetFile = asset.Identity,
                ResponseHeaders =
                [
                    new()
                    {
                        Name = "Accept-Ranges",
                        Value = "bytes"
                    },
                    new()
                    {
                        Name = "Content-Length",
                        Value = GetFileLength(asset),
                    },
                    new()
                    {
                        Name = "Content-Type",
                        Value = ResolveContentType(asset, contentTypeMappings)
                    },
                    new()
                    {
                        Name = "ETag",
                        Value = asset.Integrity,
                    },
                    new()
                    {
                        Name = "Last-Modified",
                        Value = GetFileLastModified(asset)
                    },
                ]
            };

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
        private string GetFileLastModified(StaticWebAsset asset)
        {
            var lastWrite = TestLastWriteResolver != null ? TestLastWriteResolver(asset.Identity) : GetFileLastModifiedCore(asset);
            return lastWrite.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture);
        }

        private static DateTime GetFileLastModifiedCore(StaticWebAsset asset)
        {
            var path = File.Exists(asset.OriginalItemSpec) ? asset.OriginalItemSpec : asset.Identity;
            var lastWrite = new FileInfo(path).LastWriteTimeUtc;
            return lastWrite;
        }

        private string GetFileLength(StaticWebAsset asset)
        {
            if (TestLengthResolver != null)
            {
                return TestLengthResolver(asset.Identity).ToString(CultureInfo.InvariantCulture);
            }

            var path = File.Exists(asset.OriginalItemSpec) ? asset.OriginalItemSpec : asset.Identity;
            return new FileInfo(path).Length.ToString(CultureInfo.InvariantCulture);
        }

        private string ResolveContentType(StaticWebAsset asset, ContentTypeMapping[] contentTypeMappings)
        {
            foreach (var mapping in contentTypeMappings)
            {
                if (mapping.Matches(Path.GetFileName(asset.RelativePath)))
                {
                    Log.LogMessage(MessageImportance.Low, $"Matched {asset.RelativePath} to {mapping.MimeType} using pattern {mapping.Pattern}");
                    return mapping.MimeType;
                }
                else
                {
                    Log.LogMessage(MessageImportance.Low, $"No match for {asset.RelativePath} using pattern {mapping.Pattern}");
                }
            }

            Log.LogMessage(MessageImportance.Low, $"No match for {asset.RelativePath}. Using default content type 'application/octet-stream'");

            return "application/octet-stream";
        }

        private class ContentTypeMapping
        {
            private Matcher _matcher;

            public ContentTypeMapping(string mimeType, string pattern, int priority)
            {
                Pattern = pattern;
                MimeType = mimeType;
                Priority = priority;
                _matcher = new Matcher();
                _matcher.AddInclude(pattern);

            }

            public string Pattern { get; set; }

            public string MimeType { get; set; }

            public int Priority { get; }

            internal static ContentTypeMapping FromTaskItem(ITaskItem contentTypeMappings)
            {
                return new ContentTypeMapping(
                    contentTypeMappings.ItemSpec,
                    contentTypeMappings.GetMetadata(nameof(Pattern)),
                    int.Parse(contentTypeMappings.GetMetadata(nameof(Priority))));
            }

            internal bool Matches(string identity) => _matcher.Match(identity).HasMatches;
        }
    }
}
