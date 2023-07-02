// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Performance
{
    using static MicrosoftNetCoreAnalyzersResources;
    /// <summary>
    /// CA1853: <inheritdoc cref="DoNotGuardDictionaryRemoveByContainsKeyTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotGuardDictionaryRemoveByContainsKey : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1853";
        private const string Remove = nameof(Remove);
        private const string ContainsKey = nameof(ContainsKey);

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(DoNotGuardDictionaryRemoveByContainsKeyTitle)),
            CreateLocalizableResourceString(nameof(DoNotGuardDictionaryRemoveByContainsKeyMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(DoNotGuardDictionaryRemoveByContainsKeyDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            if (!TryGetDictionaryTypeAndMethods(context.Compilation, out var containsKey, out var remove1Param, out var remove2Param))
            {
                return;
            }

            context.RegisterOperationAction(context => AnalyzeOperation(context, containsKey, remove1Param, remove2Param), OperationKind.Conditional);

            static void AnalyzeOperation(OperationAnalysisContext context, IMethodSymbol containsKeyMethod, IMethodSymbol remove1Param, IMethodSymbol? remove2Param)
            {
                var conditionalOperation = (IConditionalOperation)context.Operation;

                IInvocationOperation? invocationOperation = null;

                switch (conditionalOperation.Condition.WalkDownParentheses())
                {
                    case IInvocationOperation iOperation:
                        invocationOperation = iOperation;
                        break;
                    case IUnaryOperation unaryOperation when unaryOperation.OperatorKind == UnaryOperatorKind.Not:
                        if (unaryOperation.Operand is IInvocationOperation operand)
                            invocationOperation = operand;
                        break;
                    default:
                        return;
                }

                if (invocationOperation == null || !invocationOperation.TargetMethod.OriginalDefinition.Equals(containsKeyMethod, SymbolEqualityComparer.Default))
                {
                    return;
                }

                if (conditionalOperation.WhenTrue.Children.Any())
                {
                    using var additionalLocation = ArrayBuilder<Location>.GetInstance(2);
                    additionalLocation.Add(conditionalOperation.Syntax.GetLocation());

                    switch (conditionalOperation.WhenTrue.Children.First())
                    {
                        case IInvocationOperation childInvocationOperation:
                            if ((childInvocationOperation.TargetMethod.OriginalDefinition.Equals(remove1Param, SymbolEqualityComparer.Default) ||
                                 childInvocationOperation.TargetMethod.OriginalDefinition.Equals(remove2Param, SymbolEqualityComparer.Default)) &&
                                AreInvocationsOnSameInstance(childInvocationOperation, invocationOperation))
                            {
                                additionalLocation.Add(childInvocationOperation.Syntax.Parent!.GetLocation());
                                context.ReportDiagnostic(invocationOperation.CreateDiagnostic(Rule, additionalLocations: additionalLocation.ToImmutable(), null));
                            }

                            break;
                        case IExpressionStatementOperation childStatementOperation:
                            /*
                             * If the if statement contains a block, only proceed if one of the methods calls Remove.
                             * However, a fixer is only offered if there is a single method in the block.
                             */

                            var nestedInvocationOperation = childStatementOperation.Children.OfType<IInvocationOperation>()
                                                            .FirstOrDefault(op => op.TargetMethod.OriginalDefinition.Equals(remove1Param, SymbolEqualityComparer.Default) ||
                                                                                  op.TargetMethod.OriginalDefinition.Equals(remove2Param, SymbolEqualityComparer.Default));

                            if (nestedInvocationOperation != null && AreInvocationsOnSameInstance(nestedInvocationOperation, invocationOperation))
                            {
                                additionalLocation.Add(nestedInvocationOperation.Syntax.Parent!.GetLocation());
                                context.ReportDiagnostic(invocationOperation.CreateDiagnostic(Rule, additionalLocations: additionalLocation.ToImmutable(), null));
                            }

                            break;
                        default:
                            break;
                    }
                }
            }

            static bool AreInvocationsOnSameInstance(IInvocationOperation invocationOp1, IInvocationOperation invocationOp2)
            {
                return (invocationOp1.Instance, invocationOp2.Instance) switch
                {
                    (IFieldReferenceOperation fieldRefOp1, IFieldReferenceOperation fieldRefOp2) => fieldRefOp1.Member == fieldRefOp2.Member,
                    (IPropertyReferenceOperation propRefOp1, IPropertyReferenceOperation propRefOp2) => propRefOp1.Member == propRefOp2.Member,
                    (IParameterReferenceOperation paramRefOp1, IParameterReferenceOperation paramRefOp2) => paramRefOp1.Parameter == paramRefOp2.Parameter,
                    (ILocalReferenceOperation localRefOp1, ILocalReferenceOperation localRefOp2) => localRefOp1.Local == localRefOp2.Local,
                    _ => false,
                };
            }

            static bool TryGetDictionaryTypeAndMethods(Compilation compilation, [NotNullWhen(true)] out IMethodSymbol? containsKey,
                            [NotNullWhen(true)] out IMethodSymbol? remove1Param, out IMethodSymbol? remove2Param)
            {
                containsKey = null;
                remove1Param = null;
                remove2Param = null;

                if (!compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericDictionary2, out var dictionary))
                {
                    return false;
                }

                foreach (var m in dictionary.GetMembers().OfType<IMethodSymbol>())
                {
                    if (m.ReturnType.SpecialType == SpecialType.System_Boolean)
                    {
                        switch (m.Parameters.Length)
                        {
                            case 1:
                                switch (m.Name)
                                {
                                    case ContainsKey: containsKey = m; break;
                                    case Remove: remove1Param = m; break;
                                }

                                break;
                            case 2:
                                if (m.Name == Remove)
                                {
                                    remove2Param = m;
                                }

                                break;
                        }
                    }
                }

                return containsKey != null && remove1Param != null;
            }
        }
    }
}
