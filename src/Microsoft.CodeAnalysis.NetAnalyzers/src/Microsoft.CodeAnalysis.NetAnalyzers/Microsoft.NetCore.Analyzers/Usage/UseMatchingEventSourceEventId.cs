// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Usage
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA2269: <inheritdoc cref="UseMatchingEventSourceEventIdTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseMatchingEventSourceEventIdAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2269";

        internal const string ExpectedEventIdKey = nameof(ExpectedEventIdKey);

        private const string WriteEventMethodName = "WriteEvent";
        private const string EventIdParameterName = "eventId";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(UseMatchingEventSourceEventIdTitle)),
            CreateLocalizableResourceString(nameof(UseMatchingEventSourceEventIdMessage)),
            DiagnosticCategory.Usage,
            RuleLevel.BuildWarning,
            description: CreateLocalizableResourceString(nameof(UseMatchingEventSourceEventIdDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(static context =>
            {
                var typeProvider = WellKnownTypeProvider.GetOrCreate(context.Compilation);
                if (!typeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDiagnosticsTracingEventSource, out var eventSourceType)
                    || !typeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDiagnosticsTracingEventAttribute, out var eventAttributeType))
                {
                    return;
                }

                var writeEventMethods = eventSourceType.GetMembers(WriteEventMethodName)
                    .OfType<IMethodSymbol>()
                    .Where(static m => !m.IsStatic
                        && m.Parameters.Length > 0
                        && m.Parameters[0].Name == EventIdParameterName
                        && m.Parameters[0].Type.SpecialType == SpecialType.System_Int32)
                    .ToImmutableHashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
                if (writeEventMethods.IsEmpty)
                {
                    return;
                }

                context.RegisterOperationBlockStartAction(context =>
                {
                    if (context.OwningSymbol is not IMethodSymbol method
                        || !method.ContainingType.Inherits(eventSourceType)
                        || !TryGetEventId(method, eventAttributeType, out int expectedEventId))
                    {
                        return;
                    }

                    context.RegisterOperationAction(
                        context => AnalyzeInvocation(context, writeEventMethods, expectedEventId),
                        OperationKind.Invocation);
                });
            });
        }

        private static bool TryGetEventId(IMethodSymbol method, INamedTypeSymbol eventAttributeType, out int eventId)
        {
            foreach (var attribute in method.GetAttributes())
            {
                // [Event(1)]
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, eventAttributeType)
                    && attribute.ConstructorArguments is [{ Value: int id }])
                {
                    eventId = id;
                    return true;
                }
            }

            eventId = 0;
            return false;
        }

        private static void AnalyzeInvocation(OperationAnalysisContext context, ImmutableHashSet<IMethodSymbol> writeEventMethods, int expectedEventId)
        {
            var invocation = (IInvocationOperation)context.Operation;
            if (!IsWriteEventOnCurrentInstance(invocation, writeEventMethods)
                || IsInNestedFunction(invocation)
                || !invocation.Arguments.TryGetArgumentForParameterAtIndex(0, out var eventIdArgument)
                || eventIdArgument.Value.ConstantValue is not { HasValue: true, Value: int actualEventId }
                || actualEventId == expectedEventId)
            {
                return;
            }

            var properties = ImmutableDictionary<string, string?>.Empty.Add(
                ExpectedEventIdKey,
                expectedEventId.ToString(CultureInfo.InvariantCulture));

            context.ReportDiagnostic(eventIdArgument.Value.CreateDiagnostic(
                Rule,
                properties,
                expectedEventId,
                actualEventId));
        }

        private static bool IsWriteEventOnCurrentInstance(IInvocationOperation invocation, ImmutableHashSet<IMethodSymbol> writeEventMethods)
        {
            // this.WriteEvent(...), base.WriteEvent(...), or WriteEvent(...)
            return writeEventMethods.Contains(invocation.TargetMethod.OriginalDefinition)
                && invocation.Instance is IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance };
        }

        private static bool IsInNestedFunction(IOperation operation)
        {
            for (var current = operation.Parent; current is not null; current = current.Parent)
            {
                if (current is IAnonymousFunctionOperation or ILocalFunctionOperation)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
