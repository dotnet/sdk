using System.Collections.Generic;

namespace Mutant.Chicken.Abstractions
{
    public abstract class TemplateSourceFolder : ITemplateSourceEntry
    {
        public TemplateSourceEntryKind Kind => TemplateSourceEntryKind.Folder;

        public abstract string Name { get; }

        public abstract IEnumerable<ITemplateSourceEntry> Children { get; }

        public abstract string FullPath { get; }
    }
}