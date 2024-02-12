// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader
{
    internal class NuGetPackageInstallerException : GracefulException
    {
        public NuGetPackageInstallerException()
        {
        }

        public NuGetPackageInstallerException(string message) : base(message)
        {
        }

        public NuGetPackageInstallerException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    internal class NuGetPackageNotFoundException : NuGetPackageInstallerException
    {
        public NuGetPackageNotFoundException()
        {
        }

        public NuGetPackageNotFoundException(string message) : base(message)
        {
        }

        public NuGetPackageNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
