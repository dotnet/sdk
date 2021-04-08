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
