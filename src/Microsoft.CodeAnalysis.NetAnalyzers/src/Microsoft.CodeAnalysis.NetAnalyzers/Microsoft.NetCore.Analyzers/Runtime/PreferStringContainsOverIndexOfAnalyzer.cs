// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// Prefer string.Contains over string.IndexOf when the result is used to check for the presence/absence of a substring
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PreferStringContainsOverIndexOfAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2249";
        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.PreferStringContainsOverIndexOfTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.PreferStringContainsOverIndexOfMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.PreferStringContainsOverIndexOfDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                      s_localizableTitle,
                                                                                      s_localizableMessage,
                                                                                      DiagnosticCategory.Usage,
                                                                                      RuleLevel.IdeSuggestion,
                                                                                      s_localizableDescription,
                                                                                      isPortedFxCopRule: false,
                                                                                      isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(context =>
            {
                if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemString, out INamedTypeSymbol? stringType) ||
                    !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemChar, out INamedTypeSymbol? charType) ||
                    !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemStringComparison, out INamedTypeSymbol? stringComparisonType))
                {
                    return;
                }

                // First get all the string.IndexOf methods that we are interested in tagging
                var stringIndexOfMethods = stringType
                    .GetMembers("IndexOf")
                    .OfType<IMethodSymbol>()
                    .WhereAsArray(s =>
                        s.Parameters.Length <= 2);

                var stringArgumentIndexOfMethod = stringIndexOfMethods.GetFirstOrDefaultMemberWithParameterInfos(
                        ParameterInfo.GetParameterInfo(stringType));
                var charArgumentIndexOfMethod = stringIndexOfMethods.GetFirstOrDefaultMemberWithParameterInfos(
                        ParameterInfo.GetParameterInfo(charType));
                var stringAndComparisonTypeArgumentIndexOfMethod = stringIndexOfMethods.GetFirstOrDefaultMemberWithParameterInfos(
                        ParameterInfo.GetParameterInfo(stringType),
                        ParameterInfo.GetParameterInfo(stringComparisonType));
                var charAndComparisonTypeArgumentIndexOfMethod = stringIndexOfMethods.GetFirstOrDefaultMemberWithParameterInfos(
                        ParameterInfo.GetParameterInfo(charType),
                        ParameterInfo.GetParameterInfo(stringComparisonType));

                // Check that the contains methods that take 2 parameters exist
                // string.Contains(char) is also .NETStandard2.1+
                var stringContainsMethods = stringType
                    .GetMembers("Contains")
                    .OfType<IMethodSymbol>()
                    .WhereAsArray(s =>
                        s.Parameters.Length <= 2);
                var stringAndComparisonTypeArgumentContainsMethod = stringContainsMethods.GetFirstOrDefaultMemberWithParameterInfos(
                        ParameterInfo.GetParameterInfo(stringType),
                        ParameterInfo.GetParameterInfo(stringComparisonType));
                var charAndComparisonTypeArgumentContainsMethod = stringContainsMethods.GetFirstOrDefaultMemberWithParameterInfos(
                        ParameterInfo.GetParameterInfo(charType),
                        ParameterInfo.GetParameterInfo(stringComparisonType));
                var charArgumentContainsMethod = stringContainsMethods.GetFirstOrDefaultMemberWithParameterInfos(
                        ParameterInfo.GetParameterInfo(charType));
                if (stringAndComparisonTypeArgumentContainsMethod == null ||
                    charAndComparisonTypeArgumentContainsMethod == null ||
                    charArgumentContainsMethod == null)
                {
                    return;
                }

                // Roslyn doesn't yet support "FindAllReferences" at a file/block level. So instead, find references to local int variables in this block.
                context.RegisterOperationBlockStartAction(OnOperationBlockStart);
                return;

                void OnOperationBlockStart(OperationBlockStartAnalysisContext context)
                {
                    if (context.OwningSymbol is not IMethodSymbol method)
                    {
                        return;
                    }

                    // Algorithm:
                    // We aim to change string.IndexOf -> string.Contains
                    // 1. We register 1 callback for invocations of IndexOf.
                    //      1a. Check if invocation.Parent is a binary operation we care about (string.IndexOf >= 0 OR string.IndexOf == -1). If so, report a diagnostic and return from the callback.
                    //      1b. Otherwise, check if invocation.Parent is a variable declarator. If so, add the invocation as a potential violation to track into variableNameToOperationsMap.
                    // 2. We register another callback for local references
                    //      2a. If the local reference is not a type int, bail out.
                    //      2b. If the local reference operation's parent is not a binary operation, add it to "localsToBailOut".
                    // 3. In an operation block end, we check if entries in "variableNameToOperationsMap" exist in "localToBailOut". If an entry is NOT present, we report a diagnostic at that invocation.
                    PooledConcurrentSet<ILocalSymbol> localsToBailOut = PooledConcurrentSet<ILocalSymbol>.GetInstance();
                    PooledConcurrentDictionary<ILocalSymbol, IInvocationOperation> variableNameToOperationsMap = PooledConcurrentDictionary<ILocalSymbol, IInvocationOperation>.GetInstance();

                    context.RegisterOperationAction(PopulateLocalReferencesSet, OperationKind.LocalReference);

                    context.RegisterOperationAction(AnalyzeInvocationOperation, OperationKind.Invocation);

                    context.RegisterOperationBlockEndAction(OnOperationBlockEnd);

                    return;

                    // Local Functions
                    void PopulateLocalReferencesSet(OperationAnalysisContext context)
                    {
                        ILocalReferenceOperation localReference = (ILocalReferenceOperation)context.Operation;
                        if (localReference.Local.Type.SpecialType != SpecialType.System_Int32)
                        {
                            return;
                        }

                        var parent = localReference.Parent;
                        if (parent is IBinaryOperation binaryOperation)
                        {
                            var otherOperand = binaryOperation.LeftOperand is ILocalReferenceOperation ? binaryOperation.RightOperand : binaryOperation.LeftOperand;
                            if (CheckOperatorKindAndOperand(binaryOperation, otherOperand))
                            {
                                // Do nothing. This is a valid case to the tagged in the analyzer
                                return;
                            }
                        }
                        localsToBailOut.Add(localReference.Local);
                    }

                    void AnalyzeInvocationOperation(OperationAnalysisContext context)
                    {
                        var invocationOperation = (IInvocationOperation)context.Operation;
                        if (!IsDesiredTargetMethod(invocationOperation.TargetMethod))
                        {
                            return;
                        }

                        var parent = invocationOperation.Parent;
                        if (parent is IBinaryOperation binaryOperation)
                        {
                            var otherOperand = binaryOperation.LeftOperand is IInvocationOperation ? binaryOperation.RightOperand : binaryOperation.LeftOperand;
                            if (CheckOperatorKindAndOperand(binaryOperation, otherOperand))
                            {
                                context.ReportDiagnostic(binaryOperation.CreateDiagnostic(Rule));
                            }
                        }
                        else if (parent is IVariableInitializerOperation variableInitializer)
                        {
                            if (variableInitializer.Parent is IVariableDeclaratorOperation variableDeclaratorOperation)
                            {
                                variableNameToOperationsMap.TryAdd(variableDeclaratorOperation.Symbol, invocationOperation);
                            }
                            else if (variableInitializer.Parent is IVariableDeclarationOperation variableDeclarationOperation && variableDeclarationOperation.Declarators.Length == 1)
                            {
                                variableNameToOperationsMap.TryAdd(variableDeclarationOperation.Declarators[0].Symbol, invocationOperation);
                            }
                        }
                    }

                    static bool CheckOperatorKindAndOperand(IBinaryOperation binaryOperation, IOperation otherOperand)
                    {
                        var operatorKind = binaryOperation.OperatorKind;
                        if (otherOperand.ConstantValue.HasValue && otherOperand.ConstantValue.Value is int intValue)
                        {
                            if ((operatorKind == BinaryOperatorKind.Equals && intValue < 0) ||
                                (operatorKind == BinaryOperatorKind.GreaterThanOrEqual && intValue == 0))
                            {
                                // This is the only case we are targeting in this analyzer
                                return true;
                            }
                        }
                        return false;
                    }

                    void OnOperationBlockEnd(OperationBlockAnalysisContext context)
                    {
                        foreach (var variableNameAndLocation in variableNameToOperationsMap)
                        {
                            ILocalSymbol variable = variableNameAndLocation.Key;
                            if (!localsToBailOut.Contains(variable))
                            {
                                context.ReportDiagnostic(variableNameAndLocation.Value.CreateDiagnostic(Rule));
                            }
                        }
                        variableNameToOperationsMap.Free(context.CancellationToken);
                        localsToBailOut.Free(context.CancellationToken);
                    }

                    bool IsDesiredTargetMethod(IMethodSymbol targetMethod) =>
                         targetMethod.Equals(stringArgumentIndexOfMethod)
                         || targetMethod.Equals(charArgumentIndexOfMethod)
                         || targetMethod.Equals(stringAndComparisonTypeArgumentIndexOfMethod)
                         || targetMethod.Equals(charAndComparisonTypeArgumentIndexOfMethod);
                }
            });
        }
    }
}
