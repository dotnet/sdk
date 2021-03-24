// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions.TemplatePackages;

namespace Microsoft.TemplateEngine.Abstractions.Installer
{
    public class CheckUpdateResult : Result
    {
        public string LatestVersion { get; private set; }

        public bool IsLatestVersion { get; private set; }

        public static CheckUpdateResult CreateSuccessNoUpdate(IManagedTemplatePackage source)
        {
            return new CheckUpdateResult()
            {
                Error = InstallerErrorCode.Success,
                Source = source,
                LatestVersion = source.Version
            };
        }

        public static CheckUpdateResult CreateSuccess(IManagedTemplatePackage source, string version, bool isLatest)
        {
            return new CheckUpdateResult()
            {
                Error = InstallerErrorCode.Success,
                Source = source,
                LatestVersion = version,
                IsLatestVersion = isLatest,
            };
        }

        public static CheckUpdateResult CreateFailure(IManagedTemplatePackage source, InstallerErrorCode error, string localizedFailureMessage)
        {
            return new CheckUpdateResult()
            {
                Error = error,
                ErrorMessage = localizedFailureMessage,
                Source = source
            };
        }
    }
}
