using Microsoft.TemplateEngine.Abstractions.TemplatePackages;

namespace Microsoft.TemplateEngine.Abstractions.Installer
{
    public class UninstallResult : Result
    {
        public static UninstallResult CreateSuccess(IManagedTemplatePackage source)
        {
            return new UninstallResult()
            {
                Error = InstallerErrorCode.Success,
                Source = source
            };
        }

        public static UninstallResult CreateFailure(IManagedTemplatePackage source, InstallerErrorCode code, string localizedFailureMessage)
        {
            return new UninstallResult()
            {
                Source = source,
                Error = code,
                ErrorMessage = localizedFailureMessage
            };
        }
    }
}
