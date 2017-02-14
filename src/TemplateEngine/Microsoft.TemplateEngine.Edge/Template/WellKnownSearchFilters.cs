using System;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Edge.Template
{
    public static class WellKnownSearchFilters
    {
        public static Func<ITemplateInfo, string, MatchInfo?> NameFilter(string name)
        {
            return (template, alias) =>
            {
                if (string.IsNullOrEmpty(name))
                {
                    return new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Partial };
                }

                int nameIndex = template.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase);
                int shortNameIndex = template.ShortName.IndexOf(name, StringComparison.OrdinalIgnoreCase);

                if (nameIndex == 0 && string.Equals(template.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Exact };
                }

                if (shortNameIndex == 0 && string.Equals(template.ShortName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return new MatchInfo { Location = MatchLocation.ShortName, Kind = MatchKind.Exact };
                }

                if (nameIndex > -1)
                {
                    return new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Partial };
                }

                if (shortNameIndex > -1)
                {
                    return new MatchInfo { Location = MatchLocation.ShortName, Kind = MatchKind.Partial };
                }

                return new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Mismatch };
            };
        }

        public static Func<ITemplateInfo, string, MatchInfo?> AliasFilter(string name)
        {
            return (template, alias) =>
            {
                if (string.IsNullOrEmpty(name))
                {
                    return null;
                }

                int index = alias?.IndexOf(name, StringComparison.OrdinalIgnoreCase) ?? -1;

                if (index == 0 && string.Equals(alias, name, StringComparison.OrdinalIgnoreCase))
                {
                    return new MatchInfo { Location = MatchLocation.Alias, Kind = MatchKind.Exact };
                }

                if (index > -1)
                {
                    return new MatchInfo { Location = MatchLocation.Alias, Kind = MatchKind.Partial };
                }

                return null;
            };
        }

        // This being case-insensitive depends on the dictionaries on the cache tags being declared as case-insensitive
        public static Func<ITemplateInfo, string, MatchInfo?> ContextFilter(string context)
        {
            return (template, alias) =>
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
        public static Func<ITemplateInfo, string, MatchInfo?> LanguageFilter(string language)
        {
            return (template, alias) =>
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

                return null;
            };
        }

        public static Func<ITemplateInfo, string, MatchInfo?> ClassificationsFilter(string name)
        {
            return (template, alias) =>
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
