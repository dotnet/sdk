// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

using static Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA1854: <inheritdoc cref="PreferDictionaryTryGetValueTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PreferDictionaryTryGetValueAnalyzer : DiagnosticAnalyzer
    {
        public const string RuleId = "CA1854";

        private const string IndexerName = "this[]";
        private const string IndexerNameVb = "Item";
        private const string ContainsKey = nameof(IDictionary<dynamic, dynamic>.ContainsKey);

        private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(PreferDictionaryTryGetValueTitle));
        private static readonly LocalizableString s_localizableTryGetValueMessage = CreateLocalizableResourceString(nameof(PreferDictionaryTryGetValueMessage));
        private static readonly LocalizableString s_localizableTryGetValueDescription = CreateLocalizableResourceString(nameof(PreferDictionaryTryGetValueDescription));

        internal static readonly DiagnosticDescriptor ContainsKeyRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableTryGetValueMessage,
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            s_localizableTryGetValueDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ContainsKeyRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext compilationContext)
        {
            var compilation = compilationContext.Compilation;
            if (!TryGetDictionaryTypeAndMembers(compilation, out var iDictionaryType, out var containsKeySymbol, out var indexerSymbol))
            {
                return;
            }

            compilationContext.RegisterOperationAction(context => OnOperationAction(context, iDictionaryType, containsKeySymbol, indexerSymbol), OperationKind.PropertyReference);
        }

        private static void OnOperationAction(OperationAnalysisContext context, INamedTypeSymbol dictionaryType, IMethodSymbol containsKeySymbol, IPropertySymbol indexerSymbol)
        {
            var propertyReference = (IPropertyReferenceOperation)context.Operation;

            if (propertyReference.Parent is IAssignmentOperation
                || !IsDictionaryAccess(propertyReference, dictionaryType, indexerSymbol)
                || !TryGetParentConditionalOperation(propertyReference, out var conditionalOperation)
                || !TryGetContainsKeyGuard(conditionalOperation, containsKeySymbol, out var containsKeyInvocation))
            {
                return;
            }

            if (conditionalOperation.WhenTrue is IBlockOperation blockOperation && DictionaryEntryIsModified(propertyReference, blockOperation))
            {
                return;
            }

            var additionalLocations = ImmutableArray.Create(propertyReference.Syntax.GetLocation());
            context.ReportDiagnostic(Diagnostic.Create(ContainsKeyRule, containsKeyInvocation.Syntax.GetLocation(), additionalLocations));
        }

        private static bool TryGetDictionaryTypeAndMembers(Compilation compilation,
            [NotNullWhen(true)] out INamedTypeSymbol? iDictionaryType,
            [NotNullWhen(true)] out IMethodSymbol? containsKeySymbol,
            [NotNullWhen(true)] out IPropertySymbol? indexerSymbol)
        {
            containsKeySymbol = null;
            indexerSymbol = null;
            if (!compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericIDictionary2, out iDictionaryType))
            {
                return false;
            }

            containsKeySymbol = iDictionaryType.GetMembers().OfType<IMethodSymbol>().FirstOrDefault(m => m.Name == ContainsKey);
            indexerSymbol = iDictionaryType.GetMembers().OfType<IPropertySymbol>().FirstOrDefault(m => m.Name == IndexerName || m.Language == LanguageNames.VisualBasic && m.Name == IndexerNameVb);

            return containsKeySymbol is not null && indexerSymbol is not null;
        }

        private static bool TryGetContainsKeyGuard(IConditionalOperation conditionalOperation, IMethodSymbol containsKeySymbol, [NotNullWhen(true)] out IInvocationOperation? containsKeyInvocation)
        {
            containsKeyInvocation = FindContainsKeyInvocation(conditionalOperation.Condition, containsKeySymbol);

            return containsKeyInvocation is not null;
        }

        private static IInvocationOperation? FindContainsKeyInvocation(IOperation baseOperation, IMethodSymbol containsKeyMethod)
        {
            return baseOperation switch
            {
                IInvocationOperation i when IsContainsKeyMethod(i.TargetMethod, containsKeyMethod) => i,
                IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalAnd or BinaryOperatorKind.ConditionalOr } b =>
                    FindContainsKeyInvocation(b.LeftOperand, containsKeyMethod) ?? FindContainsKeyInvocation(b.RightOperand, containsKeyMethod),
                _ => null
            };
        }

        private static bool IsContainsKeyMethod(IMethodSymbol suspectedContainsKeyMethod, IMethodSymbol containsKeyMethod)
        {
            return suspectedContainsKeyMethod.OriginalDefinition.Equals(containsKeyMethod, SymbolEqualityComparer.Default)
                   || DoesSignatureMatch(suspectedContainsKeyMethod, containsKeyMethod);
        }

        private static bool DictionaryEntryIsModified(IPropertyReferenceOperation dictionaryAccess, IBlockOperation blockOperation)
        {
            return blockOperation.Operations.OfType<IExpressionStatementOperation>().Any(o =>
                o.Operation is IAssignmentOperation { Target: IPropertyReferenceOperation reference } && reference.Property.Equals(dictionaryAccess.Property, SymbolEqualityComparer.Default));
        }

        private static bool IsDictionaryAccess(IPropertyReferenceOperation propertyReference, INamedTypeSymbol dictionaryType, IPropertySymbol indexer)
        {
            return propertyReference.Property.IsIndexer
                   && IsDictionaryType(propertyReference.Property.ContainingType, dictionaryType)
                   && (propertyReference.Property.OriginalDefinition.Equals(indexer, SymbolEqualityComparer.Default)
                       || DoesSignatureMatch(propertyReference.Property, indexer));
        }

        private static bool TryGetParentConditionalOperation(IOperation derivedOperation, [NotNullWhen(true)] out IConditionalOperation? conditionalOperation)
        {
            conditionalOperation = null;
            do
            {
                if (derivedOperation.Parent is IConditionalOperation conditional)
                {
                    conditionalOperation = conditional;

                    return true;
                }

                derivedOperation = derivedOperation.Parent;
            } while (derivedOperation.Parent != null);

            return false;
        }

        private static bool IsDictionaryType(INamedTypeSymbol suspectedDictionaryType, ISymbol iDictionaryType)
        {
            // Either the type is the IDictionary it is a type which (indirectly) implements it.
            return suspectedDictionaryType.OriginalDefinition.Equals(iDictionaryType, SymbolEqualityComparer.Default)
                   || suspectedDictionaryType.AllInterfaces.Any((@interface, dictionary) => @interface.OriginalDefinition.Equals(dictionary, SymbolEqualityComparer.Default), iDictionaryType);
        }

        // Unfortunately we can't do symbol comparison, since this won't work for i.e. a method in a ConcurrentDictionary comparing against the same method in the IDictionary.
        private static bool DoesSignatureMatch(IMethodSymbol suspected, IMethodSymbol comparator)
        {
            return suspected.OriginalDefinition.ReturnType.Name == comparator.ReturnType.Name
                   && suspected.Name == comparator.Name
                   && suspected.Parameters.Length == comparator.Parameters.Length
                   && suspected.Parameters.Zip(comparator.Parameters, (p1, p2) => p1.OriginalDefinition.Type.Name == p2.Type.Name).All(isParameterEqual => isParameterEqual);
        }

        private static bool DoesSignatureMatch(IPropertySymbol suspected, IPropertySymbol comparator)
        {
            return suspected.OriginalDefinition.Type.Name == comparator.Type.Name
                   && suspected.Name == comparator.Name
                   && suspected.Parameters.Length == comparator.Parameters.Length
                   && suspected.Parameters.Zip(comparator.Parameters, (p1, p2) => p1.OriginalDefinition.Type.Name == p2.Type.Name).All(isParameterEqual => isParameterEqual);
        }
    }
}