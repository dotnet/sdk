namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Contains common folder paths used by TemplateEngine.
    /// </summary>
    public interface IPathInfo
    {
        /// <summary>
        /// The user profile folder.
        /// E.g: "/home/userName/" on Unix and "C:\Users\userName\" on Windows.
        /// </summary>
        string UserProfileDir { get; }

        /// <summary>
        /// Root of TemplateEngine shared between all hosts.
        /// Usually at "C:\Users\userName\.templateengine\".
        /// </summary>
        string TemplateEngineRootDir { get; }

        /// <summary>
        /// Root of specific host shared between all versions.
        /// E.g.: "C:\Users\userName\.templateengine\dotnet\".
        /// </summary>
        string TemplateEngineHostDir { get; }

        /// <summary>
        /// Folder where specific host with specific version stores cache and other files.
        /// E.g.: "C:\Users\userName\.templateengine\dotnet\v1.0.0.0\".
        /// </summary>
        string TemplateEngineHostVersionDir { get; }
    }
}
