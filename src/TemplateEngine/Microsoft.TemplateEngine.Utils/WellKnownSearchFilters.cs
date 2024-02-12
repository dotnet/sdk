// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;

namespace Microsoft.TemplateEngine.Utils
{
    /// <summary>
    /// Collection of the predicates to be used for filtering templates by the most used <see cref="ITemplateInfo"/> properties.
    /// </summary>
    public static class WellKnownSearchFilters
    {
        /// <summary>
        /// The template should match all filters: <see cref="ITemplateMatchInfo.MatchDisposition"/> should have all dispositions of <see cref="MatchKind.Exact"/> or <see cref="MatchKind.Partial"/>.
        /// </summary>
        public static Func<ITemplateMatchInfo, bool> MatchesAllCriteria =>
            t => t.MatchDisposition.Count > 0 && t.MatchDisposition.All(x => x.Kind is MatchKind.Exact or MatchKind.Partial);

        /// <summary>
        /// The template should match at least one filter: <see cref="ITemplateMatchInfo.MatchDisposition"/> should have at least one disposition of <see cref="MatchKind.Exact"/> or <see cref="MatchKind.Partial"/>.
        /// </summary>
        public static Func<ITemplateMatchInfo, bool> MatchesAtLeastOneCriteria =>
            t => t.MatchDisposition.Any(x => x.Kind is MatchKind.Exact or MatchKind.Partial);

        /// <summary>
        /// The template filter that matches <paramref name="name"/> on the following criteria: <br/>
        /// - if <paramref name="name"/> is null or empty, adds match disposition <see cref="MatchInfo.BuiltIn.Name"/> with <see cref="MatchKind.Partial"/>;<br/>
        /// - if <paramref name="name"/> is equal to <see cref="ITemplateMetadata.Name"/> (case insensitive), adds match disposition <see cref="MatchInfo.BuiltIn.Name"/> with <see cref="MatchKind.Exact"/>;<br/>
        /// - if <paramref name="name"/> is equal to one of the short names in <see cref="ITemplateMetadata.ShortNameList"/> (case insensitive), adds match disposition <see cref="MatchInfo.BuiltIn.ShortName"/> with <see cref="MatchKind.Exact"/>;<br/>
        /// - if <see cref="ITemplateMetadata.Name"/> contains <paramref name="name"/> (case insensitive), adds match disposition <see cref="MatchInfo.BuiltIn.Name"/> with <see cref="MatchKind.Partial"/>;<br/>
        /// - if one of the short names in <see cref="ITemplateMetadata.ShortNameList"/> contains <paramref name="name"/> (case insensitive), adds match disposition <see cref="MatchInfo.BuiltIn.ShortName"/> with <see cref="MatchKind.Partial"/>;<br/>
        /// - adds match disposition <see cref="MatchInfo.BuiltIn.Name"/> with <see cref="MatchKind.Mismatch"/> otherwise.<br/>
        /// </summary>
        /// <returns> the predicate to be used when filtering the templates.</returns>
        public static Func<ITemplateInfo, MatchInfo?> NameFilter(string name)
        {
            return (template) =>
            {
                if (string.IsNullOrEmpty(name))
                {
                    return new MatchInfo(MatchInfo.BuiltIn.Name, name, MatchKind.Partial);
                }

                int nameIndex = template.Name.IndexOf(name, StringComparison.CurrentCultureIgnoreCase);

                if (nameIndex == 0 && template.Name.Length == name.Length)
                {
                    return new MatchInfo(MatchInfo.BuiltIn.Name, name, MatchKind.Exact);
                }

                bool hasShortNamePartialMatch = false;

                foreach (string shortName in template.ShortNameList)
                {
                    int shortNameIndex = shortName.IndexOf(name, StringComparison.OrdinalIgnoreCase);

                    if (shortNameIndex == 0 && shortName.Length == name.Length)
                    {
                        return new MatchInfo(MatchInfo.BuiltIn.ShortName, name, MatchKind.Exact);
                    }

                    hasShortNamePartialMatch |= shortNameIndex > -1;
                }

                if (nameIndex > -1)
                {
                    return new MatchInfo(MatchInfo.BuiltIn.Name, name, MatchKind.Partial);
                }

                if (hasShortNamePartialMatch)
                {
                    return new MatchInfo(MatchInfo.BuiltIn.ShortName, name, MatchKind.Partial);
                }

                return new MatchInfo(MatchInfo.BuiltIn.Name, name, MatchKind.Mismatch);
            };
        }

