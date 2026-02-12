// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.CommandLine.StaticCompletions;

/// <summary>
/// Extensions for marking options or arguments require dynamic completions. Such symbols get special handling
/// in the static completion generation logic.
/// </summary>
public static class DynamicSymbolExtensions
{
    /// <summary>
    /// The state that is used to track which symbols are dynamic.
    /// </summary>
    private static readonly Dictionary<Symbol, bool> s_dynamicSymbols = [];

    extension(Option option)
    {
        /// <summary>
        /// Indicates whether this option requires a dynamic call into the dotnet process to compute completions.
        /// </summary>
        public bool IsDynamic
        {
            get => s_dynamicSymbols.GetValueOrDefault(option, false);
            set => s_dynamicSymbols[option] = value;
        }

        /// <summary>
        /// Mark this option as requiring dynamic completions.
        /// </summary>
        /// <returns></returns>
        public Option RequiresDynamicCompletion()
        {
            option.IsDynamic = true;
            return option;
        }
    }

    extension(Argument argument)
    {
        /// Indicates whether this argument requires a dynamic call into the dotnet process to compute completions.
        public bool IsDynamic
        {
            get => s_dynamicSymbols.GetValueOrDefault(argument, false);
            set => s_dynamicSymbols[argument] = value;
        }

        /// <summary>
        /// Mark this argument as requiring dynamic completions.
        /// </summary>
        /// <returns></returns>
        public Argument RequiresDynamicCompletion()
        {
            argument.IsDynamic = true;
            return argument;
        }
    }
}
