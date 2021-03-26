// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Abstractions.TemplatePackage
{
    /// <summary>
    /// Factory responsible for creating <see cref="ITemplatePackageProvider"/> or <see cref="IManagedTemplatePackageProvider"/>.
    /// This is registered with <see cref="IComponentManager"/> either via <see cref="IComponentManager.Register(System.Type)"/> or
    /// <see cref="ITemplateEngineHost.BuiltInComponents"/>.
    /// </summary>
    public interface ITemplatePackageProviderFactory : IIdentifiedComponent
    {
        /// <summary>
        /// Gets the readable name of the factory.
        /// This is used for two reasons:
        ///     1) To show user which provider is source of template packages (when debug/verbose flag is set)
        ///     2) To allow the user to pick specific provider to install templates to.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Creates new provider with specified environment, the provider may also implement <see cref="IManagedTemplatePackageProvider"/>.
        /// </summary>
        ITemplatePackageProvider CreateProvider(IEngineEnvironmentSettings settings);
    }
}
