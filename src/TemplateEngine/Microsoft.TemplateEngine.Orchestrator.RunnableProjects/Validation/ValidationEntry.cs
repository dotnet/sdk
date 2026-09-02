// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Validation
{
    internal class ValidationEntry : IValidationEntry
    {
        public ValidationEntry(IValidationEntry.SeverityLevel severity, string code, string errorMessage)
        {
            Severity = severity;
            Code = code;
            ErrorMessage = errorMessage;
        }

        public IValidationEntry.SeverityLevel Severity { get; }

        public string Code { get; }

        public string ErrorMessage { get; }

        public IValidationEntry.ErrorLocation? Location { get; set; }
    }
}
