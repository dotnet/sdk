namespace Mutant.Chicken.Runner
{
    public interface IPathMatcher
    {
        string Pattern { get; }

        bool IsMatch(string path);
    }
}