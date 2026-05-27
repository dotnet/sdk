// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Validation
{
    internal interface IValidationCheck
    {
        abstract string Code { get; }

        abstract IValidationEntry.SeverityLevel Severity { get; }

        void Process(ITemplateValidationInfo validationInfo);
    }
}
