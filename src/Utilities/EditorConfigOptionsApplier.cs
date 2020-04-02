// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.Options;
using Microsoft.VisualStudio.CodingConventions;

namespace Microsoft.CodeAnalysis.Tools.Utilities
{
    internal class EditorConfigOptionsApplier
    {
        private readonly ImmutableArray<(IOption?, OptionStorageLocation?, MethodInfo?)> _formattingOptionsWithStorage;

        public EditorConfigOptionsApplier()
        {
            var commonOptionsType = typeof(Formatting.FormattingOptions);
            var csharpOptionsType = typeof(CSharp.Formatting.CSharpFormattingOptions);
            _formattingOptionsWithStorage = GetOptionsWithStorageFromTypes(new[] { commonOptionsType, csharpOptionsType });
        }

        /// <summary>
        /// Apply .editorconfig settings to the <see cref="OptionSet" />.
        /// </summary>
        public OptionSet ApplyConventions(OptionSet optionSet, ICodingConventionsSnapshot codingConventions, string languageName)
        {
            foreach (var optionWithStorage in _formattingOptionsWithStorage)
            {
                if (TryGetConventionValue(optionWithStorage, codingConventions, out var value))
                {
                    var option = optionWithStorage.Item1;
                    if (option is null)
                        continue;

                    var optionKey = new OptionKey(option, option?.IsPerLanguage == true ? languageName : null);
                    optionSet = optionSet.WithChangedOption(optionKey, value);
                }
            }

            return optionSet;
        }

        internal ImmutableArray<(IOption?, OptionStorageLocation?, MethodInfo?)> GetOptionsWithStorageFromTypes(params Type[] formattingOptionTypes)
        {
            var optionType = typeof(IOption);
            return formattingOptionTypes
                .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.GetProperty))
                .Where(p => optionType.IsAssignableFrom(p.PropertyType))
                .Select(p => (IOption?)p.GetValue(null))
                .Select(GetOptionWithStorage)
                .Where(ows => ows.Item2 != null)
                .ToImmutableArray();
        }

        internal (IOption?, OptionStorageLocation?, MethodInfo?) GetOptionWithStorage(IOption? option)
        {
            var editorConfigStorage = !(option?.StorageLocations.IsDefaultOrEmpty == true)
                ? option?.StorageLocations.FirstOrDefault(IsEditorConfigStorage)
                : null;
            var tryGetOptionMethod = editorConfigStorage?.GetType().GetMethod("TryGetOption");
            return (option, editorConfigStorage, tryGetOptionMethod);
        }

        internal static bool IsEditorConfigStorage(OptionStorageLocation storageLocation)
        {
            return storageLocation.GetType().FullName?.StartsWith("Microsoft.CodeAnalysis.Options.EditorConfigStorageLocation") == true;
        }

        internal static bool TryGetConventionValue((IOption?, OptionStorageLocation?, MethodInfo?) optionWithStorage, ICodingConventionsSnapshot codingConventions, [NotNullWhen(true)] out object? value)
        {
            var (option, editorConfigStorage, tryGetOptionMethod) = optionWithStorage;

            value = null;
            if (option is null || editorConfigStorage is null || tryGetOptionMethod is null)
            {
                return false;
            }

            // EditorConfigStorageLocation no longer accepts a IReadOnlyDictionary<string, object>. All values should
            // be string so we can convert it into a Dictionary<string, string>
            var adjustedConventions = codingConventions.AllRawConventions.ToDictionary(kvp => kvp.Key, kvp => (string)kvp.Value);
            var args = new object[] { adjustedConventions, option.Type, value! };

            var isOptionPresent = tryGetOptionMethod.Invoke(editorConfigStorage, args) as bool?;
            value = args[2];

            return isOptionPresent == true;
        }
    }
}
