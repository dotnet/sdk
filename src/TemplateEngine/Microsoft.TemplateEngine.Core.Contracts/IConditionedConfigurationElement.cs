// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface IConditionedConfigurationElement
    {
        string Condition { get; }

        bool ConditionResult { get; }

        void EvaluateCondition(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables);
    }
}
