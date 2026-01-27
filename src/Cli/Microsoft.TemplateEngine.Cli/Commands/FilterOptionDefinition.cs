// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.New;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Utils;
using Microsoft.TemplateSearch.Common.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    /// <summary>
    /// Defines supported dotnet new command filter option
    /// Filter options can be used along with other dotnet new subcommands to filter the required items for the action, for example for list subcommand filters can limit the templates to be shown.
    /// </summary>
    internal class FilterOptionDefinition
    {
        internal static FilterOptionDefinition AuthorFilter { get; } =
             new TemplateFilterOptionDefinition(
                 matchFilter: authorArg => WellKnownSearchFilters.AuthorFilter(authorArg),
                 mismatchCriteria: resolutionResult => resolutionResult.HasAuthorMismatch,
                 matchInfoName: MatchInfo.BuiltIn.Author);

        internal static FilterOptionDefinition BaselineFilter { get; } =
            new TemplateFilterOptionDefinition(
                matchFilter: baselineArg => WellKnownSearchFilters.BaselineFilter(baselineArg),
                mismatchCriteria: resolutionResult => resolutionResult.HasBaselineMismatch,
                matchInfoName: MatchInfo.BuiltIn.Baseline);

        internal static FilterOptionDefinition LanguageFilter { get; } =
            new TemplateFilterOptionDefinition(
                matchFilter: languageArg => WellKnownSearchFilters.LanguageFilter(languageArg),
                mismatchCriteria: resolutionResult => resolutionResult.HasLanguageMismatch,
                matchInfoName: MatchInfo.BuiltIn.Language);

        internal static FilterOptionDefinition TagFilter { get; } =
            new TemplateFilterOptionDefinition(
                matchFilter: tagArg => WellKnownSearchFilters.ClassificationFilter(tagArg),
                mismatchCriteria: resolutionResult => resolutionResult.HasClassificationMismatch,
                matchInfoName: MatchInfo.BuiltIn.Classification);

        internal static FilterOptionDefinition TypeFilter { get; } =
            new TemplateFilterOptionDefinition(
                matchFilter: typeArg => WellKnownSearchFilters.TypeFilter(typeArg),
                mismatchCriteria: resolutionResult => resolutionResult.HasTypeMismatch,
                matchInfoName: MatchInfo.BuiltIn.Type);

        internal static FilterOptionDefinition PackageFilter { get; } =
            new PackageFilterOptionDefinition(
                matchFilter: PackageMatchFilter);

        /// <summary>
        /// Maps option name to the corresponding filter definition.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, FilterOptionDefinition> AllDefinitions = new Dictionary<string, FilterOptionDefinition>()
        {
            { SharedOptionsFactory.AuthorOptionName, AuthorFilter },
            { SharedOptionsFactory.BaselineOptionName, BaselineFilter },
            { SharedOptionsFactory.LanguageOptionName, LanguageFilter },
            { SharedOptionsFactory.TagOptionName, TagFilter },
            { SharedOptionsFactory.TypeOptionName, TypeFilter },
            { SharedOptionsFactory.PackageOptionName, PackageFilter }
        };

        private static Func<ITemplatePackageInfo, bool> PackageMatchFilter(string? packageArg)
        {
            return (pack) =>
            {
                if (string.IsNullOrWhiteSpace(packageArg))
                {
                    return true;
                }
                return pack.Name.Contains(packageArg, StringComparison.OrdinalIgnoreCase);
            };
        }
    }

    /// <summary>
    /// Defines supported dotnet new command filter option applicable to the template.
    /// </summary>
    internal sealed class TemplateFilterOptionDefinition(
        Func<string?, Func<ITemplateInfo, MatchInfo?>> matchFilter,
        Func<TemplateResolutionResult, bool> mismatchCriteria,
        string matchInfoName) : FilterOptionDefinition
    {
        /// <summary>
        /// A predicate that returns the template match filter for the filter option.
        /// Template match filter should return the MatchInfo for the given template based on filter value.
        /// </summary>
        /// <remarks>
        /// Common template match filters are defined in Microsoft.TemplateEngine.Utils.WellKnonwnSearchFilter class.
        /// </remarks>
        internal Func<string?, Func<ITemplateInfo, MatchInfo?>> TemplateMatchFilter { get; } = matchFilter;

        /// <summary>
        /// A predicate that returns if the filter option caused a mismatch in <see cref="TemplateResolutionResult"/> in case of partial match.
        /// </summary>
        internal Func<TemplateResolutionResult, bool> MismatchCriteria { get; } = mismatchCriteria;

        /// <summary>
        /// A <see cref="MatchInfo"/> name used in match dispositions.
        /// </summary>
        internal string MatchInfoName { get; } = matchInfoName;
    }

    /// <summary>
    /// Defines supported dotnet new command filter option applicable to the package.
    /// </summary>
    internal sealed class PackageFilterOptionDefinition(Func<string?, Func<ITemplatePackageInfo, bool>> matchFilter) : FilterOptionDefinition
    {
        /// <summary>
        /// A predicate that returns the package match filter for the filter option
        /// Package match filter should if package is a match based on filter value.
        /// </summary>
        internal Func<string?, Func<ITemplatePackageInfo, bool>> PackageMatchFilter { get; } = matchFilter;
    }
}
