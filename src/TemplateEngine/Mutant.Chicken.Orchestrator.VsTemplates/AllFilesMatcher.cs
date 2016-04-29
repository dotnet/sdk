using System;
using Mutant.Chicken.Runner;

namespace Mutant.Chicken.Orchestrator.VsTemplates
{
    internal class AllFilesMatcher : IPathMatcher
    {
        public string Pattern => null;

        public bool IsMatch(string path)
        {
            return true;
        }
    }
}