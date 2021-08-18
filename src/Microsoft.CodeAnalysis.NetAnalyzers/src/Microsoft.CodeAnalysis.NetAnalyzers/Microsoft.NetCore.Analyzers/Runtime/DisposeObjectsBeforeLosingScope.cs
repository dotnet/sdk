// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.DisposeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DisposeObjectsBeforeLosingScope : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2000";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DisposeObjectsBeforeLosingScopeTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableNotDisposedMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DisposeObjectsBeforeLosingScopeNotDisposedMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMayBeDisposedMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DisposeObjectsBeforeLosingScopeMayBeDisposedMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableNotDisposedOnExceptionPathsMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DisposeObjectsBeforeLosingScopeNotDisposedOnExceptionPathsMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMayBeDisposedOnExceptionPathsMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DisposeObjectsBeforeLosingScopeMayBeDisposedOnExceptionPathsMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DisposeObjectsBeforeLosingScopeDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor NotDisposedRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                        s_localizableTitle,
                                                                                        s_localizableNotDisposedMessage,
                                                                                        DiagnosticCategory.Reliability,
                                                                                        RuleLevel.Disabled,
                                                                                        description: s_localizableDescription,
                                                                                        isPortedFxCopRule: true,
                                                                                        isDataflowRule: true);

        internal static DiagnosticDescriptor MayBeDisposedRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                          s_localizableTitle,
                                                                                          s_localizableMayBeDisposedMessage,
                                                                                          DiagnosticCategory.Reliability,
                                                                                          RuleLevel.Disabled,
                                                                                          description: s_localizableDescription,
                                                                                          isPortedFxCopRule: true,
                                                                                          isDataflowRule: true);

        internal static DiagnosticDescriptor NotDisposedOnExceptionPathsRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                                        s_localizableTitle,
                                                                                                        s_localizableNotDisposedOnExceptionPathsMessage,
                                                                                                        DiagnosticCategory.Reliability,
                                                                                                        RuleLevel.Disabled,
                                                                                                        description: s_localizableDescription,
                                                                                                        isPortedFxCopRule: true,
                                                                                                        isDataflowRule: true);

        internal static DiagnosticDescriptor MayBeDisposedOnExceptionPathsRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                                          s_localizableTitle,
                                                                                                          s_localizableMayBeDisposedOnExceptionPathsMessage,
                                                                                                          DiagnosticCategory.Reliability,
                                                                                                          RuleLevel.Disabled,
                                                                                                          description: s_localizableDescription,
                                                                                                          isPortedFxCopRule: true,
                                                                                                          isDataflowRule: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(NotDisposedRule, MayBeDisposedRule, NotDisposedOnExceptionPathsRule, MayBeDisposedOnExceptionPathsRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(compilationContext =>
            {
                if (!DisposeAnalysisHelper.TryGetOrCreate(compilationContext.Compilation, out var disposeAnalysisHelper))
                {
                    return;
                }

                var reportedLocations = new ConcurrentDictionary<Location, bool>();
                compilationContext.RegisterOperationBlockAction(operationBlockContext =>
                {
                    if (operationBlockContext.OwningSymbol is not IMethodSymbol containingMethod ||
                        !disposeAnalysisHelper.HasAnyDisposableCreationDescendant(operationBlockContext.OperationBlocks, containingMethod) ||
                        operationBlockContext.Options.IsConfiguredToSkipAnalysis(NotDisposedRule, containingMethod, operationBlockContext.Compilation))
                    {
                        return;
                    }

                    var disposeAnalysisKind = operationBlockContext.Options.GetDisposeAnalysisKindOption(NotDisposedOnExceptionPathsRule, containingMethod,
                        operationBlockContext.Compilation, DisposeAnalysisKind.NonExceptionPaths);
                    var trackExceptionPaths = disposeAnalysisKind.AreExceptionPathsEnabled();

                    // For non-exception paths analysis, we can skip interprocedural analysis for certain invocations.
                    var interproceduralAnalysisPredicate = !trackExceptionPaths ?
                        new InterproceduralAnalysisPredicate(
                            skipAnalysisForInvokedMethodPredicate: SkipInterproceduralAnalysis,
                            skipAnalysisForInvokedLambdaOrLocalFunctionPredicate: null,
                            skipAnalysisForInvokedContextPredicate: null) :
                        null;

                    if (disposeAnalysisHelper.TryGetOrComputeResult(operationBlockContext.OperationBlocks, containingMethod,
                        operationBlockContext.Options, NotDisposedRule, PointsToAnalysisKind.PartialWithoutTrackingFieldsAndProperties, trackInstanceFields: false, trackExceptionPaths: trackExceptionPaths,
                        disposeAnalysisResult: out var disposeAnalysisResult, pointsToAnalysisResult: out var pointsToAnalysisResult, interproceduralAnalysisPredicate: interproceduralAnalysisPredicate))
                    {
                        using var notDisposedDiagnostics = ArrayBuilder<Diagnostic>.GetInstance();
                        using var mayBeNotDisposedDiagnostics = ArrayBuilder<Diagnostic>.GetInstance();

                        // Compute diagnostics for undisposed objects at exit block for non-exceptional exit paths.
                        var exitBlock = disposeAnalysisResult.ControlFlowGraph.GetExit();
                        var disposeDataAtExit = disposeAnalysisResult.ExitBlockOutput.Data;
                        ComputeDiagnostics(disposeDataAtExit,
                            notDisposedDiagnostics, mayBeNotDisposedDiagnostics, disposeAnalysisResult, pointsToAnalysisResult,
                            disposeAnalysisKind, isDisposeDataForExceptionPaths: false);

                        if (trackExceptionPaths)
                        {
                            // Compute diagnostics for undisposed objects at handled exception exit paths.
                            var disposeDataAtHandledExceptionPaths = disposeAnalysisResult.ExceptionPathsExitBlockOutput!.Data;
                            ComputeDiagnostics(disposeDataAtHandledExceptionPaths,
                                notDisposedDiagnostics, mayBeNotDisposedDiagnostics, disposeAnalysisResult, pointsToAnalysisResult,
                                disposeAnalysisKind, isDisposeDataForExceptionPaths: true);

                            // Compute diagnostics for undisposed objects at unhandled exception exit paths, if any.
                            var disposeDataAtUnhandledExceptionPaths = disposeAnalysisResult.MergedStateForUnhandledThrowOperations?.Data;
                            if (disposeDataAtUnhandledExceptionPaths != null)
                            {
                                ComputeDiagnostics(disposeDataAtUnhandledExceptionPaths,
                                    notDisposedDiagnostics, mayBeNotDisposedDiagnostics, disposeAnalysisResult, pointsToAnalysisResult,
                                    disposeAnalysisKind, isDisposeDataForExceptionPaths: true);
                            }
                        }

                        if (!notDisposedDiagnostics.Any() && !mayBeNotDisposedDiagnostics.Any())
                        {
                            return;
                        }

                        // Report diagnostics preferring *not* disposed diagnostics over may be not disposed diagnostics
                        // and avoiding duplicates.
                        foreach (var diagnostic in notDisposedDiagnostics.Concat(mayBeNotDisposedDiagnostics))
                        {
                            if (reportedLocations.TryAdd(diagnostic.Location, true))
                            {
                                operationBlockContext.ReportDiagnostic(diagnostic);
                            }
                        }
                    }
                });

                return;

                // Local functions.

                bool SkipInterproceduralAnalysis(IMethodSymbol invokedMethod)
                {
                    // Skip interprocedural analysis if we are invoking a method and not passing any disposable object as an argument
                    // and not receiving a disposable object as a return value.
                    // We also check that we are not passing any object type argument which might hold disposable object
                    // and also check that we are not passing delegate type argument which can
                    // be a lambda or local function that has access to disposable object in current method's scope.

                    if (CanBeDisposable(invokedMethod.ReturnType))
                    {
                        return false;
                    }

                    foreach (var p in invokedMethod.Parameters)
                    {
                        if (CanBeDisposable(p.Type))
                        {
                            return false;
                        }
                    }

                    return true;

                    bool CanBeDisposable(ITypeSymbol type)
                        => type.SpecialType == SpecialType.System_Object ||
                            type.DerivesFrom(disposeAnalysisHelper!.IDisposable) ||
                            type.TypeKind == TypeKind.Delegate;
                }
            });
        }

        private static void ComputeDiagnostics(
            ImmutableDictionary<AbstractLocation, DisposeAbstractValue> disposeData,
            ArrayBuilder<Diagnostic> notDisposedDiagnostics,
            ArrayBuilder<Diagnostic> mayBeNotDisposedDiagnostics,
            DisposeAnalysisResult disposeAnalysisResult,
            PointsToAnalysisResult pointsToAnalysisResult,
            DisposeAnalysisKind disposeAnalysisKind,
            bool isDisposeDataForExceptionPaths)
        {
            foreach (var kvp in disposeData)
            {
                AbstractLocation location = kvp.Key;
                DisposeAbstractValue disposeValue = kvp.Value;
                if (disposeValue.Kind == DisposeAbstractValueKind.NotDisposable ||
                    location.Creation == null)
                {
                    continue;
                }

                var isNotDisposed = disposeValue.Kind == DisposeAbstractValueKind.NotDisposed ||
                    (!disposeValue.DisposingOrEscapingOperations.IsEmpty &&
                     disposeValue.DisposingOrEscapingOperations.All(d => d.IsInsideCatchRegion(disposeAnalysisResult.ControlFlowGraph) && !location.GetTopOfCreationCallStackOrCreation().IsInsideCatchRegion(disposeAnalysisResult.ControlFlowGraph)));
                var isMayBeNotDisposed = !isNotDisposed && (disposeValue.Kind == DisposeAbstractValueKind.MaybeDisposed || disposeValue.Kind == DisposeAbstractValueKind.NotDisposedOrEscaped);

                if (isNotDisposed ||
                    (isMayBeNotDisposed && disposeAnalysisKind.AreMayBeNotDisposedViolationsEnabled()))
                {
                    var syntax = location.TryGetNodeToReportDiagnostic(pointsToAnalysisResult);
                    if (syntax == null)
                    {
                        continue;
                    }

                    // CA2000: Call System.IDisposable.Dispose on object created by '{0}' before all references to it are out of scope.
                    var rule = GetRule(isNotDisposed);

                    // Ensure that we do not include multiple lines for the object creation expression in the diagnostic message.
                    var argument = syntax.ToString();
                    var indexOfNewLine = argument.IndexOf(Environment.NewLine, StringComparison.Ordinal);
                    if (indexOfNewLine > 0)
                    {
                        argument = argument.Substring(0, indexOfNewLine);
                    }

                    var diagnostic = syntax.CreateDiagnostic(rule, argument);
                    if (isNotDisposed)
                    {
                        notDisposedDiagnostics.Add(diagnostic);
                    }
                    else
                    {
                        mayBeNotDisposedDiagnostics.Add(diagnostic);
                    }
                }
            }

            DiagnosticDescriptor GetRule(bool isNotDisposed)
            {
                if (isNotDisposed)
                {
                    return isDisposeDataForExceptionPaths ? NotDisposedOnExceptionPathsRule : NotDisposedRule;
                }
                else
                {
                    return isDisposeDataForExceptionPaths ? MayBeDisposedOnExceptionPathsRule : MayBeDisposedRule;
                }
            }
        }
    }
}
