using System;
using System.IO;
using Mutant.Chicken.Runner;

namespace Mutant.Chicken.Orchestrator.VsTemplates
{
    internal class ExtensionPathMatcher : IPathMatcher
    {
        private readonly string _extension;

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