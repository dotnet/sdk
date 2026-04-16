// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.ImmutableCollections
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA2009: <inheritdoc cref="DoNotCallToImmutableCollectionOnAnImmutableCollectionValueTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotCallToImmutableCollectionOnAnImmutableCollectionValueAnalyzer : DiagnosticAnalyzer
    {
        private const string ImmutableArrayMetadataName = "System.Collections.Immutable.ImmutableArray`1";
        internal const string RuleId = "CA2009";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(DoNotCallToImmutableCollectionOnAnImmutableCollectionValueTitle)),
            CreateLocalizableResourceString(nameof(DoNotCallToImmutableCollectionOnAnImmutableCollectionValueMessage)),
            DiagnosticCategory.Reliability,
            RuleLevel.IdeSuggestion,
            description: null,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        private static readonly ImmutableDictionary<string, string> ImmutableCollectionMetadataNames = new Dictionary<string, string>
        {
            ["ToImmutableArray"] = "System.Collections.Immutable.ImmutableArray`1",
            ["ToImmutableDictionary"] = "System.Collections.Immutable.ImmutableDictionary`2",
            ["ToImmutableHashSet"] = "System.Collections.Immutable.ImmutableHashSet`1",
            ["ToImmutableList"] = "System.Collections.Immutable.ImmutableList`1",
            ["ToImmutableSortedDictionary"] = "System.Collections.Immutable.ImmutableSortedDictionary`2",
            ["ToImmutableSortedSet"] = "System.Collections.Immutable.ImmutableSortedSet`1",
        }.ToImmutableDictionary();

        public static ImmutableArray<string> ToImmutableMethodNames { get; } = ImmutableCollectionMetadataNames.Keys.ToImmutableArray();

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationStartContext =>
            {
                var compilation = compilationStartContext.Compilation;
                var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilation);
                var immutableArraySymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(ImmutableArrayMetadataName);
                if (immutableArraySymbol is null)
                {
                    return;
                }

                var immutableCollectionsAssembly = immutableArraySymbol.ContainingAssembly;

                compilationStartContext.RegisterOperationAction(operationContext =>
                {
                    var invocation = (IInvocationOperation)operationContext.Operation;
                    var targetMethod = invocation.TargetMethod;
                    if (targetMethod == null || !ImmutableCollectionMetadataNames.TryGetValue(targetMethod.Name, out var metadataName))
                    {
                        return;
                    }

                    Debug.Assert(!string.IsNullOrEmpty(metadataName));

                    // Do not flag invocations that take any explicit argument (comparer, converter, etc.)
                    // as they can potentially modify the contents of the resulting collection.
                    var argumentsToSkip = invocation.IsExtensionMethodAndHasNoInstance() ? 1 : 0;
                    if (invocation.Arguments.Skip(argumentsToSkip).Any(arg => arg.ArgumentKind == ArgumentKind.Explicit))
                    {
                        return;
                    }

                    var immutableCollectionType = immutableCollectionsAssembly.GetTypeByMetadataName(metadataName);
                    if (immutableCollectionType == null)
                    {
                        // The user might be running against a custom system assembly that defines ImmutableArray,
                        // but not other immutable collection types.
                        return;
                    }

                    if (invocation.GetReceiverType(operationContext.Compilation, beforeConversion: true, cancellationToken: operationContext.CancellationToken) is INamedTypeSymbol receiverType
                        && receiverType.DerivesFromOrImplementsAnyConstructionOf(immutableCollectionType))
                    {
                        operationContext.ReportDiagnostic(
                            invocation.CreateDiagnostic(
                                Rule,
                                targetMethod.Name,
                                immutableCollectionType.Name));
                    }
                }, OperationKind.Invocation);
            });
        }
    }
}
