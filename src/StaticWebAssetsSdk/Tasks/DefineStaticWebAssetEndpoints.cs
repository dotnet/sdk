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
        public ITaskItem[] StaticWebAssets { get; set; }

        [Required]
        public ITaskItem[] ContentTypeMappings { get; set; }

        [Output]
        public ITaskItem[] Endpoints { get; set; }

        public override bool Execute()
        {
            var staticWebAssets = StaticWebAssets.Select(StaticWebAsset.FromTaskItem);
            var contentTypeMappings = ContentTypeMappings.Select(ContentTypeMapping.FromTaskItem).OrderByDescending(m => m.Priority).ToArray();
            var endpoints = new List<StaticWebAssetEndpoint>();

            foreach (var asset in staticWebAssets)
            {
                if (asset.IsPublishOnly())
                {
                    continue;
                }

                var endpoint = new StaticWebAssetEndpoint
                {
                    Route = asset.ComputeTargetPath("", '/'),
                    AssetFile = asset.Identity,
                    ResponseHeaders = [
                        new() {
                            Name = "Content-Type",
                            Value = ResolveContentType(asset, contentTypeMappings)
                        },
                        new()
                        {
                            Name = "Content-Length",
                            Value = GetFileLength(asset),
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
                        new()
                        {
                            Name = "Accept-Ranges",
                            Value = "bytes"
                        }
                    ]
                };

                endpoints.Add(endpoint);
            }

            Endpoints = StaticWebAssetEndpoint.ToTaskItems(endpoints);

            return !Log.HasLoggedErrors;
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
        private static string GetFileLastModified(StaticWebAsset asset)
        {
            var path = File.Exists(asset.OriginalItemSpec) ? asset.OriginalItemSpec : asset.Identity;
            var lastWrite = new FileInfo(path).LastWriteTimeUtc;
            return lastWrite.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture);
        }

        private static string GetFileLength(StaticWebAsset asset)
        {
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