        /// <summary>
        /// The template filter that matches <paramref name="inputType"/> on the following criteria: <br/>
        /// - if <paramref name="inputType"/> is null or empty, does not add match disposition;<br/>
        /// - if <paramref name="inputType"/> is equal to tag named 'type' from <see cref="ITemplateInfo.Tags"/> (case insensitive), adds match disposition <see cref="MatchInfo.BuiltIn.Type"/> with <see cref="MatchKind.Exact"/>;<br/>
        /// - adds match disposition <see cref="MatchInfo.BuiltIn.Type"/> with <see cref="MatchKind.Mismatch"/> otherwise.<br/>
        /// </summary>
        /// <returns> the predicate to be used when filtering the templates.</returns>
        public static Func<ITemplateInfo, MatchInfo?> TypeFilter(string? inputType)
        {
            string? type = inputType?.ToLowerInvariant();

            return (template) =>
            {
                if (string.IsNullOrEmpty(type))
                {
                    return null;
                }
                if (template.GetTemplateType()?.Equals(type, StringComparison.OrdinalIgnoreCase) ?? false)
                {
                    return new MatchInfo(MatchInfo.BuiltIn.Type, type, MatchKind.Exact);
                }
                else
                {
                    return new MatchInfo(MatchInfo.BuiltIn.Type, type, MatchKind.Mismatch);
                }
            };
        }

        /// <summary>
        /// The template filter that matches <paramref name="classification"/> on the following criteria: <br/>
        /// - if <paramref name="classification"/> is null or empty, does not add match disposition;<br/>
        /// - if <paramref name="classification"/> is equal to any entry from <see cref="ITemplateMetadata.Classifications"/> (case insensitive), adds match disposition <see cref="MatchInfo.BuiltIn.Classification"/> with <see cref="MatchKind.Exact"/>;<br/>
        /// - adds match disposition <see cref="MatchInfo.BuiltIn.Classification"/> with <see cref="MatchKind.Mismatch"/> otherwise.<br/>
        /// </summary>
        /// <returns> the predicate to be used when filtering the templates..</returns>
        public static Func<ITemplateInfo, MatchInfo?> ClassificationFilter(string? classification)
        {
            return (template) =>
            {
                if (string.IsNullOrWhiteSpace(classification))
                {
                    return null;
                }
                if (template.Classifications?.Contains(classification, StringComparer.CurrentCultureIgnoreCase) ?? false)
                {
                    return new MatchInfo(MatchInfo.BuiltIn.Classification, classification, MatchKind.Exact);
                }
                return new MatchInfo(MatchInfo.BuiltIn.Classification, classification, MatchKind.Mismatch);
            };
        }

        /// <summary>
        /// The template filter that matches <paramref name="language"/> on the following criteria: <br/>
        /// - if <paramref name="language"/> is null or empty, does not add match disposition;<br/>
        /// - if <paramref name="language"/> is equal to tag named 'language' from <see cref="ITemplateInfo.Tags"/> (case insensitive), adds match disposition <see cref="MatchInfo.BuiltIn.Language"/> with <see cref="MatchKind.Exact"/>;<br/>
        /// - adds match disposition <see cref="MatchInfo.BuiltIn.Language"/> with <see cref="MatchKind.Mismatch"/> otherwise.<br/>
        /// </summary>
        /// <returns> the predicate to be used when filtering the templates.</returns>
        public static Func<ITemplateInfo, MatchInfo?> LanguageFilter(string? language)
        {
            return (template) =>
            {
                if (string.IsNullOrEmpty(language))
                {
                    return null;
                }

                if (template.GetLanguage()?.Equals(language, StringComparison.OrdinalIgnoreCase) ?? false)
                {
                    return new MatchInfo(MatchInfo.BuiltIn.Language, language, MatchKind.Exact);
                }
                else
                {
                    return new MatchInfo(MatchInfo.BuiltIn.Language, language, MatchKind.Mismatch);
                }
            };
        }

        /// <summary>
        /// The template filter that matches <paramref name="baselineName"/> on the following criteria: <br/>
        /// - if <paramref name="baselineName"/> is null or empty, does not add match disposition;<br/>
        /// - if <paramref name="baselineName"/> is equal to key from <see cref="ITemplateMetadata.BaselineInfo"/> (case insensitive), adds match disposition <see cref="MatchInfo.BuiltIn.Baseline"/> with <see cref="MatchKind.Exact"/>;<br/>
        /// - adds match disposition <see cref="MatchInfo.BuiltIn.Baseline"/> with <see cref="MatchKind.Mismatch"/> otherwise.<br/>
        /// </summary>
        /// <returns> the predicate to be used when filtering the templates.</returns>
        public static Func<ITemplateInfo, MatchInfo?> BaselineFilter(string? baselineName)
        {
            return (template) =>
            {
                if (string.IsNullOrEmpty(baselineName))
                {
                    return null;
                }

                if (template.BaselineInfo != null && template.BaselineInfo.ContainsKey(baselineName!))
                {
                    return new MatchInfo(MatchInfo.BuiltIn.Baseline, baselineName, MatchKind.Exact);
                }
                else
                {
                    return new MatchInfo(MatchInfo.BuiltIn.Baseline, baselineName, MatchKind.Mismatch);
                }
            };
        }

