// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal interface IPostActionModel : IConditionedConfigurationElement
    {
        string Description { get; }

        Guid ActionId { get; }

        bool ContinueOnError { get; }

        IReadOnlyDictionary<string, string> Args { get; }

        IReadOnlyList<KeyValuePair<string, string>> ManualInstructionInfo { get; }

        string ConfigFile { get; }
    }
}
