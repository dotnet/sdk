using System;
using System.IO;
using Microsoft.TemplateEngine.Abstractions.Runner;

namespace Microsoft.TemplateEngine.Orchestrator.VsTemplates
{
    internal class ExtensionPathMatcher : IPathMatcher
    {
        private readonly string _extension;

        public ExtensionPathMatcher(string extension)
        {
            _extension = extension;
        }

        public string Pattern => _extension;

        public bool IsMatch(string path)
        {
            return string.Equals(Path.GetExtension(path), _extension, StringComparison.OrdinalIgnoreCase);
        }
    }
}