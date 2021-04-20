// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class CreationPathModel : ConditionedConfigurationElementBase, ICreationPathModel
    {
        public string PathOriginal { get; set; }

        public string PathResolved { get; set; }

        internal static IReadOnlyList<ICreationPathModel> ListFromJArray(JArray jsonData)
        {
            List<ICreationPathModel> modelList = new List<ICreationPathModel>();

            if (jsonData == null)
            {
                return modelList;
            }

            foreach (JToken pathInfo in jsonData)
            {
                ICreationPathModel pathModel = new CreationPathModel()
                {
                    PathOriginal = pathInfo.ToString("path").NormalizePath(),
                    Condition = pathInfo.ToString("condition")
                };

                modelList.Add(pathModel);
            }

            return modelList;
        }
    }
}
