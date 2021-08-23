// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA2234: Pass system uri objects instead of strings
    /// </summary>
    public abstract class PassSystemUriObjectsInsteadOfStringsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2234";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.PassSystemUriObjectsInsteadOfStringsTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.PassSystemUriObjectsInsteadOfStringsMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.PassSystemUriObjectsInsteadOfStringsDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Usage,
                                                                             RuleLevel.Disabled,        // Heuristics based rules are prone to false positives
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            // this is stateless analyzer, can run concurrently
            context.EnableConcurrentExecution();

            // this has no meaning on running on generated code which user can't control
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(c =>
            {
                INamedTypeSymbol? @string = c.Compilation.GetSpecialType(SpecialType.System_String);
                INamedTypeSymbol? uri = c.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemUri);
                if (@string == null || uri == null)
                {
                    // we don't have required types
                    return;
                }

                var analyzer = new PerCompilationAnalyzer(c.Compilation, @string, uri, GetInvocationExpression);
                c.RegisterOperationAction(analyzer.Analyze, OperationKind.Invocation);
            });
        }

        protected abstract SyntaxNode? GetInvocationExpression(SyntaxNode invocationNode);

        private sealed class PerCompilationAnalyzer
        {
            // this type will be created per compilation
            private readonly Compilation _compilation;
            private readonly INamedTypeSymbol _string;
            private readonly INamedTypeSymbol _uri;
            private readonly Func<SyntaxNode, SyntaxNode?> _expressionGetter;

            public PerCompilationAnalyzer(
                Compilation compilation,
                INamedTypeSymbol @string,
                INamedTypeSymbol uri,
                Func<SyntaxNode, SyntaxNode?> expressionGetter)
            {
                _compilation = compilation;
                _string = @string;
                _uri = uri;
                _expressionGetter = expressionGetter;
            }

            public void Analyze(OperationAnalysisContext context)
            {
                var invocation = (IInvocationOperation)context.Operation;
                var method = invocation.TargetMethod;

                // check basic stuff that FxCop checks.
                if (method.IsFromMscorlib(_compilation))
                {
                    // Methods defined within mscorlib are excluded from this rule,
                    // since mscorlib cannot depend on System.Uri, which is defined
                    // in System.dll
                    return;
                }

                if (!context.Options.MatchesConfiguredVisibility(Rule, method, context.ContainingSymbol, context.Compilation))
                {
                    // only apply to methods that are exposed outside by default
                    return;
                }

                var node = _expressionGetter(context.Operation.Syntax);
                if (node == null)
                {
                    // we don't have right expression node to check overloads
                    return;
                }

                var stringParameters = method.Parameters.GetParametersOfType(_string);
                if (!stringParameters.Any())
                {
                    // no string parameter. not interested.
                    return;
                }

                // now do cheap string check whether those string parameter contains uri word list we are looking for.
                if (!stringParameters.ParameterNamesContainUriWordSubstring(context.CancellationToken))
                {
                    // no string parameter that contains what we are looking for.
                    return;
                }

                // now we make sure we actually have overloads that contains uri type parameter
                var overloads = context.Operation.SemanticModel.GetMemberGroup(node, context.CancellationToken).OfType<IMethodSymbol>();
                if (!overloads.HasOverloadWithParameterOfType(method, _uri, context.CancellationToken))
                {
                    // no overload that contains uri as parameter
                    return;
                }

                // now we do more expensive word parsing to find exact parameter that contains url in parameter name
                var indicesSet = new HashSet<int>(method.GetParameterIndices(stringParameters.GetParametersThatContainUriWords(context.CancellationToken), context.CancellationToken));

                // now we search exact match. this is exactly same behavior as old FxCop
                foreach (IMethodSymbol overload in overloads)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();

                    if (method.Equals(overload) || overload.Parameters.Length != method.Parameters.Length)
                    {
                        // either itself, or signature is not same
                        continue;
                    }

                    if (!method.ParameterTypesAreSame(overload, Enumerable.Range(0, method.Parameters.Length).Where(i => !indicesSet.Contains(i)), context.CancellationToken))
                    {
                        // check whether remaining parameters match existing types, otherwise, we are not interested
                        continue;
                    }

                    // original FxCop implementation doesn't account for case where original method call contains
                    // 2+ string uri parameters that has overload with matching uri parameters. original implementation works
                    // when there is exactly 1 parameter having matching uri overload. this implementation follow that.
                    foreach (int index in indicesSet)
                    {
                        // check other string uri parameters matches original type
                        if (!method.ParameterTypesAreSame(overload, indicesSet.Where(i => i != index), context.CancellationToken))
                        {
                            continue;
                        }

                        // okay all other type match. check the main one
                        if (overload.Parameters[index].Type?.Equals(_uri) == true &&
                            !Equals(overload, context.ContainingSymbol))
                        {
                            context.ReportDiagnostic(
                                node.CreateDiagnostic(
                                    Rule,
                                    context.ContainingSymbol.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat),
                                    overload.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat),
                                    method.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat)));

                            // we no longer interested in this overload. there can be only 1 match
                            break;
                        }
                    }
                }
            }
        }
    }
}
