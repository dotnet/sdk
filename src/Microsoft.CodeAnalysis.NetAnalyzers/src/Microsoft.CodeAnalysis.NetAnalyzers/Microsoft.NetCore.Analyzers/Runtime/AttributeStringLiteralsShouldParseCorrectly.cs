﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA2243: <inheritdoc cref="AttributeStringLiteralsShouldParseCorrectlyTitle"/>
    /// Unlike FxCop, this rule does not fire diagnostics for ill-formed versions
    /// Reason: There is wide usage of semantic versioning which does not follow traditional versioning grammar.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class AttributeStringLiteralsShouldParseCorrectlyAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2243";

        private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(AttributeStringLiteralsShouldParseCorrectlyTitle));
        private static readonly LocalizableString s_localizableDescription = CreateLocalizableResourceString(nameof(AttributeStringLiteralsShouldParseCorrectlyDescription));

        internal static readonly DiagnosticDescriptor DefaultRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(AttributeStringLiteralsShouldParseCorrectlyMessageDefault)),
            DiagnosticCategory.Usage,
            RuleLevel.Disabled,    // Heuristic based rule.
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor EmptyRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(AttributeStringLiteralsShouldParseCorrectlyMessageEmpty)),
            DiagnosticCategory.Usage,
            RuleLevel.Disabled,    // Heuristic based rule.
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(DefaultRule, EmptyRule);

        private static readonly List<ValueValidator> s_tokensToValueValidator =
            new(
                new[] { new ValueValidator(ImmutableArray.Create("guid"), "Guid", GuidValueValidator),
                        new ValueValidator(ImmutableArray.Create("url", "uri", "urn"), "Uri", UrlValueValidator, "UriTemplate", "UrlFormat")});

        private static bool GuidValueValidator(string value)
        {
            try
            {
                var unused = new Guid(value);
                return true;
            }
            catch (OverflowException)
            {
            }
            catch (FormatException)
            {
            }

            return false;
        }

        private static bool UrlValueValidator(string value)
        {
            return Uri.IsWellFormedUriString(value, UriKind.RelativeOrAbsolute);
        }

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolAction(saContext =>
            {
                var symbol = saContext.Symbol;
                AnalyzeSymbol(saContext.ReportDiagnostic, symbol, saContext.CancellationToken);
                switch (symbol.Kind)
                {
                    case SymbolKind.NamedType:
                        {
                            var namedType = (INamedTypeSymbol)symbol;

                            AnalyzeSymbols(saContext.ReportDiagnostic, namedType.TypeParameters, saContext.CancellationToken);

                            if (namedType.TypeKind == TypeKind.Delegate && namedType.DelegateInvokeMethod != null)
                            {
                                AnalyzeSymbols(saContext.ReportDiagnostic, namedType.DelegateInvokeMethod.Parameters, saContext.CancellationToken);
                            }

                            return;
                        }

                    case SymbolKind.Method:
                        {
                            var methodSymbol = (IMethodSymbol)symbol;
                            if (!methodSymbol.IsAccessorMethod())
                            {
                                AnalyzeSymbols(saContext.ReportDiagnostic, methodSymbol.Parameters, saContext.CancellationToken);
                                AnalyzeSymbols(saContext.ReportDiagnostic, methodSymbol.TypeParameters, saContext.CancellationToken);
                            }

                            return;
                        }

                    case SymbolKind.Property:
                        {
                            var propertySymbol = (IPropertySymbol)symbol;
                            AnalyzeSymbols(saContext.ReportDiagnostic, propertySymbol.Parameters, saContext.CancellationToken);
                            return;
                        }
                }
            },
            SymbolKind.NamedType,
            SymbolKind.Method, SymbolKind.Property, SymbolKind.Field, SymbolKind.Event);

            context.RegisterCompilationAction(caContext =>
            {
                var compilation = caContext.Compilation;
                AnalyzeSymbol(caContext.ReportDiagnostic, compilation.Assembly, caContext.CancellationToken);
            });
        }

        private static void AnalyzeSymbols(Action<Diagnostic> reportDiagnostic, IEnumerable<ISymbol> symbols, CancellationToken cancellationToken)
        {
            foreach (var symbol in symbols)
            {
                AnalyzeSymbol(reportDiagnostic, symbol, cancellationToken);
            }
        }

        private static void AnalyzeSymbol(Action<Diagnostic> reportDiagnostic, ISymbol symbol, CancellationToken cancellationToken)
        {
            var attributes = symbol.GetAttributes();

            foreach (var attribute in attributes)
            {
                Analyze(reportDiagnostic, attribute, symbol, cancellationToken);
            }
        }

        private static void Analyze(Action<Diagnostic> reportDiagnostic, AttributeData attributeData, ISymbol symbol, CancellationToken cancellationToken)
        {
            var attributeConstructor = attributeData.AttributeConstructor;
            var constructorArguments = attributeData.ConstructorArguments;

            if (attributeData.AttributeClass == null ||
                attributeConstructor == null ||
                !attributeConstructor.Parameters.HasExactly(constructorArguments.Count()))
            {
                return;
            }

            var syntax = attributeData.ApplicationSyntaxReference?.GetSyntax(cancellationToken) ?? symbol.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken);

            for (int i = 0; i < attributeConstructor.Parameters.Count(); i++)
            {
                var parameter = attributeConstructor.Parameters[i];
                if (parameter.Type.SpecialType != SpecialType.System_String)
                {
                    continue;
                }

                // If the name of the parameter is not something which requires the value-passed
                // to the parameter to be validated then we don't have to do anything
                var valueValidator = GetValueValidator(parameter.Name);
                if (valueValidator != null && !valueValidator.IsIgnoredName(parameter.Name))
                {
                    if (constructorArguments[i].Value is string value)
                    {
                        string classDisplayString = attributeData.AttributeClass.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat);
                        if (value.Length == 0)
                        {
                            reportDiagnostic(syntax.CreateDiagnostic(EmptyRule,
                                classDisplayString,
                                parameter.Name,
                                valueValidator.TypeName));
                        }
                        else if (!valueValidator.IsValidValue(value))
                        {
                            reportDiagnostic(syntax.CreateDiagnostic(DefaultRule,
                                classDisplayString,
                                parameter.Name,
                                value,
                                valueValidator.TypeName));
                        }
                    }
                }
            }

            foreach (var namedArgument in attributeData.NamedArguments)
            {
                if (namedArgument.Value.IsNull ||
                    namedArgument.Value.Type?.SpecialType != SpecialType.System_String)
                {
                    return;
                }

                var valueValidator = GetValueValidator(namedArgument.Key);
                if (valueValidator != null &&
                    !valueValidator.IsIgnoredName(namedArgument.Key) &&
                    namedArgument.Value.Value is string value)
                {
                    string classDisplayString = attributeData.AttributeClass.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat);
                    if (value.Length == 0)
                    {
                        reportDiagnostic(syntax.CreateDiagnostic(EmptyRule,
                            classDisplayString,
                            $"{classDisplayString}.{namedArgument.Key}",
                            valueValidator.TypeName));
                    }
                    else if (!valueValidator.IsValidValue(value))
                    {
                        reportDiagnostic(syntax.CreateDiagnostic(DefaultRule,
                            classDisplayString,
                            $"{classDisplayString}.{namedArgument.Key}",
                            value,
                            valueValidator.TypeName));
                    }
                }
            }
        }

        private static ValueValidator? GetValueValidator(string name)
        {
            foreach (var valueValidator in s_tokensToValueValidator)
            {
                if (WordParser.ContainsWord(name, WordParserOptions.SplitCompoundWords, valueValidator.AcceptedTokens))
                {
                    return valueValidator;
                }
            }

            return null;
        }
    }

    internal class ValueValidator
    {
        private readonly string[] _ignoredNames;

        public ImmutableArray<string> AcceptedTokens { get; }
        public string TypeName { get; }
        public Func<string, bool> IsValidValue { get; }

        public bool IsIgnoredName(string name)
            => _ignoredNames.Contains(name, StringComparer.OrdinalIgnoreCase);

        public ValueValidator(ImmutableArray<string> acceptedTokens, string typeName, Func<string, bool> isValidValue, params string[] ignoredNames)
        {
            _ignoredNames = ignoredNames;

            AcceptedTokens = acceptedTokens;
            TypeName = typeName;
            IsValidValue = isValidValue;
        }
    }
}