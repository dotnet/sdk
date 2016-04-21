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
}