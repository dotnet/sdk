using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Mutant.Chicken.Abstractions
{
    public abstract class TemplateSourceEntry : ITemplateSourceEntry
    {
        protected TemplateSourceEntry(ITemplateSourceFolder parent)
        {
            Parent = parent;
        }

        public abstract string Name { get; }

        public abstract string FullPath { get; }

        public abstract TemplateSourceEntryKind Kind { get; }

        public ITemplateSourceFolder Parent { get; }

        public string PathRelativeTo(ITemplateSourceEntry source)
        {
            //The path should be relative to either source itself (in the case that it's a folder) or the parent of source)
            ITemplateSourceFolder relTo = source as ITemplateSourceFolder ?? source.Parent;

            //If the thing to be relative to is the root (or a file in the root), just use the full path of the item
            if (relTo == null)
            {
                return FullPath;
            }

            //Get all the path segments for the thing we're relative to
            Dictionary<ITemplateSourceFolder, int> sourceSegments = new Dictionary<ITemplateSourceFolder, int> { { relTo, 0 } };
            ITemplateSourceFolder current = relTo.Parent;
            int index = 0;
            while (current != null)
            {
                sourceSegments[current] = ++index;
                current = current.Parent;
            }

            current = Parent;
            List<string> segments = new List<string> { Name };

            //Walk back the set of parents of this item until one is contained by our source, building up a list as we go
            int revIndex = 0;
            while (current != null && !sourceSegments.TryGetValue(current, out revIndex))
            {
                segments.Insert(0, current.Name);
            }

            //Now that we've found our common point (and the index of the common segment _from the end_ of the source's parent chain)
            //  the number of levels up we need to go is the value of revIndex
            segments.InsertRange(0, Enumerable.Repeat("..", revIndex));
            return string.Join("\\", segments);
        }
    }

    public abstract class TemplateSourceFile : TemplateSourceEntry, ITemplateSourceFile
    {
        protected TemplateSourceFile(ITemplateSourceFolder sourceFolder)
            : base(sourceFolder)
        {
        }
        
        public override TemplateSourceEntryKind Kind => TemplateSourceEntryKind.File;

        public abstract Stream OpenRead();
    }
}