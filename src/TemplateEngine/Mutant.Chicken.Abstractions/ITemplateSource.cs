using System.Collections.Generic;
using System.IO;

namespace Mutant.Chicken.Abstractions
{
    public interface ITemplateSource : IComponent
    {
        IEnumerable<ITemplateSourceEntry> EntriesIn(string location);

        bool CanHandle(string location);
    }

    public interface IConfiguredTemplateSource
    {
        ITemplateSource Source { get; }

        IEnumerable<ITemplateSourceEntry> Entries { get; }

        string Alias { get; }

        string Location { get; }
    }

    public interface ITemplateSourceEntry
    {
        string Name { get; }

        string FullPath { get; }

        TemplateSourceEntryKind Kind { get; }
    }

    public enum TemplateSourceEntryKind
    {
        File,
        Folder
    }

    public abstract class TemplateSourceFolder : ITemplateSourceEntry
    {
        public TemplateSourceEntryKind Kind => TemplateSourceEntryKind.Folder;

        public abstract string Name { get; }

        public abstract IEnumerable<ITemplateSourceEntry> Children { get; }

        public abstract string FullPath { get; }
    }

    public abstract class TemplateSourceFile : ITemplateSourceEntry
    {
        public abstract string FullPath { get; }

        public TemplateSourceEntryKind Kind => TemplateSourceEntryKind.File;

        public abstract string Name { get; }

        public abstract Stream OpenRead();
    }
}