        /// <summary>
        /// The template filter that matches <paramref name="author"/> on the following criteria: <br/>
        /// - if <paramref name="author"/> is null or empty, does not add match disposition;<br/>
        /// - if <see cref="ITemplateMetadata.Author"/> is null or empty, adds match disposition <see cref="MatchInfo.BuiltIn.Author"/> with <see cref="MatchKind.Mismatch"/>;<br/>
        /// - if <paramref name="author"/> is equal to <see cref="ITemplateMetadata.Author"/> (case insensitive), adds match disposition <see cref="MatchInfo.BuiltIn.Author"/> with <see cref="MatchKind.Exact"/>;<br/>
        /// - if <see cref="ITemplateMetadata.Author"/> contains <paramref name="author"/> (case insensitive), adds match disposition <see cref="MatchInfo.BuiltIn.Author"/> with <see cref="MatchKind.Partial"/>;<br/>
        /// - <see cref="MatchInfo.BuiltIn.Author"/> with <see cref="MatchKind.Mismatch"/> otherwise.<br/>
        /// </summary>
        /// <returns> the predicate to be used when filtering the templates.</returns>
        public static Func<ITemplateInfo, MatchInfo?> AuthorFilter(string? author)
        {
            return (template) =>
            {
                if (string.IsNullOrWhiteSpace(author))
                {
                    return null;
                }

                if (string.IsNullOrWhiteSpace(template.Author))
                {
                    return new MatchInfo(MatchInfo.BuiltIn.Author, author, MatchKind.Mismatch);
                }

                int authorIndex = template.Author!.IndexOf(author, StringComparison.CurrentCultureIgnoreCase);

                if (authorIndex == 0 && template.Author.Length == author!.Length)
                {
                    return new MatchInfo(MatchInfo.BuiltIn.Author, author, MatchKind.Exact);
                }

                if (authorIndex > -1)
                {
                    return new MatchInfo(MatchInfo.BuiltIn.Author, author, MatchKind.Partial);
                }
                return new MatchInfo(MatchInfo.BuiltIn.Author, author, MatchKind.Mismatch);
            };
        }

        /// <summary>
        /// Gets the list of template filters for template constraints definition <paramref name="constraintDefinitions"/> <br/>
        /// For each constraint the template filter will be created: <br/>
        /// - if the constraint is not used in the template, does not add match disposition;<br/>
        /// - if the template meets the constraint, adds match disposition with <see cref="MatchKind.Exact"/>;<br/>
        /// - if the template does not meet the constraint or constraint cannot be evaluated, adds match disposition with <see cref="MatchKind.Mismatch"/>.<br/>
        /// The match info name used is 'Constraint.&lt;constraint type&gt;'.
        /// </summary>
        /// <returns> the list of predicates to be used when filtering the templates.</returns>
        public static IEnumerable<Func<ITemplateInfo, MatchInfo?>> ConstraintFilters(IEnumerable<ITemplateConstraint> constraintDefinitions)
        {
            foreach (ITemplateConstraint constraintDefinition in constraintDefinitions)
            {
                yield return (template) =>
                {
                    var matchingConstraints = template.Constraints.Where(c => c.Type == constraintDefinition.Type);
                    if (!matchingConstraints.Any())
                    {
                        //no constraint of such type defined in the template
                        return null;
                    }
                    foreach (var constraint in matchingConstraints)
                    {
                        var result = constraintDefinition.Evaluate(constraint.Args);
                        if (result.EvaluationStatus != TemplateConstraintResult.Status.Allowed)
                        {
                            return new MatchInfo($"{MatchInfo.BuiltIn.Constraint}.{constraintDefinition.Type}", null, MatchKind.Mismatch);
                        }
                    }
                    return new MatchInfo($"{MatchInfo.BuiltIn.Constraint}.{constraintDefinition.Type}", null, MatchKind.Exact);
                };
            }
        }
    }
}
