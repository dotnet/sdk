// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.Abstractions.Installer
{
    /// <summary>
    /// Installer needs to implement this interface if installations to be done using built-in providers.
    /// <see cref="TemplatePackageData"/> is used to store information about installed template packages.
    /// </summary>
    public interface ISerializableInstaller
    {
        /// <summary>
        /// Deserializes <see cref="TemplatePackageData"/> to <see cref="IManagedTemplatePackage"/> that can be processed by <see cref="ISerializableInstaller"/>.
        /// </summary>
        /// <param name="provider">The provider that provides the data.</param>
        /// <param name="data">Data to serialize.</param>
        /// <returns>deserialized <see cref="IManagedTemplatePackage"/>.</returns>
        IManagedTemplatePackage Deserialize(IManagedTemplatePackageProvider provider, TemplatePackageData data);

        /// <summary>
        /// Serializes <see cref="IManagedTemplatePackage"/> to <see cref="TemplatePackageData"/>.
        /// </summary>
        /// <param name="templatePackage">template package to serialize. </param>
        /// <returns>serialized <see cref="TemplatePackageData"/>.</returns>
        TemplatePackageData Serialize(IManagedTemplatePackage templatePackage);
    }
}
