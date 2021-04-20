// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Localization
{
    internal class HostTemplateModel : IHostTemplateModel
    {
        public IReadOnlyDictionary<string, string> ParameterMap { get; set; }
    }
}
