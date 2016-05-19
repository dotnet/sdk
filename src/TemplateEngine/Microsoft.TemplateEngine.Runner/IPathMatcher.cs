namespace Microsoft.TemplateEngine.Runner
{
    public interface IPathMatcher
    {
        string Pattern { get; }

        bool IsMatch(string path);
    }
}