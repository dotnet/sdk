// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;
using Microsoft.TemplateEngine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateSearch.TemplateDiscovery.AdditionalData
{
    internal class CliHostTemplateDataLoader
    {
        private readonly IEngineEnvironmentSettings _engineEnvironment;

        internal CliHostTemplateDataLoader(IEngineEnvironmentSettings engineEnvironment)
        {
            _engineEnvironment = engineEnvironment;
        }

        internal CliHostTemplateData ReadHostSpecificTemplateData(ITemplateInfo templateInfo)
        {
            IMountPoint? mountPoint = null;

            if (templateInfo is ITemplateInfoHostJsonCache { HostData: string hostData })
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(hostData))
                    {
                        JsonObject jObject = JExtensions.ParseJsonObject(hostData);
                        return new CliHostTemplateData(jObject);
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(
                        "Failed to load dotnet CLI host data for template {0} from cache.",
                        templateInfo.ShortNameList?[0] ?? templateInfo.Name);
                    Console.Error.WriteLine("Details: {0}", e);
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
                        using Stream stream = file.OpenRead();
                        using TextReader textReader = new StreamReader(stream, true);
                        string jsonContent = textReader.ReadToEnd();
                        var jsonData = JExtensions.ParseJsonObject(jsonContent);

                        return new CliHostTemplateData(jsonData);
                    }
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(
                    "Failed to load dotnet CLI host data for template {0} from {1}. The host data will be ignored.",
                    templateInfo.ShortNameList?[0] ?? templateInfo.Name,
                    file?.GetDisplayPath() ?? (templateInfo.MountPointUri + templateInfo.HostConfigPlace));
                Console.Error.WriteLine("Details: {0}", e);
            }
            finally
            {
                mountPoint?.Dispose();
            }
            return CliHostTemplateData.Default;
        }
    }

}
