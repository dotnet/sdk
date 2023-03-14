// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Validation
{
    internal class MultiValidationCheck<T> : BaseValidationCheck
    {
        internal MultiValidationCheck(string code, IValidationEntry.SeverityLevel severityLevel, Func<ITemplateValidationInfo, T, string> getDescription, Func<ITemplateValidationInfo, IEnumerable<T>> invalidElements)
        : base(code, severityLevel)
        {
            GetDescription = getDescription;
            InvalidElements = invalidElements;
        }

        internal Func<ITemplateValidationInfo, T, string> GetDescription { get; }

        internal Func<ITemplateValidationInfo, IEnumerable<T>> InvalidElements { get; }

        public override void Process(ITemplateValidationInfo validationInfo)
        {
            InvalidElements(validationInfo).ForEach(ie => AddValidationError(validationInfo, GetDescription(validationInfo, ie)));
        }
    }
}
