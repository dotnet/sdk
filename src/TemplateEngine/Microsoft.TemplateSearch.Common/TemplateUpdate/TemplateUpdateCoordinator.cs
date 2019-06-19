using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;

namespace Microsoft.TemplateSearch.Common.TemplateUpdate
{
    public class TemplateUpdateCoordinator
    {
        public TemplateUpdateCoordinator(IEngineEnvironmentSettings environmentSettings, IInstallerBase installer)
        {
            _environmentSettings = environmentSettings;
            _installer = installer;
            _updateChecker = new TemplateUpdateChecker(_environmentSettings);
        }

        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly IInstallerBase _installer;
        private readonly TemplateUpdateChecker _updateChecker;

        public virtual async Task<IUpdateCheckResult> CheckForUpdatesAsync(IReadOnlyList<IInstallUnitDescriptor> installUnitsToCheck)
        {
            return await _updateChecker.CheckForUpdatesAsync(installUnitsToCheck);
        }

        public bool TryApplyUpdate(IUpdateUnitDescriptor updateDescriptor)
        {
            if (_updateChecker.TryGetUpdaterForDescriptorFactoryId(updateDescriptor.InstallUnitDescriptor.FactoryId, out IUpdater updater))
            {
                try
                {
                    updater.ApplyUpdates(_installer, new List<IUpdateUnitDescriptor>() { updateDescriptor });
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        public bool TryApplyUpdates(IReadOnlyList<IUpdateUnitDescriptor> updateDescriptorList, out IReadOnlyList<IUpdateUnitDescriptor> updateFailures)
        {
            List<IUpdateUnitDescriptor> failedToUpdate = new List<IUpdateUnitDescriptor>();

            foreach (IUpdateUnitDescriptor updateDescriptor in updateDescriptorList)
            {
                if (_updateChecker.TryGetUpdaterForDescriptorFactoryId(updateDescriptor.InstallUnitDescriptor.FactoryId, out IUpdater updater))
                {
                    try
                    {
                        updater.ApplyUpdates(_installer, new List<IUpdateUnitDescriptor>() { updateDescriptor });
                    }
                    catch
                    {
                        failedToUpdate.Add(updateDescriptor);
                    }
                }
            }

            updateFailures = failedToUpdate;
            return updateFailures.Count > 0;
        }
    }
}
