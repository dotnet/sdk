// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.CommandLine;
using System.CommandLine.Completions;
using System.CommandLine.Help;

namespace Microsoft.DotNet.Cli.Help;

public partial class HelpBuilder
{
    /// <summary>
    /// Provides default formatting for help output.
    /// </summary>
    public static class Default
    {
        /// <summary>
        /// Gets an argument's default value to be displayed in help.
        /// </summary>
        /// <param name="parameter">The argument or option to get the default value for.</param>
        public static string GetArgumentDefaultValue(Symbol parameter)
        {
            return parameter switch
            {
                Argument argument => argument.HasDefaultValue ? ToString(argument.GetDefaultValue()) : "",
                Option option => option.HasDefaultValue && !(option.ValueType == typeof(bool) && option.Arity.MaximumNumberOfValues == 0)
                    ? ToString(option.GetDefaultValue())
                    : "",
                _ => throw new InvalidOperationException("Symbol must be an Argument or Option.")
            };

            static string ToString(object? value) => value switch
            {
                null => string.Empty,
                string str => str,
                IEnumerable enumerable => string.Join("|", enumerable.Cast<object>()),
                _ => value.ToString() ?? string.Empty
            };
        }

        /// <summary>
        /// Gets the description for an argument (typically used in the second column text in the arguments section).
        /// </summary>
        public static string GetArgumentDescription(Argument argument) => argument.Description ?? string.Empty;

        /// <summary>
        /// Gets the usage title for an argument (for example: <c>&lt;value&gt;</c>, typically used in the first column text in the arguments usage section, or within the synopsis.
        /// </summary>
        public static string GetArgumentUsageLabel(Symbol parameter) => GetArgumentUsageLabel(parameter, null);

