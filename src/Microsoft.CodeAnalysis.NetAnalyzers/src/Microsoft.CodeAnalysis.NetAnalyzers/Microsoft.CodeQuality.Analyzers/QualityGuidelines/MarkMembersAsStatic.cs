// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1822: <inheritdoc cref="MarkMembersAsStaticTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class MarkMembersAsStaticAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1822";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(MarkMembersAsStaticTitle)),
            CreateLocalizableResourceString(nameof(MarkMembersAsStaticMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(MarkMembersAsStaticDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Don't report in generated code since that's not actionable.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(context.Compilation);

                // Get the list of all method' attributes for which the rule shall not be triggered.
                ImmutableArray<INamedTypeSymbol> skippedAttributes = GetSkippedAttributes(wellKnownTypeProvider);

                var isWebProject = context.Compilation.IsWebProject(context.Options);

                context.RegisterSymbolStartAction(
                    context => OnSymbolStart(context, wellKnownTypeProvider, skippedAttributes, isWebProject),
                    SymbolKind.NamedType);
            });

            return;

            static void OnSymbolStart(
                SymbolStartAnalysisContext context,
                WellKnownTypeProvider wellKnownTypeProvider,
                ImmutableArray<INamedTypeSymbol> skippedAttributes,
                bool isWebProject)
            {
                // Since property/event accessors cannot be marked static themselves and the associated symbol (property/event)
                // has to be marked static, we want to report the diagnostic on the property/event.
                // So we make a note of the property/event symbols which have at least one accessor with no instance access.
                // At symbol end, we report candidate property/event symbols whose all accessors are candidates to be marked static.
                var propertyOrEventCandidates = TemporarySet<ISymbol>.Empty;
                var accessorCandidates = TemporarySet<IMethodSymbol>.Empty;

                var methodCandidates = TemporarySet<IMethodSymbol>.Empty;

                // Do not flag methods that are used as delegates: https://github.com/dotnet/roslyn-analyzers/issues/1511
                var methodsUsedAsDelegates = TemporarySet<IMethodSymbol>.Empty;

                context.RegisterOperationAction(OnMethodReference, OperationKind.MethodReference);
                context.RegisterOperationBlockStartAction(OnOperationBlockStart);
                context.RegisterSymbolEndAction(OnSymbolEnd);

                return;

                void OnMethodReference(OperationAnalysisContext context)
                {
                    var methodReference = (IMethodReferenceOperation)context.Operation;
                    methodsUsedAsDelegates.Add(methodReference.Method, context.CancellationToken);
                }

                void OnOperationBlockStart(OperationBlockStartAnalysisContext context)
                {
                    if (context.OwningSymbol is not IMethodSymbol methodSymbol)
                    {
                        return;
                    }

                    // Don't run any other check for this method if it isn't a valid analysis context
                    if (!ShouldAnalyze(methodSymbol, wellKnownTypeProvider, skippedAttributes, isWebProject, context))
                    {
                        return;
                    }

                    bool isInstanceReferenced = false;

                    context.RegisterOperationAction(context =>
                    {
                        if (context.Operation is IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance }
                            && (context.Operation.Parent is not IInvocationOperation invocation || !invocation.TargetMethod.Equals(methodSymbol, SymbolEqualityComparer.Default)))
                        {
                            isInstanceReferenced = true;
                        }
                    }, OperationKind.InstanceReference);

                    // Workaround for https://github.com/dotnet/roslyn/issues/27564
                    context.RegisterOperationAction(context =>
                    {
                        if (!context.Operation.IsOperationNoneRoot())
                        {
                            isInstanceReferenced = true;
                        }
                    }, OperationKind.None);

                    context.RegisterOperationAction(context =>
                    {
                        if (context.Operation is IParameterReferenceOperation { Parameter.ContainingSymbol: IMethodSymbol { MethodKind: MethodKind.Constructor } })
                        {
                            // we're referencing a parameter not from our actual method, but from a type constructor.
                            // This must be a primary constructor scenario, and we're capturing the parameter here.  
                            // This member cannot be made static.
                            isInstanceReferenced = true;
                        }
                    }, OperationKind.ParameterReference);

                    context.RegisterOperationBlockEndAction(context =>
                    {
                        if (!isInstanceReferenced)
                        {
                            if (methodSymbol.IsAccessorMethod())
                            {
                                accessorCandidates.Add(methodSymbol, context.CancellationToken);
                                propertyOrEventCandidates.Add(methodSymbol.AssociatedSymbol!, context.CancellationToken);
                            }
                            else if (methodSymbol.IsExternallyVisible())
                            {
                                if (!IsOnObsoleteMemberChain(methodSymbol, wellKnownTypeProvider))
                                {
                                    context.ReportDiagnostic(methodSymbol.CreateDiagnostic(Rule, methodSymbol.Name));
                                }
                            }
                            else
                            {
                                methodCandidates.Add(methodSymbol, context.CancellationToken);
                            }
                        }
                    });
                }

                void OnSymbolEnd(SymbolAnalysisContext context)
                {
                    foreach (var candidate in methodCandidates.NonConcurrentEnumerable)
                    {
                        if (methodsUsedAsDelegates.Contains_NonConcurrent(candidate))
                        {
                            continue;
                        }

                        if (!IsOnObsoleteMemberChain(candidate, wellKnownTypeProvider))
                        {
                            context.ReportDiagnostic(candidate.CreateDiagnostic(Rule, candidate.Name));
                        }
                    }

                    foreach (var candidatePropertyOrEvent in propertyOrEventCandidates.NonConcurrentEnumerable)
                    {
                        var allAccessorsAreCandidates = true;
                        foreach (var accessor in candidatePropertyOrEvent.GetAccessors())
                        {
                            if (!accessorCandidates.Contains_NonConcurrent(accessor) ||
                                IsOnObsoleteMemberChain(accessor, wellKnownTypeProvider))
                            {
                                allAccessorsAreCandidates = false;
                                break;
                            }
                        }

                        if (allAccessorsAreCandidates)
                        {
                            context.ReportDiagnostic(candidatePropertyOrEvent.CreateDiagnostic(Rule, candidatePropertyOrEvent.Name));
                        }
                    }

                    propertyOrEventCandidates.Free(context.CancellationToken);
                    accessorCandidates.Free(context.CancellationToken);
                    methodCandidates.Free(context.CancellationToken);
                    methodsUsedAsDelegates.Free(context.CancellationToken);
                }
            }
        }

        private static bool ShouldAnalyze(
            IMethodSymbol methodSymbol,
            WellKnownTypeProvider wellKnownTypeProvider,
            ImmutableArray<INamedTypeSymbol> skippedAttributes,
            bool isWebProject,
#pragma warning disable RS1012 // Start action has no registered actions
            OperationBlockStartAnalysisContext context)
