using System.IO;

namespace Mutant.Chicken.Abstractions
{
    public abstract class TemplateSourceFile : ITemplateSourceEntry
    {
        public abstract string FullPath { get; }

        public TemplateSourceEntryKind Kind => TemplateSourceEntryKind.File;

        public abstract string Name { get; }

        public abstract Stream OpenRead();
    }
}