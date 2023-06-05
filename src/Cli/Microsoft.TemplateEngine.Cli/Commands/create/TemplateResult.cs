﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    /// <summary>
    /// The class represents validity of certain template in context of the command being executed.
    /// </summary>
    internal class TemplateResult
    {
        private readonly TemplateCommand _templateCommand;
        private readonly ParseResult _parseResult;
        private List<TemplateOptionResult> _parametersInfo = new List<TemplateOptionResult>();

        private TemplateResult(TemplateCommand templateCommand, ParseResult parseResult)
        {
            _templateCommand = templateCommand;
            _parseResult = parseResult;
        }

        internal bool IsTemplateMatch => IsLanguageMatch && IsTypeMatch && IsBaselineMatch;

        internal bool IsLanguageMatch { get; private set; }

        internal bool IsTypeMatch { get; private set; }

        internal bool IsBaselineMatch { get; private set; }

        internal OptionResult? Language { get; private set; }

        internal CliTemplateInfo TemplateInfo => _templateCommand.Template;

        internal IEnumerable<TemplateOptionResult> ValidTemplateOptions => _parametersInfo.Where(i => !(i is InvalidTemplateOptionResult));

        internal IEnumerable<InvalidTemplateOptionResult> InvalidTemplateOptions => _parametersInfo.OfType<InvalidTemplateOptionResult>();

        internal static TemplateResult FromParseResult(TemplateCommand templateCommand, ParseResult parseResult)
        {
            TemplateResult result = new TemplateResult(templateCommand, parseResult);
            result.IsLanguageMatch = templateCommand.LanguageOption == null || !parseResult.HasErrorFor(templateCommand.LanguageOption);
            result.IsTypeMatch = templateCommand.TypeOption == null || !parseResult.HasErrorFor(templateCommand.TypeOption);
            result.IsBaselineMatch = templateCommand.BaselineOption == null || !parseResult.HasErrorFor(templateCommand.BaselineOption);

            if (templateCommand.LanguageOption != null && result.IsTemplateMatch)
            {
                result.Language = parseResult.FindResultFor(templateCommand.LanguageOption);
            }

            foreach (var option in templateCommand.TemplateOptions)
            {
                if (parseResult.HasErrorFor(option.Value.Option))
                {
                    result._parametersInfo.Add(InvalidTemplateOptionResult.FromParseResult(option.Value, parseResult));
                }
                else
                {
                    if (TemplateOptionResult.FromParseResult(option.Value, parseResult) is TemplateOptionResult { } res)
                    {
                        result._parametersInfo.Add(res);
                    }
                }
            }
            foreach (var unmatchedToken in parseResult.UnmatchedTokens)
            {
                result._parametersInfo.Add(new InvalidTemplateOptionResult(
                    null,
                    InvalidTemplateOptionResult.Kind.InvalidName,
                    inputFormat: unmatchedToken,
                    specifiedValue: null,
                    errorMessage: null));
            }
            return result;
        }
    }
}
