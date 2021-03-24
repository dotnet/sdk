// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions.TemplatePackages;

namespace Microsoft.TemplateEngine.Abstractions.Installer
{
    public class UpdateResult : Result
    {
        public UpdateRequest UpdateRequest { get; private set; }

        public static UpdateResult CreateSuccess(UpdateRequest request, IManagedTemplatePackage source)
        {
            return new UpdateResult()
            {
                UpdateRequest = request,
                Error = InstallerErrorCode.Success,
                Source = source
            };
        }

        public static UpdateResult CreateFailure(UpdateRequest request, InstallerErrorCode error, string localizedFailureMessage)
        {
            return new UpdateResult()
            {
                UpdateRequest = request,
                Error = error,
                ErrorMessage = localizedFailureMessage
            };
        }

        public static UpdateResult FromInstallResult(UpdateRequest request, InstallResult installResult)
        {
            return new UpdateResult()
            {
                UpdateRequest = request,
                Source = installResult.Source,
                Error = installResult.Error,
                ErrorMessage = installResult.ErrorMessage
            };
        }
    }
}
