// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using NuGet.Versioning;

namespace Microsoft.TemplateEngine.Edge.Installers.NuGet
{
    internal class DownloadException : Exception
    {
        public DownloadException(string packageIdentifier, string packageVersion, string filePath) : base($"Failed to download {packageIdentifier}::{packageVersion} from {filePath}")
        {
            PackageIdentifier = packageIdentifier;
            PackageVersion = packageVersion;
            PackageLocation = filePath;
        }

        public DownloadException(string packageIdentifier, string packageVersion, IEnumerable<string> attemptedSources) : base($"Failed to download {packageIdentifier}::{packageVersion} from NuGet feeds {string.Join(";", attemptedSources)}")
        {
            PackageIdentifier = packageIdentifier;
            PackageVersion = packageVersion;
            SourcesList = attemptedSources;
        }

        public DownloadException(string packageIdentifier, string packageVersion, IEnumerable<string> attemptedSources, Exception inner) : base($"Failed to download {packageIdentifier}::{packageVersion} from NuGet feeds {string.Join(";", attemptedSources)}", inner)
        {
            PackageIdentifier = packageIdentifier;
            PackageVersion = packageVersion;
            SourcesList = attemptedSources;
        }

        public string PackageIdentifier { get; private set; }
        public string PackageLocation { get; private set; }
        public string PackageVersion { get; private set; }
        public IEnumerable<string> SourcesList { get; private set; }
    }
}
