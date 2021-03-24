namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IPathInfo
    {
        string UserProfileDir { get; }

        string TemplateEngineRootDir { get; }

        string BaseDir { get; }
    }
}