#pragma warning restore RS1012 // Start action has no registered actions
        {
            // Modifiers that we don't care about
            // 'PartialImplementationPart is not null' means the method is partial, and the
            // symbol we have is the "definition", not the "implementation".
            // We should only analyze the implementation.
            if (methodSymbol.IsStatic || methodSymbol.IsOverride || methodSymbol.IsVirtual ||
                methodSymbol.IsExtern || methodSymbol.IsAbstract || methodSymbol.PartialImplementationPart is not null ||
                methodSymbol.IsImplementationOfAnyInterfaceMember())
            {
                return false;
            }

            // Do not analyze constructors, finalizers, and indexers.
            if (methodSymbol.IsConstructor() || methodSymbol.IsFinalizer() || methodSymbol.AssociatedSymbol.IsIndexer())
            {
                return false;
            }

            // Don't report methods which have a single throw statement
            // with NotImplementedException or NotSupportedException
            if (context.IsMethodNotImplementedOrSupported())
            {
                return false;
            }

            if (methodSymbol.IsExternallyVisible())
            {
                // Do not analyze public APIs for web projects
                // See https://github.com/dotnet/roslyn-analyzers/issues/3835 for details.
                if (isWebProject)
                {
                    return false;
                }

                // CA1000 says one shouldn't declare static members on generic types. So don't flag such cases.
                if (methodSymbol.ContainingType.IsGenericType)
                {
                    return false;
                }
            }

            // We consider that auto-property have the intent to always be instance members so we want to workaround this issue.
            if (methodSymbol.IsAutoPropertyAccessor())
            {
                return false;
            }

            // Awaitable-awaiter pattern members should not be marked as static.
            // There is no need to check for INotifyCompletion or ICriticalNotifyCompletion members as they are already excluded.
            if (wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeCompilerServicesINotifyCompletion, out var inotifyCompletionType)
                && wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeCompilerServicesICriticalNotifyCompletion, out var icriticalNotifyCompletionType))
            {
                if (methodSymbol.IsGetAwaiterFromAwaitablePattern(inotifyCompletionType, icriticalNotifyCompletionType)
                    || methodSymbol.IsGetResultFromAwaiterPattern(inotifyCompletionType, icriticalNotifyCompletionType))
                {
                    return false;
                }

                if (methodSymbol.AssociatedSymbol is IPropertySymbol property
                    && property.IsIsCompletedFromAwaiterPattern(inotifyCompletionType, icriticalNotifyCompletionType))
                {
                    return false;
                }
            }

            var attributes = methodSymbol.GetAttributes();
            if (methodSymbol.AssociatedSymbol != null)
            {
                // For accessors we want to also check the attributes of the associated symbol
                attributes = attributes.AddRange(methodSymbol.AssociatedSymbol.GetAttributes());
            }

            // FxCop doesn't check for the fully qualified name for these attributes - so we'll do the same.
            if (attributes.Any(static (attribute, skippedAttributes) => skippedAttributes.Any(static (attr, attribute) => attribute.AttributeClass.Inherits(attr), attribute), skippedAttributes))
            {
                return false;
            }

            // If this looks like an event handler don't flag such cases.
            // However, we do want to consider EventRaise accessor as a candidate
            // so we can flag the associated event if none of it's accessors need instance reference.
            if (methodSymbol.HasEventHandlerSignature(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemEventArgs)) &&
                methodSymbol.MethodKind != MethodKind.EventRaise)
            {
                return false;
            }

            if (IsExplicitlyVisibleFromCom(methodSymbol, wellKnownTypeProvider))
            {
                return false;
            }

            var hasCorrectVisibility = context.Options.MatchesConfiguredVisibility(Rule, methodSymbol, wellKnownTypeProvider.Compilation,
                defaultRequiredVisibility: SymbolVisibilityGroup.All);
            if (!hasCorrectVisibility)
            {
                return false;
            }

            return true;
        }

        private static bool IsExplicitlyVisibleFromCom(IMethodSymbol methodSymbol, WellKnownTypeProvider wellKnownTypeProvider)
        {
            if (!methodSymbol.IsExternallyVisible() || methodSymbol.IsGenericMethod)
            {
                return false;
            }

            var comVisibleAttribute = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesComVisibleAttribute);
            if (comVisibleAttribute == null)
            {
                return false;
            }

            if (methodSymbol.HasAnyAttribute(comVisibleAttribute) ||
                methodSymbol.ContainingType.HasAnyAttribute(comVisibleAttribute))
            {
                return true;
            }

            return false;
        }

        private static ImmutableArray<INamedTypeSymbol> GetSkippedAttributes(WellKnownTypeProvider wellKnownTypeProvider)
        {
            ImmutableArray<INamedTypeSymbol>.Builder? builder = null;

            void Add(INamedTypeSymbol? symbol)
            {
                if (symbol != null)
                {
                    builder ??= ImmutableArray.CreateBuilder<INamedTypeSymbol>();
                    builder.Add(symbol);
                }
            }

            // MSTest attributes
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestInitializeAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDataTestMethodAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestCleanupAttribute));

            // XUnit attributes
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.XunitFactAttribute));

            // NUnit Attributes
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.NUnitFrameworkSetUpAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.NUnitFrameworkInterfacesITestBuilder));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.NUnitFrameworkOneTimeSetUpAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.NUnitFrameworkOneTimeTearDownAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.NUnitFrameworkTestAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.NUnitFrameworkTearDownAttribute));

            return builder?.ToImmutable() ?? ImmutableArray<INamedTypeSymbol>.Empty;
        }

        private static bool IsOnObsoleteMemberChain(ISymbol symbol, WellKnownTypeProvider wellKnownTypeProvider)
        {
            var obsoleteAttributeType = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemObsoleteAttribute);
            if (obsoleteAttributeType is null)
            {
                return false;
            }

            while (symbol != null)
            {
                if (symbol.HasAnyAttribute(obsoleteAttributeType))
                    return true;

                symbol = symbol is IMethodSymbol method && method.AssociatedSymbol != null
                    ? method.AssociatedSymbol :
                    symbol.ContainingSymbol;
            }

            return false;
        }
    }
}
