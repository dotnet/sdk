using Microsoft.TemplateEngine.Abstractions;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.TemplateEngine.Utils
{
    /// <summary>
    /// The class provides ITemplateInfo extension methods
    /// </summary>
    public static class TemplateInfoExtensions
    {
        /// <summary>
        /// Gets the language defined in <paramref name="template"/>
        /// </summary>
        /// <param name="template">template definition</param>
        /// <returns>The language defined in the template or null if no language is defined</returns>
        /// <remarks>The tags are read in <see cref="SimpleConfigModel.ConvertedDeprecatedTagsToParameterSymbols"/> method. The single value for the tag is guaranteed.</remarks>
        public static string GetLanguage(this ITemplateInfo template)
        {
            return template.GetTagValues("language")?.Single();
        }

        /// <summary>
        /// Gets the type defined in <paramref name="template"/>
        /// </summary>
        /// <param name="template">template definition</param>
        /// <returns>The type defined in the template or null if no type is defined</returns>
        /// <remarks>The tags are read in <see cref="SimpleConfigModel.ConvertedDeprecatedTagsToParameterSymbols"/> method. The single value for the tag is guaranteed.</remarks>
        public static string GetTemplateType(this ITemplateInfo template)
        {
            return template.GetTagValues("type")?.Single();
        }

        /// <summary>
        /// Gets the possible values for the tag <paramref name="tagName"/> in <paramref name="template"/>
        /// </summary>
        /// <param name="template">template definition</param>
        /// <param name="tagName">tag name</param>
        /// <returns>The values of tag defined in the template or null if the tag is not defined in the template</returns>
        public static IEnumerable<string> GetTagValues (this ITemplateInfo template, string tagName)
        {
            if (template.Tags == null || !template.Tags.TryGetValue(tagName, out ICacheTag tag))
            {
                return null;
            }
            return tag.ChoicesAndDescriptions.Keys;
        }
    }
}
