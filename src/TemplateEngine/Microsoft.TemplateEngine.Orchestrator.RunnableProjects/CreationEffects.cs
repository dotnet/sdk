using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class CreationEffects : ICreationEffects
    {
        public IReadOnlyList<IFileChange> FileChanges { get; set; }

        public ICreationResult CreationResult { get; set; }
    }
}
