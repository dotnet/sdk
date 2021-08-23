// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA1816: Dispose methods should call SuppressFinalize
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class CallGCSuppressFinalizeCorrectlyAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1816";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.CallGCSuppressFinalizeCorrectlyTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        private static readonly LocalizableString s_localizableMessageNotCalledWithFinalizer = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.CallGCSuppressFinalizeCorrectlyMessageNotCalledWithFinalizer), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageNotCalled = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.CallGCSuppressFinalizeCorrectlyMessageNotCalled), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageNotPassedThis = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.CallGCSuppressFinalizeCorrectlyMessageNotPassedThis), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageOutsideDispose = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.CallGCSuppressFinalizeCorrectlyMessageOutsideDispose), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.CallGCSuppressFinalizeCorrectlyDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor NotCalledWithFinalizerRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageNotCalledWithFinalizer,
                                                                             DiagnosticCategory.Usage,
                                                                             RuleLevel.BuildWarningCandidate,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);
        internal static DiagnosticDescriptor NotCalledRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageNotCalled,
                                                                             DiagnosticCategory.Usage,
                                                                             RuleLevel.BuildWarningCandidate,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);
        internal static DiagnosticDescriptor NotPassedThisRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageNotPassedThis,
                                                                             DiagnosticCategory.Usage,
                                                                             RuleLevel.BuildWarningCandidate,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);
        internal static DiagnosticDescriptor OutsideDisposeRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageOutsideDispose,
                                                                             DiagnosticCategory.Usage,
                                                                             RuleLevel.BuildWarningCandidate,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(NotCalledWithFinalizerRule, NotCalledRule, NotPassedThisRule, OutsideDisposeRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationContext =>
            {
                var gcSuppressFinalizeMethodSymbol = compilationContext.Compilation
                                                        .GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemGC)
                                                        ?.GetMembers("SuppressFinalize")
                                                        .OfType<IMethodSymbol>()
                                                        .FirstOrDefault();

                if (gcSuppressFinalizeMethodSymbol == null)
                {
                    return;
                }

                compilationContext.RegisterOperationBlockStartAction(operationBlockContext =>
                {
                    if (operationBlockContext.OwningSymbol.Kind != SymbolKind.Method)
                    {
                        return;
                    }

                    var methodSymbol = (IMethodSymbol)operationBlockContext.OwningSymbol;
                    if (methodSymbol.IsExtern || methodSymbol.IsAbstract)
                    {
                        return;
                    }

                    var analyzer = new SuppressFinalizeAnalyzer(methodSymbol, gcSuppressFinalizeMethodSymbol, compilationContext.Compilation);

                    operationBlockContext.RegisterOperationAction(analyzer.Analyze, OperationKind.Invocation);
                    operationBlockContext.RegisterOperationBlockEndAction(analyzer.OperationBlockEndAction);
                });
            });

        }

        private class SuppressFinalizeAnalyzer
        {
            private enum SuppressFinalizeUsage
            {
                CanCall,
                MustCall,
                MustNotCall
            }

            private readonly Compilation _compilation;
            private readonly IMethodSymbol _containingMethodSymbol;
            private readonly IMethodSymbol _gcSuppressFinalizeMethodSymbol;
            private readonly SuppressFinalizeUsage _expectedUsage;

            private bool _suppressFinalizeCalled;

            public SuppressFinalizeAnalyzer(IMethodSymbol methodSymbol, IMethodSymbol gcSuppressFinalizeMethodSymbol, Compilation compilation)
            {
                this._compilation = compilation;
                this._containingMethodSymbol = methodSymbol;
                this._gcSuppressFinalizeMethodSymbol = gcSuppressFinalizeMethodSymbol;

                this._expectedUsage = GetAllowedSuppressFinalizeUsage(_containingMethodSymbol);
            }

            public void Analyze(OperationAnalysisContext analysisContext)
            {
                var invocationExpression = (IInvocationOperation)analysisContext.Operation;
                if (invocationExpression.TargetMethod.OriginalDefinition.Equals(_gcSuppressFinalizeMethodSymbol))
                {
                    _suppressFinalizeCalled = true;

                    // Check for GC.SuppressFinalize outside of IDisposable.Dispose()
                    if (_expectedUsage == SuppressFinalizeUsage.MustNotCall)
                    {
                        analysisContext.ReportDiagnostic(invocationExpression.Syntax.CreateDiagnostic(
                            OutsideDisposeRule,
                            _containingMethodSymbol.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat),
                            _gcSuppressFinalizeMethodSymbol.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat)));
                    }

                    // Checks for GC.SuppressFinalize(this)
                    if (!invocationExpression.Arguments.HasExactly(1))
                    {
                        return;
                    }

                    if (invocationExpression.SemanticModel.GetSymbolInfo(invocationExpression.Arguments.Single().Value.Syntax, analysisContext.CancellationToken).Symbol is not IParameterSymbol parameterSymbol || !parameterSymbol.IsThis)
                    {
                        analysisContext.ReportDiagnostic(invocationExpression.Syntax.CreateDiagnostic(
                            NotPassedThisRule,
                            _containingMethodSymbol.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat),
                            _gcSuppressFinalizeMethodSymbol.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat)));
                    }
                }
            }

            public void OperationBlockEndAction(OperationBlockAnalysisContext context)
            {
                // Check for absence of GC.SuppressFinalize
                if (!_suppressFinalizeCalled && _expectedUsage == SuppressFinalizeUsage.MustCall)
                {
                    var descriptor = _containingMethodSymbol.ContainingType.HasFinalizer() ? NotCalledWithFinalizerRule : NotCalledRule;
                    context.ReportDiagnostic(_containingMethodSymbol.CreateDiagnostic(
                        descriptor,
                        _containingMethodSymbol.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat),
                        _gcSuppressFinalizeMethodSymbol.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat)));
                }
            }

            private SuppressFinalizeUsage GetAllowedSuppressFinalizeUsage(IMethodSymbol method)
            {
                // We allow constructors in sealed types to call GC.SuppressFinalize.
                // This allows types that derive from Component (such SqlConnection)
                // to prevent the finalizer they inherit from Component from ever
                // being called.
                if (method.ContainingType.IsSealed && method.IsConstructor() && !method.IsStatic)
                {
                    return SuppressFinalizeUsage.CanCall;
                }

                if (!method.IsDisposeImplementation(_compilation) && !method.IsAsyncDisposeImplementation(_compilation))
                {
                    return SuppressFinalizeUsage.MustNotCall;
                }

                // If the Dispose method is declared in a sealed type, we do
                // not require that the method calls GC.SuppressFinalize
                var hasFinalizer = method.ContainingType.HasFinalizer();
                if (method.ContainingType.IsSealed && !hasFinalizer)
                {
                    return SuppressFinalizeUsage.CanCall;
                }

                // We don't require that non-public types call GC.SuppressFinalize
                // if they don't have a finalizer as the owner of the assembly can
                // control whether any finalizable types derive from them.
                if (method.ContainingType.DeclaredAccessibility != Accessibility.Public && !hasFinalizer)
                {
                    return SuppressFinalizeUsage.CanCall;
                }

                // Even if the Dispose method is declared on a type without a
                // finalizer, we still require it to call GC.SuppressFinalize to
                // prevent derived finalizable types from having to reimplement
                // IDisposable.Dispose just to call it.
                return SuppressFinalizeUsage.MustCall;
            }
        }
    }
}
