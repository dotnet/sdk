using System.Collections.Generic;
using Minimatch;
using Mutant.Chicken.Runner;

namespace Mutant.Chicken.Demo
{
    internal class MinimatchShim : IPathMatcher
    {
        private readonly Minimatcher _matcher;
        private static readonly Dictionary<string, IPathMatcher> Lookup = new Dictionary<string, IPathMatcher>();

        private MinimatchShim(string value)
        {
            Pattern = value;
            _matcher = new Minimatcher(value, new Options
            {
                AllowWindowsPaths = true,
                IgnoreCase = true
            });
        }

        public string Pattern { get; }

        public bool IsMatch(string path)
        {
            return _matcher.IsMatch(path);
        }

        public static IPathMatcher Get(string value)
        {
            IPathMatcher matcher;
            if (!Lookup.TryGetValue(value, out matcher))
            {
                matcher = Lookup[value] = new MinimatchShim(value);
            }

            return matcher;
        }
    }
}