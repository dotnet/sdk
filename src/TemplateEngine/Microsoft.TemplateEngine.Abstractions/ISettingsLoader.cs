// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface ISettingsLoader
    {
        [Obsolete("Use IEngineEnvironmentSettings.Components")]
        IComponentManager Components { get; }

        [Obsolete("Use IEngineEnvironmentSettings directly.")]
        IEngineEnvironmentSettings EnvironmentSettings { get; }

        [Obsolete("Not possible anymore.")]
        IEnumerable<object> MountPoints { get; }

        [Obsolete("Not possible anymore.")]
        void AddMountPoint(IMountPoint mountPoint);

        [Obsolete("Probing paths need to be handled by ComponentManager itself.")]
        void AddProbingPath(string probeIn);

        [Obsolete("Use new TemplatePackagesManager().GetTemplatesAsync.")]
        void GetTemplates(HashSet<ITemplateInfo> templates);

        [Obsolete("Use Microsoft.TemplateEngine.Utils.TemplateInfoExtensions.LoadTemplate extension.")]
        ITemplate LoadTemplate(ITemplateInfo info, string baselineName);

        [Obsolete("No need to call Save anymore.")]
        void Save();

        [Obsolete("Use Microsoft.TemplateEngine.Utils.EngineEnvironmentSettingsExtensions.TryGetMountPoint extension and then look for file inside mountpoint..")]
        bool TryGetFileFromIdAndPath(Guid mountPointId, string place, out IFile file, out IMountPoint mountPoint);

        [Obsolete("Use Microsoft.TemplateEngine.Utils.EngineEnvironmentSettingsExtensions.TryGetMountPoint extension.")]
        bool TryGetMountPointFromPlace(string mountPointPlace, out IMountPoint mountPoint);

        [Obsolete("Use Microsoft.TemplateEngine.Utils.EngineEnvironmentSettingsExtensions.TryGetMountPoint extension.")]
        bool TryGetMountPointInfo(Guid mountPointId, out object info);

        [Obsolete("Should be handled by TemplatePackageManager itself.")]
        void WriteTemplateCache(IList<ITemplateInfo> templates, string locale);

        [Obsolete("Should be handled by TemplatePackageManager itself.")]
        void WriteTemplateCache(IList<ITemplateInfo> templates, string locale, bool hasContentChanges);

        IFile FindBestHostTemplateConfigFile(IFileSystemInfo config);

        [Obsolete("IMountPoint is IDisposable now.")]
        void ReleaseMountPoint(IMountPoint mountPoint);

        [Obsolete("Not possible anymore.")]
        void RemoveMountPoints(IEnumerable<Guid> mountPoints);

        [Obsolete("Not possible anymore.")]
        void RemoveMountPoint(IMountPoint mountPoint);
    }
}
