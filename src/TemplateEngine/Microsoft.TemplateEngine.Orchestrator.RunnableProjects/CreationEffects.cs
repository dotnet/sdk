using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class CreationEffects : ICreationEffects
    {
        public IReadOnlyList<IFileChange> FileChanges { get; set; }

        public ICreationResult CreationResult { get; set; }
    }

    public class CreationEffects2 : ICreationEffects, ICreationEffects2
    {
        public IReadOnlyList<IFileChange2> FileChanges { get; set; }

        IReadOnlyList<IFileChange> ICreationEffects.FileChanges => FileChanges;

        public ICreationResult CreationResult { get; set; }
    }
}
