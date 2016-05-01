using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public ITemplateSourceFolder GetDirectoryAtRelativePath(string source)
        {
            int sepIndex = source.IndexOfAny(new[] { '\\', '/' });
            if (sepIndex < 0)
            {
                switch (source)
                {
                    case "":
                    case ".":
                        return this;
                    case "..":
                        return Parent;
                    default:
                        return (ITemplateSourceFolder)Children.FirstOrDefault(x => string.Equals(x.Name, source, StringComparison.OrdinalIgnoreCase));
                }
            }

            string part = source.Substring(0, sepIndex);
            ITemplateSourceFolder current = this;
            while(sepIndex > -1)
            {
                switch (part)
                {
                    case "":
                    case ".":
                        break;
                    case "..":
                        current = current.Parent;
                        break;
                    default:
                        current = (ITemplateSourceFolder)current.Children.FirstOrDefault(x => string.Equals(x.Name, source, StringComparison.OrdinalIgnoreCase));
                        break;
                }

                int start = sepIndex + 1;
                sepIndex = source.IndexOfAny(new[] { '\\', '/' }, start);

                if (sepIndex == -1)
                {
                    part = source.Substring(start);
                    switch (part)
                    {
                        case "":
                        case ".":
                            break;
                        case "..":
                            current = current.Parent;
                            break;
                        default:
                            current = (ITemplateSourceFolder)current.Children.FirstOrDefault(x => string.Equals(x.Name, source, StringComparison.OrdinalIgnoreCase));
                            break;
                    }
                }
                else
                {
                    part = source.Substring(start, sepIndex - start);
                }
            }

            return current;
        }
    }
}