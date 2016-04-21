using System;
using System.IO;
using Mutant.Chicken.Runner;

namespace Mutant.Chicken.Orchestrator.VsTemplates
{
    internal class AllFilesMatcher : IPathMatcher
    {
        public bool IsMatch(string path)
        {
            return true;
        }
    }

    internal class ExtensionPathMatcher : IPathMatcher
    {
        private string _extension;

        public ExtensionPathMatcher(string extension)
        {
            _extension = extension;
        }

        public bool IsMatch(string path)
        {
            return string.Equals(Path.GetExtension(path), _extension, StringComparison.OrdinalIgnoreCase);
        }
    }
}