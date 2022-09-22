// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class RunSpec : IRunSpec
    {
        private readonly IReadOnlyList<IOperationProvider> _overrides;

        internal RunSpec(IReadOnlyList<IOperationProvider> operationOverrides, string? variableFormatString)
        {
            _overrides = operationOverrides;
            VariableFormatString = variableFormatString ?? "{0}";
        }

        public string VariableFormatString { get; }

        public bool TryGetTargetRelPath(string sourceRelPath, out string? targetRelPath)
        {
            targetRelPath = null;
            return false;
        }

        public IReadOnlyList<IOperationProvider> GetOperations(IReadOnlyList<IOperationProvider> sourceOperations)
        {
            return _overrides ?? sourceOperations;
        }
    }
}
