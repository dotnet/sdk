// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel
{
    /// <summary>
    /// Defines the primary output. Corresponds to the element of "primaryOutputs" JSON array.
    /// Primary outputs define the list of template files for further processing.
    /// </summary>
    public sealed class PrimaryOutputModel : ConditionedConfigurationElement
    {
        internal PrimaryOutputModel(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new System.ArgumentException($"'{nameof(path)}' cannot be null or whitespace.", nameof(path));
            }

            Path = path;
        }

        /// <summary>
        /// Defines the relative path to the file after the template is instantiated.
        /// </summary>
        public string Path { get; }

        internal static IReadOnlyList<PrimaryOutputModel> ListFromJArray(JArray? jsonData)
        {
            List<PrimaryOutputModel> modelList = new List<PrimaryOutputModel>();

            if (jsonData == null)
            {
                return modelList;
            }

            foreach (JToken pathInfo in jsonData)
            {
                string? path = pathInfo.ToString(nameof(Path));
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                PrimaryOutputModel pathModel = new PrimaryOutputModel(path!.NormalizePath())
                {
                    Condition = pathInfo.ToString("condition")
                };

                modelList.Add(pathModel);
            }

            return modelList;
        }
    }
}
