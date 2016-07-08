namespace Microsoft.TemplateEngine.Abstractions.Runner
{
    public interface IPathMatcher
    {
        string Pattern { get; }

        bool IsMatch(string path);
    }
}