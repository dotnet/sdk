// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Validation
{
    internal abstract class BaseValidationCheck : IValidationCheck
    {
        protected BaseValidationCheck(string code, IValidationEntry.SeverityLevel severity)
        {
            Code = code;
            Severity = severity;
        }

        public string Code { get; }

        public IValidationEntry.SeverityLevel Severity { get; }

        public abstract void Process(ITemplateValidationInfo validationInfo);

        public void AddValidationError(ITemplateValidationInfo validationInfo, string errorMessage)
        {
            validationInfo.AddValidationError(new ValidationEntry(Severity, Code, errorMessage));
        }
    }
}
