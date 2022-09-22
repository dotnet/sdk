// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class FirstUpperCaseInvariantValueFormFactory : ActionableValueFormFactory
    {
        internal const string FormIdentifier = "firstUpperCaseInvariant";

        internal FirstUpperCaseInvariantValueFormFactory() : base(FormIdentifier) { }

        protected override string Process(string value)
        {
            return value switch
            {
                "" => value,
                _ => value.First().ToString().ToUpperInvariant() + value.Substring(1),
            };
        }
    }
}
