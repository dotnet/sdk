// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli
{
    public class HostSpecificDataLoader : IHostSpecificDataLoader
    {
        private readonly IEngineEnvironmentSettings _engineEnvironment;

        private static readonly JsonDocumentOptions s_jsonDocumentOptions = new()
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private readonly ConcurrentDictionary<ITemplateInfo, HostSpecificTemplateData> _cache =
            new();

        public HostSpecificDataLoader(IEngineEnvironmentSettings engineEnvironment)
        {
            _engineEnvironment = engineEnvironment;
        }

        public HostSpecificTemplateData ReadHostSpecificTemplateData(ITemplateInfo templateInfo)
        {
            return _cache.GetOrAdd(templateInfo, ReadHostSpecificTemplateDataUncached);
        }

        private HostSpecificTemplateData ReadHostSpecificTemplateDataUncached(ITemplateInfo templateInfo)
        {
            IMountPoint? mountPoint = null;

            if (templateInfo is ITemplateInfoHostJsonCache { HostData: string hostData })
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(hostData))
                    {
                        JsonObject? jObject = JsonNode.Parse(hostData, nodeOptions: null, s_jsonDocumentOptions)?.AsObject();
                        return new HostSpecificTemplateData(jObject);
                    }
                }
                catch (Exception e)
                {
                    _engineEnvironment.Host.Logger.LogWarning(
                        e,
                        LocalizableStrings.HostSpecificDataLoader_Warning_FailedToRead,
                        templateInfo.ShortNameList?[0] ?? templateInfo.Name);
                }
            }

            IFile? file = null;
            try
            {
                if (!string.IsNullOrEmpty(templateInfo.HostConfigPlace) && _engineEnvironment.TryGetMountPoint(templateInfo.MountPointUri, out mountPoint) && mountPoint != null)
                {
                    file = mountPoint.FileInfo(templateInfo.HostConfigPlace);
                    if (file != null && file.Exists)
                    {
                        JsonObject? jsonData;
                        using (Stream stream = file.OpenRead())
                        {
                            jsonData = JsonNode.Parse(stream, nodeOptions: null, s_jsonDocumentOptions)?.AsObject();
                        }

                        return new HostSpecificTemplateData(jsonData);
                    }
                }
            }
            catch (Exception e)
            {
                _engineEnvironment.Host.Logger.LogWarning(
                    e,
                    LocalizableStrings.HostSpecificDataLoader_Warning_FailedToReadFromFile,
                    templateInfo.ShortNameList?[0] ?? templateInfo.Name,
                    file?.GetDisplayPath() ?? templateInfo.MountPointUri + templateInfo.HostConfigPlace);
            }
            finally
            {
                mountPoint?.Dispose();
            }
            return HostSpecificTemplateData.Default;
        }
    }
}
