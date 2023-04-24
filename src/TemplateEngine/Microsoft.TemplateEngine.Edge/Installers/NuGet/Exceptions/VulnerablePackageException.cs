// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Installer;

namespace Microsoft.TemplateEngine.Edge.Installers.NuGet
{
    internal class VulnerablePackageException : Exception
    {
        public VulnerablePackageException(string message, string packageIdentifier, string packageVersion, IReadOnlyList<VulnerabilityInfo> vulnerabilities)
            : base(message)
        {
            PackageIdentifier = packageIdentifier;
            Vulnerabilities = vulnerabilities;
            PackageVersion = packageVersion;
        }

        public IReadOnlyList<VulnerabilityInfo> Vulnerabilities { get; internal set; }

        public string PackageIdentifier { get; internal set; }

        public string PackageVersion { get; private set; }
    }
}
