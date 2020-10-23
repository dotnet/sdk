using System;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Edge.Template
{
    public static class WellKnownSearchFilters
    {
        public static Func<ITemplateInfo, MatchInfo?> NameFilter(string name)
        {
            return (template) =>
            {
                if (string.IsNullOrEmpty(name))
                {
                    return new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Partial };
                }

                int nameIndex = template.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase);

                if (nameIndex == 0 && string.Equals(template.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Exact };
                }

                bool hasShortNamePartialMatch = false;

                if (template is FilterableTemplateInfo filterableTemplate)
                {
                    foreach (string shortName in filterableTemplate.GroupShortNameList)
                    {
                        int shortNameIndex = shortName.IndexOf(name, StringComparison.OrdinalIgnoreCase);

                        if (shortNameIndex == 0 && string.Equals(shortName, name, StringComparison.OrdinalIgnoreCase))
                        {
                            return new MatchInfo { Location = MatchLocation.ShortName, Kind = MatchKind.Exact };
                        }

                        hasShortNamePartialMatch |= shortNameIndex > -1;
                    }
                }
                else
                {
                    int shortNameIndex = template.ShortName.IndexOf(name, StringComparison.OrdinalIgnoreCase);

                    if (shortNameIndex == 0 && string.Equals(template.ShortName, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return new MatchInfo { Location = MatchLocation.ShortName, Kind = MatchKind.Exact };
                    }

                    hasShortNamePartialMatch = shortNameIndex > -1;
                }

                if (nameIndex > -1)
                {
                    return new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Partial };
                }

                if (hasShortNamePartialMatch)
                {
                    return new MatchInfo { Location = MatchLocation.ShortName, Kind = MatchKind.Partial };
                }

                return new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Mismatch };
            };
        }

        // This being case-insensitive depends on the dictionaries on the cache tags being declared as case-insensitive
        public static Func<ITemplateInfo, MatchInfo?> ContextFilter(string inputContext)
        {
            string context = inputContext?.ToLowerInvariant();

            return (template) =>
            {
                if (string.IsNullOrEmpty(context))
                {
                    return null;
                }

                if (template.Tags != null && template.Tags.TryGetValue("type", out ICacheTag typeTag))
                {
                    if (typeTag.ChoicesAndDescriptions.ContainsKey(context))
                    {
                        return new MatchInfo { Location = MatchLocation.Context, Kind = MatchKind.Exact };
                    }
                    else
                    {
                        return new MatchInfo { Location = MatchLocation.Context, Kind = MatchKind.Mismatch };
                    }
                }

                return null;
            };
        }

        // This being case-insensitive depends on the dictionaries on the cache tags being declared as case-insensitive
        // Note: This is specifically designed to provide match info against a user-input language.
        //      All dealings with the host-default language should be separate from this.
        public static Func<ITemplateInfo, MatchInfo?> LanguageFilter(string language)
        {
            return (template) =>
            {
                if (string.IsNullOrEmpty(language))
                {
                    return null;
                }

                if (template.Tags != null && template.Tags.TryGetValue("language", out ICacheTag languageTag))
                {
                    if (languageTag.ChoicesAndDescriptions.ContainsKey(language))
                    {
                        return new MatchInfo { Location = MatchLocation.Language, Kind = MatchKind.Exact };
                    }
                    else
                    {
                        return new MatchInfo { Location = MatchLocation.Language, Kind = MatchKind.Mismatch };
                    }
                }
                else
                {   // user specified a language, but the template didnt.
                    return new MatchInfo { Location = MatchLocation.Language, Kind = MatchKind.Mismatch };
                }
            };
        }

        public static Func<ITemplateInfo, MatchInfo?> BaselineFilter(string baselineName)
        {
            return (template) =>
            {
                if (string.IsNullOrEmpty(baselineName))
                {
                    return null;
                }

                if (template.BaselineInfo != null && template.BaselineInfo.ContainsKey(baselineName))
                {
                    return new MatchInfo { Location = MatchLocation.Baseline, Kind = MatchKind.Exact };
                }
                else
                {
                    return new MatchInfo { Location = MatchLocation.Baseline, Kind = MatchKind.Mismatch };
                }
            };
        }

        public static Func<ITemplateInfo, MatchInfo?> ClassificationsFilter(string name)
        {
            return (template) =>
            {
                if (string.IsNullOrEmpty(name))
                {
                    return null;
                }

                string[] parts = name.Split('/');

                if (template.Classifications != null)
                {
                    bool allParts = true;
                    bool anyParts = false;

                    foreach (string part in parts)
                    {
                        if(!template.Classifications.Contains(part, StringComparer.OrdinalIgnoreCase))
                        {
                            allParts = false;
                        }
                        else
                        {
                            anyParts = true;
                        }
                    }

                    anyParts &= parts.Length == template.Classifications.Count;

                    if (allParts || anyParts)
                    {
                        return new MatchInfo { Location = MatchLocation.Classification, Kind = allParts ? MatchKind.Exact : MatchKind.Partial };
                    }
                }

                return null;
            };
        }
    }
}
