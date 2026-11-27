// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;

namespace Microsoft.NetCore.Analyzers.Tasks
{
    /// <summary>
    /// CA2029: <inheritdoc cref="DoNotSetApartmentStateOfRunningThreadTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotSetApartmentStateOfRunningThread : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2029";
        private const string CurrentThread = nameof(CurrentThread);
        private const string SetApartmentState = nameof(SetApartmentState);
        private const string TrySetApartmentState = nameof(TrySetApartmentState);
        private const string STA = nameof(STA);

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(DoNotSetApartmentStateOfRunningThreadTitle)),
            CreateLocalizableResourceString(nameof(DoNotSetApartmentStateOfRunningThreadMessage)),
            DiagnosticCategory.Reliability,
            RuleLevel.BuildWarning,
            CreateLocalizableResourceString(nameof(DoNotSetApartmentStateOfRunningThreadDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                var compilation = context.Compilation;

                if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSTAThreadAttribute, out var staThreadAttributeType))
                {
                    var entryPoint = compilation.GetEntryPoint(context.CancellationToken);

                    context.RegisterSymbolAction(context =>
                    {
                        var method = (IMethodSymbol)context.Symbol;

                        if (!method.IsAsync &&
                            (entryPoint is null || SymbolEqualityComparer.Default.Equals(method, entryPoint)))
                        {
                            return;
                        }

                        foreach (var attribute in method.GetAttributes())
                        {
                            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, staThreadAttributeType) &&
                                attribute.ApplicationSyntaxReference is { } syntaxReference)
                            {
                                context.ReportDiagnostic(syntaxReference.GetSyntax(context.CancellationToken).CreateDiagnostic(Rule, "STAThread"));
                            }
                        }
                    }, SymbolKind.Method);
                }

                if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingThread, out var threadType) &&
                    threadType.GetMembers(CurrentThread).OfType<IPropertySymbol>().FirstOrDefault() is { } currentThreadProperty &&
                    compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingApartmentState, out var apartmentStateType) &&
                    apartmentStateType.GetMembers(STA).OfType<IFieldSymbol>().FirstOrDefault() is { } staField)
                {
                    var setApartmentStateMethods = threadType.GetMembers(SetApartmentState)
                        .AddRange(threadType.GetMembers(TrySetApartmentState))
                        .OfType<IMethodSymbol>()
                        .ToImmutableArray();

                    context.RegisterOperationAction(context =>
                    {
                        var invocation = (IInvocationOperation)context.Operation;

                        if (!setApartmentStateMethods.Contains(invocation.TargetMethod, SymbolEqualityComparer.Default) ||
                            invocation.Instance is not IPropertyReferenceOperation { Property: var property } ||
                            !SymbolEqualityComparer.Default.Equals(property, currentThreadProperty))
                        {
                            return;
                        }

                        var containingSymbol = invocation.TryGetContainingAnonymousFunctionOrLocalFunction() ?? context.ContainingSymbol;

                        if (containingSymbol is IMethodSymbol { IsAsync: true } ||
                            invocation.Arguments.GetArgumentForParameterAtIndex(0).Value.ConstantValue is { HasValue: true } constant &&
                                Equals(constant.Value, staField.ConstantValue))
                        {
                            context.ReportDiagnostic(invocation.CreateDiagnostic(Rule, invocation.TargetMethod.Name));
                        }
                    }, OperationKind.Invocation);
                }
            });
        }
    }
}
