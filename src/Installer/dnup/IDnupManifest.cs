using System.Collections.Generic;

namespace Microsoft.DotNet.Tools.Bootstrapper
{
    internal interface IDnupManifest
    {
        IEnumerable<DotnetInstall> GetInstalledVersions();
        void AddInstalledVersion(DotnetInstall version);
        void RemoveInstalledVersion(DotnetInstall version);
    }
}
