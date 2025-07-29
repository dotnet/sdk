// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.HotReload
{
    internal abstract class SingleProcessDeltaApplier(ILogger logger) : DeltaApplier(logger)
    {
        /// <summary>
        /// List of modules that can't receive changes anymore.
        /// A module is added when a change is requested for it that is not supported by the runtime.
        /// </summary>
        private readonly HashSet<Guid> _frozenModules = [];

        public async Task<IReadOnlyList<HotReloadManagedCodeUpdate>> FilterApplicableUpdatesAsync(ImmutableArray<HotReloadManagedCodeUpdate> updates, CancellationToken cancellationToken)
        {
            var availableCapabilities = await GetApplyUpdateCapabilitiesAsync(cancellationToken);
            var applicableUpdates = new List<HotReloadManagedCodeUpdate>();

            foreach (var update in updates)
            {
                if (_frozenModules.Contains(update.ModuleId))
                {
                    // can't update frozen module:
                    continue;
                }

                if (update.RequiredCapabilities.Except(availableCapabilities).Any())
                {
                    // required capability not available:
                    _frozenModules.Add(update.ModuleId);
                }
                else
                {
                    applicableUpdates.Add(update);
                }
            }

            return applicableUpdates;
        }
    }
}
