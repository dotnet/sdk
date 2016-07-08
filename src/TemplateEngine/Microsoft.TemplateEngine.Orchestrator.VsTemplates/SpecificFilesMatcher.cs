using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Runner;

namespace Microsoft.TemplateEngine.Orchestrator.VsTemplates
{
    internal class SpecificFilesMatcher : IPathMatcher
    {
        private readonly HashSet<string> _files;

        public SpecificFilesMatcher(IEnumerable<string> files)
        {
            _files = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
        }

        public string Pattern => string.Join(",", _files);

        public bool IsMatch(string path)
        {
            return _files.Contains(path);
        }
    }
}