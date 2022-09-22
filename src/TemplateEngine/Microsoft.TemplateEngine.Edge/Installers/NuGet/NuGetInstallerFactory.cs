// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;

namespace Microsoft.TemplateEngine.Edge.Installers.NuGet
{
    public sealed class NuGetInstallerFactory : IInstallerFactory
    {
        internal static readonly Guid FactoryId = new("{015DCBAC-B4A5-49EA-94A6-061616EB60E2}");

        Guid IIdentifiedComponent.Id => FactoryId;

        string IInstallerFactory.Name => "NuGet";

        IInstaller IInstallerFactory.CreateInstaller(IEngineEnvironmentSettings settings, string installPath)
        {
            return new NuGetInstaller(this, settings, installPath);
        }
    }
}