        private static string GetArgumentUsageLabel(Symbol parameter, string? helpName)
        {
            return parameter switch
            {
                Argument argument => GetUsageLabel(helpName ?? argument.HelpName, argument.ValueType, argument.CompletionSources, argument, argument.Arity) ?? $"<{argument.Name}>",
                Option option => GetUsageLabel(
                    helpName ?? option.HelpName ?? (option.ValueType != typeof(bool) && option.ValueType != typeof(bool?)
                        && option.Arity.MaximumNumberOfValues > 0 && option.CompletionSources.Count == 0 ? SplitPrefix(option.Name).Alias : null),
                    option.ValueType, option.CompletionSources, option, option.Arity) ?? "",
                _ => throw new InvalidOperationException()
            };

            static string? GetUsageLabel(string? helpName, Type valueType, List<Func<CompletionContext, IEnumerable<CompletionItem>>> completionSources, Symbol symbol, ArgumentArity arity)
            {
                // Argument.HelpName is always first choice
                if (!string.IsNullOrWhiteSpace(helpName))
                {
                    return $"<{helpName}>";
                }
                else if (
                    !(valueType == typeof(bool) || valueType == typeof(bool?))
                    && arity.MaximumNumberOfValues > 0 // allowing zero arguments means we don't need to show usage
                    && completionSources.Count > 0)
                {
                    IEnumerable<string> completions = symbol
                        .GetCompletions(CompletionContext.Empty)
                        .Select(item => item.Label);

                    string joined = string.Join("|", completions);

                    if (!string.IsNullOrEmpty(joined))
                    {
                        return $"<{joined}>";
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Gets the usage label for the specified symbol (typically used as the first column text in help output).
        /// </summary>
        /// <param name="symbol">The symbol to get a help item for.</param>
        /// <returns>Text to display.</returns>
        public static string GetCommandUsageLabel(Command symbol)
            => GetIdentifierSymbolUsageLabel(symbol, symbol.Aliases);

        internal static string GetCommandUsageLabel(Command symbol, Func<Argument, string> getArgumentUsageLabel)
            => GetIdentifierSymbolUsageLabel(symbol, symbol.Aliases, getArgumentUsageLabel: getArgumentUsageLabel);

        /// <inheritdoc cref="GetCommandUsageLabel(Command)"/>
        public static string GetOptionUsageLabel(Option symbol) => GetOptionUsageLabel(symbol, null);

        /// <summary>
        /// Gets the option usage label with a display-only argument name.
        /// </summary>
        public static string GetOptionUsageLabel(Option symbol, string? helpName)
            => GetIdentifierSymbolUsageLabel(symbol, symbol.Aliases, helpName);

        /// <summary>
        /// Gets the default sections to be written for command line help.
        /// </summary>
        public static IEnumerable<Func<HelpContext, bool>> GetLayout()
        {
            yield return SynopsisSection();
            yield return CommandUsageSection();
            yield return CommandArgumentsSection();
            yield return OptionsSection();
            yield return SubcommandsSection();
            yield return AdditionalArgumentsSection();
        }

        /// <summary>
        /// Writes a help section describing a command's synopsis.
        /// </summary>
        public static Func<HelpContext, bool> SynopsisSection() =>
            ctx =>
            {
                ctx.HelpBuilder.WriteHeading(LocalizationResources.HelpDescriptionTitle(), ctx.Command.Description, ctx.Output);
                return true;
            };

        /// <summary>
        /// Writes a help section describing a command's usage.
        /// </summary>
        public static Func<HelpContext, bool> CommandUsageSection() =>
            ctx =>
            {
                ctx.HelpBuilder.WriteHeading(LocalizationResources.HelpUsageTitle(), ctx.HelpBuilder.GetUsage(ctx), ctx.Output);
                return true;
            };

        /// <summary>
        /// Writes a help section describing a command's arguments.
        ///  </summary>
        public static Func<HelpContext, bool> CommandArgumentsSection() => CommandArgumentsSection(includeHidden: false);

        /// <summary>
        /// Writes a help section describing a command's arguments.
        /// </summary>
        /// <param name="includeHidden">Whether to include the current command's hidden arguments.</param>
        public static Func<HelpContext, bool> CommandArgumentsSection(bool includeHidden) =>
            ctx =>
            {
                TwoColumnHelpRow[] commandArguments = ctx.HelpBuilder.GetCommandArgumentRows(ctx.Command, ctx, includeHidden).ToArray();

                if (commandArguments.Length > 0)
                {
                    ctx.HelpBuilder.WriteHeading(LocalizationResources.HelpArgumentsTitle(), null, ctx.Output);
                    ctx.HelpBuilder.WriteColumns(commandArguments, ctx);
                    return true;
                }

                return false;
            };

        /// <summary>
        /// Writes a help section describing a command's subcommands.
        ///  </summary>
        public static Func<HelpContext, bool> SubcommandsSection() =>
            ctx => ctx.HelpBuilder.WriteSubcommands(ctx);

        /// <summary>
        /// Writes a help section describing a command's options.
        ///  </summary>
        public static Func<HelpContext, bool> OptionsSection() =>
            ctx =>
            {
                List<TwoColumnHelpRow> optionRows = new();
                bool addedHelpOption = false;
                foreach (Option option in ctx.Command.Options)
                {
                    if (!option.Hidden)
                    {
                        optionRows.Add(ctx.HelpBuilder.GetTwoColumnRow(option, ctx));
                        if (option is HelpOption)
                        {
                            addedHelpOption = true;
                        }
                    }
                }

                Command? current = ctx.Command;
                while (current is not null)
                {
                    Command? parentCommand = null;
                    foreach (Symbol parent in current.Parents)
                    {
                        if ((parentCommand = parent as Command) is not null)
                        {
                            if (parentCommand.Options.Any())
                            {
                                foreach (var option in parentCommand.Options)
                                {
                                    // global help aliases may be duplicated, we just ignore them
                                    if (option is { Recursive: true, Hidden: false })
                                    {
                                        if (option is not HelpOption || !addedHelpOption)
                                        {
                                            optionRows.Add(ctx.HelpBuilder.GetTwoColumnRow(option, ctx, isInheritedOption: true));
                                        }
                                    }
                                }
                            }

                            break;
                        }
                    }
                    current = parentCommand;
                }

                if (optionRows.Count > 0)
                {
                    ctx.HelpBuilder.WriteHeading(LocalizationResources.HelpOptionsTitle(), null, ctx.Output);
                    ctx.HelpBuilder.WriteColumns(optionRows, ctx);
                    return true;
                }

                return false;
            };

        /// <summary>
        /// Writes a help section describing a command's additional arguments, typically shown only when <see cref="Command.TreatUnmatchedTokensAsErrors"/> is set to <see langword="true"/>.
        ///  </summary>
        public static Func<HelpContext, bool> AdditionalArgumentsSection() =>
            ctx => ctx.HelpBuilder.WriteAdditionalArguments(ctx);

        private static string GetIdentifierSymbolUsageLabel(
            Symbol symbol,
            ICollection<string>? aliasSet,
            string? helpName = null,
            Func<Argument, string>? getArgumentUsageLabel = null)
        {
            var aliases = aliasSet is null
                ? new[] { symbol.Name }
                : new[] { symbol.Name }.Concat(aliasSet)
                                .Select(SplitPrefix)
                                .OrderBy(r => r.Prefix, StringComparer.OrdinalIgnoreCase)
                                .ThenBy(r => r.Alias, StringComparer.OrdinalIgnoreCase)
                                .GroupBy(t => t.Alias)
                                .Select(t => t.First())
                                .Select(t => $"{t.Prefix}{t.Alias}");

            var firstColumnText = string.Join(", ", aliases);

            foreach (var argument in GetParameters(symbol))
            {
                if (!argument.Hidden)
                {
                    var argumentFirstColumnText = argument is Argument commandArgument && getArgumentUsageLabel is not null
                        ? getArgumentUsageLabel(commandArgument)
                        : GetArgumentUsageLabel(argument, helpName);

                    if (!string.IsNullOrWhiteSpace(argumentFirstColumnText))
                    {
                        firstColumnText += $" {argumentFirstColumnText}";
                    }
                }
            }

            if (symbol is Option { Required: true })
            {
                firstColumnText += $" {LocalizationResources.HelpOptionsRequiredLabel()}";
            }

            return firstColumnText;
        }

        private static (string? Prefix, string Alias) SplitPrefix(string rawAlias)
        {
            if (rawAlias[0] == '/')
            {
                return ("/", rawAlias.Substring(1));
            }
            else if (rawAlias[0] == '-')
            {
                if (rawAlias.Length > 1 && rawAlias[1] == '-')
                {
                    return ("--", rawAlias.Substring(2));
                }

                return ("-", rawAlias.Substring(1));
            }

            return (null, rawAlias);
        }

    }
}
