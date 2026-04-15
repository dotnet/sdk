// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Validation
{
    internal interface ITemplateValidator
    {
        ITemplateValidatorFactory Factory { get; }

        void ValidateTemplate(ITemplateValidationInfo templateValidationInfo);
    }
}
