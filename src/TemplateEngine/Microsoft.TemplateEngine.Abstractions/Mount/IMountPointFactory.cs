using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions.Mount
{
    /// <summary>
    /// Implement this factory and register it with <see cref="IComponentManager"/>.
    /// When template engine will get Uri to mount it will call <see cref="TryMount(IEngineEnvironmentSettings, IMountPoint, string, out IMountPoint)"/>
    /// on all factories and use mount point from first successful factory.
    /// </summary>
    public interface IMountPointFactory : IIdentifiedComponent
    {
        /// <summary>
        /// Tries to mount specified Uri.
        /// </summary>
        /// <param name="environmentSettings">Environment to be used.</param>
        /// <param name="parent">Mount points can be mounted inside each other. Pass in parent or <c>null</c>.</param>
        /// <param name="mountPointUri">Valid <see cref="System.Uri"/> that represents mount point.</param>
        /// <param name="mountPoint">Resulting mount point.</param>
        /// <returns><c>true</c> if mount point was successfully mounted.</returns>
        bool TryMount(IEngineEnvironmentSettings environmentSettings, IMountPoint parent, string mountPointUri, out IMountPoint mountPoint);
    }
}
