// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.TemplateEngine.Edge.Installers.NuGet
{
    internal class InvalidNuGetPackageException : Exception
    {
        public InvalidNuGetPackageException(string packagePath) : base($"The NuGet package {packagePath} is invalid.")
        {
            PackageLocation = packagePath;
        }

        public InvalidNuGetPackageException(string packagePath, Exception innerException) : base($"The NuGet package {packagePath} is invalid.", innerException)
        {
            PackageLocation = packagePath;
        }

        public string PackageLocation { get; private set; }
    }
}
