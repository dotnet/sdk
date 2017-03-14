namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface IPathMatcher
    {
        string Pattern { get; }

        bool IsMatch(string path);
    }
}
