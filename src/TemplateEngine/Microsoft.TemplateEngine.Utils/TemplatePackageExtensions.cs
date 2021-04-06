using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            var allTemplates = await settings.SettingsLoader.GetTemplatesAsync(default);
            return allTemplates.Where(t => t.MountPointUri == templatePackage.MountPointUri);
        }
    }
}
