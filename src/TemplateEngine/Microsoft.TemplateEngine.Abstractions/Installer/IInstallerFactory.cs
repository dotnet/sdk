// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Abstractions.Installer
{
    /// <summary>
    /// Template package installer factory interface.
    /// Implement this interface to register installer supported by built-in providers.
    /// </summary>
    public interface IInstallerFactory : IIdentifiedComponent
    {
        /// <summary>
        /// The factory name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Creates the <see cref="IInstaller"/> managed by this factory.
        /// </summary>
        /// <param name="settings">environment settings of the template engine host.</param>
        /// <param name="installPath">path to install the template packages to.</param>
        /// <returns>created <see cref="IInstaller"/>.</returns>
        IInstaller CreateInstaller(IEngineEnvironmentSettings settings, string installPath);
    }
}
