// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Expressions.Cpp2;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class CreationPath : ICreationPath
    {
        public string Path { get; set; }

        internal static IReadOnlyList<ICreationPath> ListFromModel(IEngineEnvironmentSettings environmentSettings, IReadOnlyList<ICreationPathModel> modelList, IVariableCollection rootVariableCollection)
        {
            List<ICreationPath> pathList = new List<ICreationPath>();

            if (rootVariableCollection == null)
            {
                rootVariableCollection = new VariableCollection();
            }

            foreach (ICreationPathModel model in modelList)
            {
                // Note: this check is probably superfluous. The Model has evaluation info.
                // OTOH: this is probaby a cleaner way to do it.
                if (string.IsNullOrEmpty(model.Condition)
                    || Cpp2StyleEvaluatorDefinition.EvaluateFromString(environmentSettings, model.Condition, rootVariableCollection))
                {
                    ICreationPath path = new CreationPath()
                    {
                        Path = model.PathResolved
                    };
                    pathList.Add(path);
                }
            }

            return pathList;
        }
    }
}
