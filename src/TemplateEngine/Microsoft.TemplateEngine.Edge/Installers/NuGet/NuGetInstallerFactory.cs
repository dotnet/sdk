// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.Edge.Installers.NuGet
{
    internal class NuGetInstallerFactory : IInstallerFactory
    {
        public static readonly Guid FactoryId = new Guid("{015DCBAC-B4A5-49EA-94A6-061616EB60E2}");

        public Guid Id => FactoryId;

        public string Name => "NuGet";

        public IInstaller CreateInstaller(IEngineEnvironmentSettings settings, string installPath)
        {
            return new NuGetInstaller(this, settings, installPath);
        }
    }
}
