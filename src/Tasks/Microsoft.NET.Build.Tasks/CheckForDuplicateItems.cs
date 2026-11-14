// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Globalization;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    [MSBuildMultiThreadableTask]
    public class CheckForDuplicateItems : TaskBase
    {
        [Required]
        public ITaskItem[] Items { get; set; }

        [Required]
        public string ItemName { get; set; }

        public bool DefaultItemsEnabled { get; set; }

        public bool DefaultItemsOfThisTypeEnabled { get; set; }

        [Required]
        public string PropertyNameToDisableDefaultItems { get; set; }

        public string PropertyValueToDisableDefaultItems { get; set; } = "false";

        [Required]
        public string MoreInformationLink { get; set; }

        /// <summary>
        /// Optional name of a metadata that can make items with the same item spec distinct. Items with the same
        /// item spec are not treated as duplicates if each of them has a different, non-empty value for this metadata
        /// (for example, the same file embedded more than once with different LogicalName values).
        /// </summary>
        public string DistinguishingMetadataName { get; set; }

        [Output]
        public ITaskItem[] DeduplicatedItems { get; set; }

        protected override void ExecuteCore()
        {
            DeduplicatedItems = Array.Empty<ITaskItem>();

            if (DefaultItemsEnabled && DefaultItemsOfThisTypeEnabled)
            {
                var itemGroups = Items.GroupBy(i => i.ItemSpec, StringComparer.OrdinalIgnoreCase);

                var duplicateItems = itemGroups.Where(IsDuplicate).ToList();
                if (duplicateItems.Any())
                {
                    string duplicateItemsFormatted = string.Join("; ", duplicateItems.Select(d => $"'{d.Key}'"));

                    string message = string.Format(CultureInfo.CurrentCulture, Strings.DuplicateItemsError,
                        ItemName,
                        PropertyNameToDisableDefaultItems,
                        PropertyValueToDisableDefaultItems,
                        duplicateItemsFormatted,
                        MoreInformationLink);

                    Log.LogError(message);

                    DeduplicatedItems = itemGroups.SelectMany(g => IsDuplicate(g) ? g.Take(1) : g).ToArray();
                }
            }
        }

        private bool IsDuplicate(IGrouping<string, ITaskItem> itemGroup)
        {
            if (itemGroup.Count() <= 1)
            {
                return false;
            }

            if (string.IsNullOrEmpty(DistinguishingMetadataName))
            {
                return true;
            }

            var metadataValues = itemGroup.Select(i => i.GetMetadata(DistinguishingMetadataName)).ToList();
            bool allValuesAreDistinct = metadataValues.All(v => !string.IsNullOrEmpty(v)) &&
                metadataValues.Distinct(StringComparer.Ordinal).Count() == metadataValues.Count;

            return !allValuesAreDistinct;
        }
    }
}
