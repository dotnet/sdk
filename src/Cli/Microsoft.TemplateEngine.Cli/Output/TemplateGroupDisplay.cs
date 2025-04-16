// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.Output
{
    internal static class TemplateGroupDisplay
    {
        /// <summary>
        /// Displays the list of templates in a table, one row per template group.
        ///
        /// The columns displayed are as follows:
        /// Except where noted, the values are taken from the highest-precedence template in the group. The info could vary among the templates in the group, but shouldn't.
        /// (There is no check that the info doesn't vary.)
        /// - Template Name
        /// - Short Name: displays the all available short names for the group.
        /// - Language: All languages supported by any template in the group are displayed, with the default language in brackets, e.g.: [C#]
        /// - Tags
        /// The columns can be configured via the command args, see <see cref="ITabularOutputArgs"/>/>.
        /// </summary>
        internal static void DisplayTemplateList(
            IEngineEnvironmentSettings engineEnvironmentSettings,
            IEnumerable<TemplateGroup> templateGroups,
            IDisplayFormatter displayFormatter,
            IReporter reporter,
            string? selectedLanguage = null)
        {
            IReadOnlyCollection<TemplateGroupEntry> groupsForDisplay = GetTemplateGroupsForListDisplay(
                templateGroups,
                selectedLanguage,
                engineEnvironmentSettings.GetDefaultLanguage(),
                engineEnvironmentSettings.Environment);

            DisplayTemplateList(groupsForDisplay, displayFormatter, engineEnvironmentSettings.Environment, reporter);

        }

        /// <summary>
        /// Displays the list of templates in a table, one row per template group.
        ///
        /// The columns displayed are as follows:
        /// Except where noted, the values are taken from the highest-precedence template in the group. The info could vary among the templates in the group, but shouldn't.
        /// (There is no check that the info doesn't vary.)
        /// - Template Name
        /// - Short Name: displays the all available short names for the group.
        /// - Language: All languages supported by any template in the group are displayed, with the default language in brackets, e.g.: [C#]
        /// - Tags
        /// The columns can be configured via the command args, see <see cref="ITabularOutputArgs"/>/>.
        /// </summary>
        internal static void DisplayTemplateList(
            IEngineEnvironmentSettings engineEnvironmentSettings,
            IEnumerable<ITemplateInfo> templates,
            IDisplayFormatter displayFormatter,
            IReporter reporter,
            string? selectedLanguage = null)
        {
            IReadOnlyCollection<TemplateGroupEntry> groupsForDisplay = GetTemplateGroupsForListDisplay(
                templates,
                selectedLanguage,
                engineEnvironmentSettings.GetDefaultLanguage(),
                engineEnvironmentSettings.Environment);

            DisplayTemplateList(groupsForDisplay, displayFormatter, engineEnvironmentSettings.Environment, reporter);
        }

        /// <summary>
        /// Displays the template languages.
        /// </summary>
        internal static IReadOnlyCollection<TemplateLanguageEntry> GetLanguagesToDisplay(IEnumerable<ITemplateInfo> templateGroup, string? language, string? defaultLanguage, IEnvironment environment)
        {
            var groupedTemplates = GetAuthorBasedGroups(templateGroup);

            List<TemplateLanguageEntry> languageGroups = new();

            foreach (var templates in groupedTemplates)
            {
                //List<TemplateLanguageEntry> languagesForDisplay = new();
                HashSet<string> uniqueLanguages = new(StringComparer.OrdinalIgnoreCase);
                string defaultLanguageDisplay = string.Empty;
                foreach (ITemplateInfo template in templates)
                {
                    string? lang = template.GetLanguage();
                    if (string.IsNullOrWhiteSpace(lang))
                    {
                        continue;
                    }

                    if (!uniqueLanguages.Add(lang))
                    {
                        continue;
                    }

                    var isDefault = string.IsNullOrEmpty(language) && string.Equals(defaultLanguage, lang, StringComparison.OrdinalIgnoreCase);

                    languageGroups.Add(new TemplateLanguageEntry { Id = lang, Default = isDefault });

                    //if (string.IsNullOrEmpty(language) && string.Equals(defaultLanguage, lang, StringComparison.OrdinalIgnoreCase))
                    //{
                    //    defaultLanguageDisplay = ;
                    //}
                    //else
                    //{
                    //    languagesForDisplay.Add(lang);
                    //}
                }

                //languagesForDisplay.Sort(StringComparer.OrdinalIgnoreCase);
                //if (!string.IsNullOrEmpty(defaultLanguageDisplay))
                //{
                //    languagesForDisplay.Insert(0, defaultLanguageDisplay);
                //}
                //languageGroups.Add(string.Join(",", languagesForDisplay));
            }
            //return string.Join(environment.NewLine, languageGroups);

            return languageGroups;
        }

        /// <summary>
        /// Displays the template authors.
        /// </summary>
        internal static IReadOnlyCollection<string> GetAuthorsToDisplay(IEnumerable<ITemplateInfo> templateGroup, IEnvironment environment)
        {
            //return string.Join(environment.NewLine, GetAuthorBasedGroups(templateGroup).Select(group => group.Key));
            return GetAuthorBasedGroups(templateGroup)
                .Select(group => group.Key)
                .ToArray();
        }

        /// <summary>
        /// Generates the list of template groups for table display.
        /// Except where noted, the values are taken from the highest-precedence template in the group. The info could vary among the templates in the group, but shouldn't. (There is no check that the info doesn't vary.)
        /// - Template Name
        /// - Short Name: displays the first short name from the highest precedence template in the group.
        /// - Language: All languages supported by any template in the group are displayed, with the default language in brackets, e.g.: [C#]
        /// - Tags
        /// - Author
        /// - Type.
        /// </summary>
        /// <param name="templateList">list of templates to be displayed.</param>
        /// <param name="language">language from the command input.</param>
        /// <param name="defaultLanguage">default language.</param>
        /// <param name="environment"><see cref="IEnvironment"/> settings to use.</param>
        /// <returns></returns>
        internal static IReadOnlyList<TemplateGroupEntry> GetTemplateGroupsForListDisplay(
            IEnumerable<ITemplateInfo> templateList,
            string? language,
            string? defaultLanguage,
            IEnvironment environment)
        {
            List<TemplateGroupEntry> templateGroupsForDisplay = new();
            IEnumerable<IGrouping<string?, ITemplateInfo>> groupedTemplateList = templateList.GroupBy(x => x.GroupIdentity, x => !string.IsNullOrEmpty(x.GroupIdentity), StringComparer.OrdinalIgnoreCase);
            foreach (IGrouping<string?, ITemplateInfo> templateGroup in groupedTemplateList)
            {
                ITemplateInfo highestPrecedenceTemplate = templateGroup.OrderByDescending(x => x.Precedence).First();
                //string shortNames = string.Join(",", templateGroup.SelectMany(t => t.ShortNameList).Distinct(StringComparer.OrdinalIgnoreCase));

                TemplateGroupEntry groupDisplayInfo = new()
                {
                    Name = highestPrecedenceTemplate.Name,
                    ShortNames = GetShortNamesToDisplay(templateGroup),
                    Languages = GetLanguagesToDisplay(templateGroup, language, defaultLanguage, environment),
                    Classifications = GetClassificationsToDisplay(templateGroup, environment),
                    Authors = GetAuthorsToDisplay(templateGroup, environment),
                    Types = GetTypesToDisplay(templateGroup, environment),
                };
                templateGroupsForDisplay.Add(groupDisplayInfo);
            }

            return templateGroupsForDisplay;
        }

        /// <summary>
        /// Displays the template tags.
        /// </summary>
        internal static IReadOnlyCollection<string> GetClassificationsToDisplay(IEnumerable<ITemplateInfo> templateGroup, IEnvironment environment)
        {
            var groupedTemplates = GetAuthorBasedGroups(templateGroup);

            List<string> classificationGroups = new();
            foreach (var templates in groupedTemplates)
            {
                var classifications = templates
                    .SelectMany(t => t.Classifications)
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                classificationGroups.AddRange(classifications);
                //classificationGroups.Add(
                //    string.Join(
                //        "/",
                //        templates
                //            .SelectMany(template => template.Classifications)
                //            .Where(classification => !string.IsNullOrWhiteSpace(classification))
                //            .Distinct(StringComparer.OrdinalIgnoreCase)));
            }
            //return string.Join(environment.NewLine, classificationGroups);

            return classificationGroups;
        }

        /// <summary>
        /// Generates the list of template groups for table display.
        /// Except where noted, the values are taken from the highest-precedence template in the group. The info could vary among the templates in the group, but shouldn't. (There is no check that the info doesn't vary.)
        /// - Template Name
        /// - Short Name: displays the first short name from the highest precedence template in the group.
        /// - Language: All languages supported by any template in the group are displayed, with the default language in brackets, e.g.: [C#]
        /// - Tags
        /// - Author
        /// - Type.
        /// </summary>
        /// <param name="templateGroupList">list of template groups to be displayed.</param>
        /// <param name="language">language from the command input.</param>
        /// <param name="defaultLanguage">default language.</param>
        /// <param name="environment"><see cref="IEnvironment"/> settings to use.</param>
        /// <returns></returns>
        private static IReadOnlyList<TemplateGroupEntry> GetTemplateGroupsForListDisplay(
            IEnumerable<TemplateGroup> templateGroupList,
            string? language,
            string? defaultLanguage,
            IEnvironment environment)
        {
            List<TemplateGroupEntry> templateGroupsForDisplay = new();
            foreach (TemplateGroup templateGroup in templateGroupList)
            {
                ITemplateInfo highestPrecedenceTemplate = templateGroup.Templates.OrderByDescending(x => x.Precedence).First();
                TemplateGroupEntry groupDisplayInfo = new()
                {
                    Name = highestPrecedenceTemplate.Name,
                    ShortNames = templateGroup.ShortNames,
                    Languages = GetLanguagesToDisplay(templateGroup.Templates, language, defaultLanguage, environment),
                    Classifications = GetClassificationsToDisplay(templateGroup.Templates, environment),
                    Authors = GetAuthorsToDisplay(templateGroup.Templates, environment),
                    Types = GetTypesToDisplay(templateGroup.Templates, environment),
                };

                templateGroupsForDisplay.Add(groupDisplayInfo);
            }

            return templateGroupsForDisplay;
        }

        private static IReadOnlyCollection<string> GetShortNamesToDisplay(IGrouping<string?, ITemplateInfo> templateGroups)
            => templateGroups.SelectMany(t => t.ShortNameList).ToArray();

        private static void DisplayTemplateList(
            IReadOnlyCollection<TemplateGroupEntry> groupsForDisplay,
            IDisplayFormatter displayFormatter,
            IEnvironment environment,
            IReporter reporter)
        {
            var output = displayFormatter.FormatTemplateList(groupsForDisplay, environment);
            reporter.WriteLine(output);
        }

        private static IOrderedEnumerable<IGrouping<string, ITemplateInfo>> GetAuthorBasedGroups(IEnumerable<ITemplateInfo> templateGroup)
        {
            return templateGroup
                .GroupBy(template => string.IsNullOrWhiteSpace(template.Author) ? string.Empty : template.Author, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);
        }

        private static IReadOnlyCollection<string> GetTypesToDisplay(IEnumerable<ITemplateInfo> templateGroup, IEnvironment environment)
        {
            var groupedTemplates = GetAuthorBasedGroups(templateGroup);

            List<string> typesGroups = new();
            foreach (var templates in groupedTemplates)
            {
                //typesGroups.Add(
                //    string.Join(
                //        ",",
                //        templates
                //            .Select(template => template.GetTemplateType())
                //            .Where(type => !string.IsNullOrWhiteSpace(type))
                //            .Distinct(StringComparer.OrdinalIgnoreCase)
                //            .OrderBy(type => type, StringComparer.OrdinalIgnoreCase)));

                var types = templates
                    .Select(template => template.GetTemplateType())
                    .Where(type => !string.IsNullOrWhiteSpace(type))
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                typesGroups.AddRange(types!);
            }

            //return string.Join(environment.NewLine, typesGroups);

            return typesGroups;
        }
    }
}
