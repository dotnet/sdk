// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.Utils
{
    public static class TemplatePackageExtensions
    {
        /// <summary>
        /// Returns all <see cref="ITemplateInfo"/> contained by <paramref name="templatePackage"/>.
        /// </summary>
        /// <param name="templatePackage"></param>
        /// <param name="settings"></param>
        /// <returns></returns>
        public static async Task<IEnumerable<ITemplateInfo>> GetTemplates(this ITemplatePackage templatePackage, IEngineEnvironmentSettings settings)
        {
            var allTemplates = await settings.SettingsLoader.GetTemplatesAsync(default).ConfigureAwait(false);
            return allTemplates.Where(t => t.MountPointUri == templatePackage.MountPointUri);
        }
    }
}
