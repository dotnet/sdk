using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions.GlobalSettings;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.TemplatePackages;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface ISettingsLoader
    {
        IComponentManager Components { get; }

        IEngineEnvironmentSettings EnvironmentSettings { get; }

        IGlobalSettings GlobalSettings { get; }

        ITemplatePackagesManager TemplatePackagesManager { get; }

        void AddProbingPath(string probeIn);

        Task<IReadOnlyList<ITemplateInfo>> GetTemplatesAsync(CancellationToken token);

        ITemplate LoadTemplate(ITemplateInfo info, string baselineName);

        void Save();

        bool TryGetFileFromIdAndPath(string mountPointUri, string filePathInsideMount, out IFile file, out IMountPoint mountPoint);

        bool TryGetMountPoint(string mountPointUri, out IMountPoint mountPoint);

        IFile FindBestHostTemplateConfigFile(IFileSystemInfo config);

        Task RebuildCacheFromSettingsIfNotCurrent(bool forceRebuild);
    }
}
