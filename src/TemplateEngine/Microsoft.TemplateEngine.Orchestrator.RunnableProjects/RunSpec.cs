// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class RunSpec : IRunSpec
    {
        private readonly IReadOnlyList<IOperationProvider> _overrides;
        private readonly IVariableCollection _vars;

        internal RunSpec(IReadOnlyList<IOperationProvider> operationOverrides, IVariableCollection vars, string variableFormatString)
        {
            _overrides = operationOverrides;
            _vars = vars ?? new VariableCollection();
            VariableFormatString = variableFormatString ?? "{0}";
        }

        public bool TryGetTargetRelPath(string sourceRelPath, out string targetRelPath)
        {
            targetRelPath = null;
            return false;
        }

        public IReadOnlyList<IOperationProvider> GetOperations(IReadOnlyList<IOperationProvider> sourceOperations)
        {
            return _overrides ?? sourceOperations;
        }

        public IVariableCollection ProduceCollection(IVariableCollection parent)
        {
            return _vars;
        }

        public string VariableFormatString { get; }
    }
}
