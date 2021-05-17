// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Orchestrator.RunnableProjects;

namespace Microsoft.TemplateEngine.Utils
{
    internal class SimpleConfigModifiers : ISimpleConfigModifiers
    {
        internal SimpleConfigModifiers (string baseline)
        {
            BaselineName = baseline;
        }

        public string BaselineName { get; }
    }
}
