// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockInstallerFactory : IInstallerFactory
    {
        private Guid _factoryId = new Guid("00000000-0000-0000-0000-000000000000");
        public string Name => "MockInstallerFactory";

        public Guid Id => _factoryId;

        public IInstaller CreateInstaller(IManagedTemplatePackageProvider provider, IEngineEnvironmentSettings settings, string installPath) => throw new NotImplementedException();
    }
}
