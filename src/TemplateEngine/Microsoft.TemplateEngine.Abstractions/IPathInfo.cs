namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Contains common folder paths used be TemplateEngine.
    /// </summary>
    public interface IPathInfo
    {
        /// <summary>
        /// The user's profile folder.
        /// E.g: "/home/David/" on Unix and "C:\Users\David\" on Windows.
        /// </summary>
        string UserProfileDir { get; }

        /// <summary>
        /// Root of TemplateEngine shared between all hosts.
        /// Usually at "C:\Users\David\.templateengine\".
        /// </summary>
        string TemplateEngineRootDir { get; }

        /// <summary>
        /// Root of specific host shared between all versions.
        /// E.g.: "C:\Users\David\.templateengine\dotnet\".
        /// </summary>
        string TemplateEngineHostDir { get; }

        /// <summary>
        /// Folder where specific host with specific version stores cache and other files.
        /// E.g.: "C:\Users\David\.templateengine\dotnet\v1.0.0.0\".
        /// </summary>
        string TemplateEngineHostVersionDir { get; }
    }
}
