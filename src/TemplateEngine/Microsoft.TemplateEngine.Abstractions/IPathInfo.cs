namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IPathInfo
    {
        string UserProfileDir { get; }

        string BaseDir { get; }
    }
}
