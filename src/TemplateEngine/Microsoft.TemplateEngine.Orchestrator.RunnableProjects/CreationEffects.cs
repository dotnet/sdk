using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class CreationEffects : ICreationEffects
    {
        public IReadOnlyList<IFileChange> FileChanges { get; set; }

        public ICreationResult CreationResult { get; set; }
    }

    internal class CreationEffects2 : ICreationEffects, ICreationEffects2
    {
        public IReadOnlyList<IFileChange2> FileChanges { get; set; }

        IReadOnlyList<IFileChange> ICreationEffects.FileChanges => FileChanges;

        public ICreationResult CreationResult { get; set; }
    }
}
