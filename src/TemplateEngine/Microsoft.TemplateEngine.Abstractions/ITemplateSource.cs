namespace Microsoft.TemplateEngine.Abstractions
{
    public interface ITemplateSource : IComponent
    {
        IDisposable<ITemplateSourceFolder> RootFor(string location);

        /// <summary>
        /// This is called when reading an embedded template source root
        /// </summary>
        /// <param name="source">The parent source</param>
        /// <param name="location">The location relative to the root of the parent source</param>
        /// <returns>The root of the embedded source</returns>
        IDisposable<ITemplateSourceFolder> RootFor(IConfiguredTemplateSource source, string location);

        bool CanHandle(string location);

        /// <summary>
        /// This is called during source embedding to determine whether the embedded file entry can be handled by the embeddable source
        /// </summary>
        /// <param name="source">The parent source</param>
        /// <param name="location">The location relative to the source to check</param>
        /// <returns><see langword="true"/> if this source may be embedded for the specified location, <see langword="false"/> otherwise</returns>
        bool CanHandle(IConfiguredTemplateSource source, string location);

        bool IsEmbeddable { get; }

        bool CanHostEmbeddedSources { get; }
    }
}
