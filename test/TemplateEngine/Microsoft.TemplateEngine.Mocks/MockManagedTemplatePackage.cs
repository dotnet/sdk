// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockManagedTemplatePackage : IManagedTemplatePackage
    {
        public string DisplayName => throw new NotImplementedException();

        public string Identifier => throw new NotImplementedException();

        public IInstaller Installer => throw new NotImplementedException();

        public IManagedTemplatePackageProvider ManagedProvider => throw new NotImplementedException();

        public string Version => throw new NotImplementedException();

        public DateTime LastChangeTime => throw new NotImplementedException();

        public string MountPointUri => throw new NotImplementedException();

        public ITemplatePackageProvider Provider => throw new NotImplementedException();

        public IReadOnlyDictionary<string, string> GetDetails() => throw new NotImplementedException();
    }
}
