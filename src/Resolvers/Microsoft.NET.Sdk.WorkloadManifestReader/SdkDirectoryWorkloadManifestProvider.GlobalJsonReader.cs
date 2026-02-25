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

                using var fileStream = File.OpenRead(globalJsonPath);

                // Probe for UTF-16 BOM (global.json is user-generated and may not be UTF-8)
                var bomBuffer = new byte[4];
                int bomBytesRead = fileStream.Read(bomBuffer, 0, bomBuffer.Length);
                fileStream.Seek(0, SeekOrigin.Begin);

                bool isUtf16 = bomBytesRead >= 2 &&
                    ((bomBuffer[0] == 0xFF && bomBuffer[1] == 0xFE) || // UTF-16 LE
                     (bomBuffer[0] == 0xFE && bomBuffer[1] == 0xFF));  // UTF-16 BE

                var readerOptions = new JsonReaderOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                };

                if (isUtf16)
                {
                    // For UTF-16 encoded files, transcode to UTF-8 in memory
                    // (global.json files are small so this is acceptable)
                    using var streamReader = new StreamReader(fileStream, detectEncodingFromByteOrderMarks: true);
                    var content = streamReader.ReadToEnd();
                    using var memStream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                    var reader = new Utf8JsonStreamReader(memStream, readerOptions);
                    return ParseGlobalJson(ref reader, out shouldUseWorkloadSets);
                }
                else
                {
                    var reader = new Utf8JsonStreamReader(fileStream, readerOptions);
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
