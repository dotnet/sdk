// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Expressions.Cpp2;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class CreationPath : ICreationPath
    {
        public CreationPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new System.ArgumentException($"'{nameof(path)}' cannot be null or whitespace.", nameof(path));
            }

            Path = path;
        }

        public string Path { get; }

        internal static IReadOnlyList<ICreationPath> ListFromModel(ILogger logger, IReadOnlyList<PrimaryOutputModel> modelList, IVariableCollection rootVariableCollection)
        {
            List<ICreationPath> pathList = new List<ICreationPath>();

            rootVariableCollection ??= new VariableCollection();

            foreach (PrimaryOutputModel model in modelList)
            {
                // Note: this check is probably superfluous. The Model has evaluation info.
                // OTOH: this is probaby a cleaner way to do it.
                if (string.IsNullOrEmpty(model.Condition)
                    || Cpp2StyleEvaluatorDefinition.EvaluateFromString(logger, model.Condition, rootVariableCollection))
                {
                    if (model.PathResolved != null)
                    {
                        ICreationPath path = new CreationPath(model.PathResolved);
                        pathList.Add(path);
                    }
                }
            }

            return pathList;
        }
    }
}
