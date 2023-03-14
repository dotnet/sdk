// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Validation
{
    internal class ActionValidationCheck : BaseValidationCheck
    {
        internal ActionValidationCheck(string code, IValidationEntry.SeverityLevel severityLevel, Func<ITemplateValidationInfo, string> getDescription, Func<ITemplateValidationInfo, bool> action)
            : base(code, severityLevel)
        {
            GetDescription = getDescription;
            Action = action;
        }

        internal Func<ITemplateValidationInfo, string> GetDescription { get; init; }

        internal Func<ITemplateValidationInfo, bool> Action { get; init; }

        public override void Process(ITemplateValidationInfo validationInfo)
        {
            if (Action(validationInfo))
            {
                AddValidationError(validationInfo, GetDescription(validationInfo));
            }
        }
    }
}
