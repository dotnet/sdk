using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Mutant.Chicken.Abstractions
{
    public abstract class TemplateSourceFolder : TemplateSourceEntry, ITemplateSourceFolder
    {
        public override TemplateSourceEntryKind Kind => TemplateSourceEntryKind.Folder;

        protected TemplateSourceFolder(ITemplateSourceFolder parent)
            : base(parent)
        {
        }

        public abstract IEnumerable<ITemplateSourceEntry> Children { get; }

        public virtual IEnumerable<ITemplateSourceFile> EnumerateFiles(string pattern, SearchOption searchOption)
        {
            foreach (ITemplateSourceEntry child in Children)
            {
                if (child.Kind == TemplateSourceEntryKind.File)
                {
                    ITemplateSourceFile childFile = (ITemplateSourceFile) child;
                    if (IsMatch(childFile, pattern))
                    {
                        yield return childFile;
                    }
                }
                else
                {
                    if (searchOption == SearchOption.AllDirectories)
                    {
                        foreach (ITemplateSourceFile file in ((ITemplateSourceFolder) child).EnumerateFiles(pattern, SearchOption.AllDirectories))
                        {
                            yield return file;
                        }
                    }
                }
            }
        }

        protected virtual bool IsMatch(ITemplateSourceFile entry, string pattern)
        {
            string rx = Regex.Escape(pattern);
            rx = rx.Replace("\\*", ".*").Replace("\\?", ".?");
            Regex r = new Regex(rx);
            return r.IsMatch(entry.Name);
        }
    }
}