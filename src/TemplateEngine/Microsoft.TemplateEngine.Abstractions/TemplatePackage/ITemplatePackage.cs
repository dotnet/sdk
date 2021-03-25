using System;

namespace Microsoft.TemplateEngine.Abstractions.TemplatePackage
{
    /// <summary>
    /// Templates source is folder, .nupkg or other container that can contain single or multiple templates.
    /// <seealso cref="ITemplatePackageProvider"/> for more information.
    /// </summary>
    public interface ITemplatePackage
    {
        /// <summary>
        /// To avoid scanning for changes every time. TemplateEngine is caching templates from
        /// template source, this timestamp is used to invalidate content and re-scan this templates source.
        /// </summary>
        DateTime LastChangeTime { get; }

        /// <summary>
        /// This can be full Uri like file://, http:// or simply file path
        /// </summary>
        string MountPointUri { get; }

        /// <summary>
        /// This is provider that created this source.
        /// This is mostly helper for grouping sources by provider
        /// so caller doesn't need to keep track of provider->source relation.
        /// </summary>
        ITemplatePackageProvider Provider { get; }
    }
}
