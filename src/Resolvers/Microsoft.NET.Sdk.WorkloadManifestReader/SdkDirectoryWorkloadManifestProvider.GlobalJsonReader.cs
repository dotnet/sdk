// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Text.Json;
using Microsoft.NET.Sdk.Localization;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadManifestReader;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public partial class SdkDirectoryWorkloadManifestProvider
    {
        public static class GlobalJsonReader
        {
            public static string? GetWorkloadVersionFromGlobalJson(string? globalJsonPath, out bool? shouldUseWorkloadSets)
            {
                shouldUseWorkloadSets = null;
                if (string.IsNullOrEmpty(globalJsonPath))
                {
                    return null;
                }

                var readerOptions = new JsonReaderOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                };

                // Use StreamReader with BOM detection to determine the encoding
                using var streamReader = new StreamReader(globalJsonPath, detectEncodingFromByteOrderMarks: true);
                streamReader.Peek(); // trigger BOM detection without consuming content

                if (streamReader.CurrentEncoding is UTF8Encoding)
                {
                    // UTF-8 (with or without BOM): stream the underlying file directly.
                    // Utf8JsonStreamReader handles the UTF-8 BOM itself.
                    streamReader.BaseStream.Seek(0, SeekOrigin.Begin);
                    var reader = new Utf8JsonStreamReader(streamReader.BaseStream, readerOptions);
                    return ParseGlobalJson(ref reader, out shouldUseWorkloadSets);
                }
                else
                {
                    // For other encodings (e.g. UTF-16 LE/BE), transcode to UTF-8 in memory.
                    // global.json files are small so this is acceptable.
                    var content = streamReader.ReadToEnd();
                    using var memStream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                    var reader = new Utf8JsonStreamReader(memStream, readerOptions);
                    return ParseGlobalJson(ref reader, out shouldUseWorkloadSets);
                }
            }

            private static string? ParseGlobalJson(ref Utf8JsonStreamReader reader, out bool? shouldUseWorkloadSets)
            {
                shouldUseWorkloadSets = null;
                string? workloadVersion = null;

                JsonReader.ConsumeToken(ref reader, JsonTokenType.StartObject);
                while (reader.Read())
                {
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.PropertyName:
                            var propName = reader.GetString();
                            if (string.Equals("sdk", propName, StringComparison.OrdinalIgnoreCase))
                            {
                                JsonReader.ConsumeToken(ref reader, JsonTokenType.StartObject);

                                bool readingSdk = true;
                                while (readingSdk && reader.Read())
                                {
                                    switch (reader.TokenType)
                                    {
                                        case JsonTokenType.PropertyName:
                                            var sdkPropName = reader.GetString();
                                            if (string.Equals("workloadVersion", sdkPropName, StringComparison.OrdinalIgnoreCase))
                                            {
                                                workloadVersion = JsonReader.ReadString(ref reader);
                                            }
                                            else if (string.Equals("workloads-update-mode", sdkPropName, StringComparison.OrdinalIgnoreCase))
                                            {
                                                var useWorkloadSetsString = JsonReader.ReadString(ref reader);
                                                shouldUseWorkloadSets = "workload-set".Equals(useWorkloadSetsString, StringComparison.OrdinalIgnoreCase) ? true :
                                                                        "manifests".Equals(useWorkloadSetsString, StringComparison.OrdinalIgnoreCase) ? false :
                                                                        shouldUseWorkloadSets;
                                            }
                                            else
                                            {
                                                JsonReader.ConsumeValue(ref reader);
                                            }
                                            break;
                                        case JsonTokenType.EndObject:
                                            readingSdk = false;
                                            break;
                                        default:
                                            throw new JsonFormatException(Strings.UnexpectedTokenAtOffset, reader.TokenType, reader.TokenStartIndex);
                                    }
                                }
                            }
                            else
                            {
                                JsonReader.ConsumeValue(ref reader);
                            }
                            break;

                        case JsonTokenType.EndObject:
                            return workloadVersion;
                        default:
                            throw new JsonFormatException(Strings.UnexpectedTokenAtOffset, reader.TokenType, reader.TokenStartIndex);
                    }
                }

                throw new JsonFormatException(Strings.IncompleteDocument);
            }
        }
    }
}
