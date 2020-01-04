// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1001: Types that own disposable fields should be disposable
    /// </summary>
    public abstract class TypesThatOwnDisposableFieldsShouldBeDisposableAnalyzer<TTypeDeclarationSyntax> : DiagnosticAnalyzer
            where TTypeDeclarationSyntax : SyntaxNode
    {
        internal const string RuleId = "CA1001";
        internal const string Dispose = "Dispose";

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(RuleId,
                                                                         new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.TypesThatOwnDisposableFieldsShouldBeDisposableTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources)),
                                                                         new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.TypesThatOwnDisposableFieldsShouldBeDisposableMessageNonBreaking), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources)),
                                                                         DiagnosticCategory.Design,
                                                                         DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                         isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                                                                         description: new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.TypesThatOwnDisposableFieldsShouldBeDisposableDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources)),
                                                                         helpLinkUri: "https://docs.microsoft.com/visualstudio/code-quality/ca1001-types-that-own-disposable-fields-should-be-disposable",
                                                                         customTags: FxCopWellKnownDiagnosticTags.PortedFxCopRule);

        // Disable analyzer when building the FxCop analyzers VSIX as it gets unconditionally turned on by the default ManagedMinimumRecommended ruleset that ships with FxCop.
        // Rule is not critical to ship in the analyzers VSIX.
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX ? ImmutableArray.Create(Rule) : ImmutableArray<DiagnosticDescriptor>.Empty;

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterCompilationStartAction(compilationContext =>
            {
                if (!compilationContext.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIDisposable, out var disposableType))
                {
                    return;
                }

                DisposableFieldAnalyzer analyzer = GetAnalyzer(compilationContext.Compilation);
                compilationContext.RegisterSymbolAction(analyzer.AnalyzeSymbol, SymbolKind.NamedType);
            });
        }

        protected abstract DisposableFieldAnalyzer GetAnalyzer(Compilation compilation);

        protected abstract class DisposableFieldAnalyzer
        {
            private readonly DisposeAnalysisHelper _disposeAnalysisHelper;

            public DisposableFieldAnalyzer(Compilation compilation)
            {
                DisposeAnalysisHelper.TryGetOrCreate(compilation, out _disposeAnalysisHelper!);
                Debug.Assert(_disposeAnalysisHelper != null);
            }

            public void AnalyzeSymbol(SymbolAnalysisContext symbolContext)
            {
                INamedTypeSymbol namedType = (INamedTypeSymbol)symbolContext.Symbol;
                if (!_disposeAnalysisHelper.IsDisposable(namedType))
                {
                    IEnumerable<IFieldSymbol> disposableFields = from member in namedType.GetMembers()
                                                                 where member.Kind == SymbolKind.Field && !member.IsStatic
                                                                 let field = member as IFieldSymbol
                                                                 where _disposeAnalysisHelper.IsDisposable(field.Type)
                                                                 select field;

                    if (disposableFields.Any())
                    {
                        var disposableFieldsHashSet = new HashSet<ISymbol>(disposableFields);
                        IEnumerable<TTypeDeclarationSyntax> classDecls = GetClassDeclarationNodes(namedType, symbolContext.CancellationToken);
                        foreach (TTypeDeclarationSyntax classDecl in classDecls)
                        {
                            SemanticModel model = symbolContext.Compilation.GetSemanticModel(classDecl.SyntaxTree);
                            IEnumerable<SyntaxNode> syntaxNodes = classDecl.DescendantNodes(n => !(n is TTypeDeclarationSyntax) || ReferenceEquals(n, classDecl))
                                .Where(n => IsDisposableFieldCreation(n,
                                                                    model,
                                                                    disposableFieldsHashSet,
                                                                    symbolContext.CancellationToken));
                            if (syntaxNodes.Any())
                            {
                                // Type '{0}' owns disposable field(s) '{1}' but is not disposable
                                var arg1 = namedType.Name;
                                var arg2 = string.Join(", ", disposableFieldsHashSet.Select(f => f.Name).Order());
                                symbolContext.ReportDiagnostic(namedType.CreateDiagnostic(Rule, arg1, arg2));
                                return;
                            }
                        }
                    }
                }
            }

            private static IEnumerable<TTypeDeclarationSyntax> GetClassDeclarationNodes(INamedTypeSymbol namedType, CancellationToken cancellationToken)
            {
                foreach (SyntaxNode syntax in namedType.DeclaringSyntaxReferences.Select(s => s.GetSyntax(cancellationToken)))
                {
                    if (syntax != null)
                    {
                        TTypeDeclarationSyntax classDecl = syntax.FirstAncestorOrSelf<TTypeDeclarationSyntax>(ascendOutOfTrivia: false);
                        if (classDecl != null)
                        {
                            yield return classDecl;
                        }
                    }
                }
            }

            protected abstract bool IsDisposableFieldCreation(SyntaxNode node, SemanticModel model, HashSet<ISymbol> disposableFields, CancellationToken cancellationToken);
        }
    }
}
