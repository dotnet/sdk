// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.TemplateEngine.Edge.Installers.Folder
{
    internal class FolderInstallerFactory : IInstallerFactory
    {
        public static readonly Guid FactoryId = new Guid("{F01DEA33-E89C-46D1-89C2-1CA1F394C5AA}");

        public Guid Id => FactoryId;

        public string Name => "Folder";

        public IInstaller CreateInstaller(IEngineEnvironmentSettings settings, string installPath)
        {
            return new FolderInstaller(settings, this);
        }
    }
}
