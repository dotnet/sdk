using System.Collections.Generic;
using System.IO;

namespace Mutant.Chicken.Abstractions
{
    public interface ITemplateSourceEntry
    {
        string Name { get; }

        string FullPath { get; }

        TemplateSourceEntryKind Kind { get; }

        ITemplateSourceFolder Parent { get; }

        string PathRelativeTo(ITemplateSourceEntry source);
    }

    public interface ITemplateSourceFolder : ITemplateSourceEntry
    {
        IEnumerable<ITemplateSourceEntry> Children { get; }

        IEnumerable<ITemplateSourceFile> EnumerateFiles(string pattern, SearchOption searchOption);

        ITemplateSourceFolder GetDirectoryAtRelativePath(string source);
    }

    public interface ITemplateSourceFile : ITemplateSourceEntry
    {
        Stream OpenRead();
    }
}