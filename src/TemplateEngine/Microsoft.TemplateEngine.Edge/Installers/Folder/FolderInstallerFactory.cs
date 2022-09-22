// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;

namespace Microsoft.TemplateEngine.Edge.Installers.Folder
{
    public sealed class FolderInstallerFactory : IInstallerFactory
    {
        internal static readonly Guid FactoryId = new("{F01DEA33-E89C-46D1-89C2-1CA1F394C5AA}");

        Guid IIdentifiedComponent.Id => FactoryId;

        string IInstallerFactory.Name => "Folder";

        IInstaller IInstallerFactory.CreateInstaller(IEngineEnvironmentSettings settings, string installPath)
        {
            return new FolderInstaller(settings, this);
        }
    }
}
