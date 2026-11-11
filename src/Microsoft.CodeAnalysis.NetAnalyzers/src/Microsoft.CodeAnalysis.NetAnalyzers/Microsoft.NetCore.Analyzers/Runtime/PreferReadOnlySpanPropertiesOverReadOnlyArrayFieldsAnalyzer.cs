// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    public abstract class PreferReadOnlySpanPropertiesOverReadOnlyArrayFieldsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1878";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(PreferReadOnlySpanPropertiesOverReadOnlyArrayFieldsTitle)),
            CreateLocalizableResourceString(nameof(PreferReadOnlySpanPropertiesOverReadOnlyArrayFieldsMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(PreferReadOnlySpanPropertiesOverReadOnlyArrayFieldsDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false,
            isReportedAtCompilationEnd: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        protected abstract bool IsApplicableToLanguageVersion(ParseOptions options);

        public override void Initialize(AnalysisContext context)
        {
            // References in generated partial declarations must still disqualify fields declared in user code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            if (!context.Compilation.SyntaxTrees.All(tree => IsApplicableToLanguageVersion(tree.Options)) ||
                !RequiredSymbols.TryGetRequiredSymbols(context.Compilation, out RequiredSymbols? symbols))
            {
                return;
            }

            var cache = new Cache();
            var fieldReferenceVisitor = new FieldReferenceVisitor(symbols, cache);
            context.RegisterOperationAction(
                AnalyzeOperation,
                OperationKind.FieldInitializer,
                OperationKind.FieldReference);
            context.RegisterSymbolStartAction(OnSymbolStart, SymbolKind.NamedType);
            context.RegisterCompilationEndAction(_ => cache.Dispose());

            return;

            //  Local functions.

            void OnSymbolStart(SymbolStartAnalysisContext context)
            {
                if (!ShouldAnalyze((INamedTypeSymbol)context.Symbol))
                {
                    return;
                }

                context.RegisterSymbolEndAction(OnSymbolEnd);
            }

            bool ShouldAnalyze(INamedTypeSymbol namedType)
            {
                foreach (var member in namedType.GetMembers())
                {
                    if (member is IFieldSymbol field &&
                        IsPotentialField(field, symbols.SupportsMultiBytePrimitiveTypes))
                    {
                        return true;
                    }
                }

                return false;
            }

            //  We analyze two types of operations: IFieldReferenceOperations and IFieldInitializerOperations.
            //  We maintain collections of candidate fields with valid field initializers.
            //  We analyze IFieldReferenceOperations and eliminate candidates that are used in ways that prohibit
            //  conversion to ReadOnlySpan.
            void AnalyzeOperation(OperationAnalysisContext context)
            {
                switch (context.Operation)
                {
                    case IFieldInitializerOperation fieldInitializer:
                        foreach (var field in fieldInitializer.InitializedFields)
                        {
                            if (IsValidCandidate(field, fieldInitializer.Value, symbols))
                            {
                                cache.AddCandidate(field);
                            }
                        }

                        break;
                    case IFieldReferenceOperation fieldReference:
                        if (!IsPotentialField(fieldReference.Field, symbols.SupportsMultiBytePrimitiveTypes))
                        {
                            break;
                        }

                        if (IsWithinExpressionTree(fieldReference, symbols.LinqExpressionTreeType))
                        {
                            //  Eliminate candidates referenced within an expression tree, where a
                            //  ReadOnlySpan<T> property cannot be used (CS8640).
                            cache.EliminateCandidate(fieldReference.Field);
                        }
                        else if (fieldReference.GetValueUsageInfo(context.ContainingSymbol) is
                            ValueUsageInfo.ReadableWritableReference or ValueUsageInfo.WritableReference)
                        {
                            //  Eliminate candidates that are assigned to ref or out variables.
                            cache.EliminateCandidate(fieldReference.Field);
                        }
                        else
                        {
                            // Eliminate candidates that are used in ways that prohibit conversion to ReadOnlySpan.
                            if (fieldReference.Parent is IOperation parent)
                            {
                                                parent.Accept(fieldReferenceVisitor, new VisitContext(fieldReference, fieldReference.Field, context.ContainingSymbol));
                            }
                            else
                            {
                                cache.EliminateCandidate(fieldReference.Field);
                            }
                        }
                        break;
                }
            }

            //  Report diagnostics for all fields declared by this type that survived candidate elimination.
            void OnSymbolEnd(SymbolAnalysisContext context)
            {
                var namedType = (INamedTypeSymbol)context.Symbol;
                foreach (var field in namedType.GetMembers().OfType<IFieldSymbol>())
                {
                    if (!cache.IsCandidate(field))
                    {
                        continue;
                    }

                    var asSpanReferences = cache.GetAsSpanReferences(field);
                    var asSpanLocations = asSpanReferences is null
                        ? ImmutableArray<Location>.Empty
                        : asSpanReferences
                            .OrderBy(location => location.SourceTree?.FilePath, StringComparer.Ordinal)
                            .ThenBy(location => location.SourceSpan.Start)
                            .ToImmutableArray();
                    var messageArgument = ((IArrayTypeSymbol)field.Type).ElementType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                    var diagnostic = field.Locations[0].CreateDiagnostic(Rule, asSpanLocations, properties: null, messageArgument);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        internal static bool IsValidCandidate(
            IFieldSymbol field,
            IOperation initializerValue,
            INamedTypeSymbol? attributeUsageAttributeType,
            bool supportsMultiBytePrimitiveTypes)
        {
            if (!IsPotentialField(field, supportsMultiBytePrimitiveTypes) ||
                !CanMoveAttributesToProperty(field, attributeUsageAttributeType) ||
                field.Type is not IArrayTypeSymbol)
            {
                return false;
            }

            return initializerValue switch
            {
                IArrayCreationOperation { Initializer: { } initializer }
                    => initializer.ElementValues.All(element => element.ConstantValue.HasValue),
                IArrayInitializerOperation initializer
                    => initializer.ElementValues.All(element => element.ConstantValue.HasValue),
                _ => false,
            };
        }

        private static bool IsWithinExpressionTree(
            IOperation operation,
            INamedTypeSymbol? linqExpressionTreeType)
        {
            if (linqExpressionTreeType is null)
            {
                return false;
            }

            for (IOperation? ancestor = operation.Parent; ancestor is not null; ancestor = ancestor.Parent)
            {
                if (ancestor is IAnonymousFunctionOperation or ILocalFunctionOperation &&
                    ancestor.Parent?.Type?.OriginalDefinition is ITypeSymbol lambdaType &&
                    SymbolEqualityComparer.Default.Equals(linqExpressionTreeType, lambdaType))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsValidCandidate(IFieldSymbol field, IOperation initializerValue, RequiredSymbols symbols)
            => IsValidCandidate(
                field,
                initializerValue,
                symbols.AttributeUsageAttributeType,
                symbols.SupportsMultiBytePrimitiveTypes);

        private static bool IsSupportedArrayElementType(ITypeSymbol elementType, bool supportsMultiBytePrimitiveTypes)
            => elementType.IsPrimitiveType() &&
                elementType.SpecialType != SpecialType.System_String &&
                (supportsMultiBytePrimitiveTypes ||
                elementType.SpecialType is
                    SpecialType.System_Boolean or
                    SpecialType.System_Byte or
                    SpecialType.System_SByte);

        private static bool IsPotentialField(IFieldSymbol field, bool supportsMultiBytePrimitiveTypes)
            => field.IsStatic &&
                field.IsReadOnly &&
                field.IsPrivate() &&
                field.Type is IArrayTypeSymbol { Rank: 1 } arrayType &&
                IsSupportedArrayElementType(arrayType.ElementType, supportsMultiBytePrimitiveTypes);

        internal static bool SupportsMultiBytePrimitiveTypes(
            Compilation compilation,
            INamedTypeSymbol readOnlySpanType)
        {
            INamedTypeSymbol? runtimeHelpersType =
                compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeCompilerServicesRuntimeHelpers);

            return runtimeHelpersType?.GetMembers("CreateSpan").OfType<IMethodSymbol>().Any(
                method =>
                    method.IsStatic &&
                    method.IsGenericMethod &&
                    method.Arity == 1 &&
                    method.DeclaredAccessibility == Accessibility.Public &&
                    method.Parameters is [{ RefKind: RefKind.None, Type.SpecialType: SpecialType.System_RuntimeFieldHandle }] &&
                    method.ReturnType is INamedTypeSymbol
                    {
                        Arity: 1,
                        TypeArguments: [ITypeParameterSymbol returnTypeParameter],
                    } returnType &&
                    SymbolEqualityComparer.Default.Equals(returnType.OriginalDefinition, readOnlySpanType) &&
                    SymbolEqualityComparer.Default.Equals(returnTypeParameter, method.TypeParameters[0])) == true;
        }

        private static bool CanMoveAttributesToProperty(IFieldSymbol field, INamedTypeSymbol? attributeUsageAttributeType)
        {
            foreach (var attribute in field.GetAttributes())
            {
                if (attribute.AttributeClass is INamedTypeSymbol attributeClass &&
                    !IsValidOnProperty(attributeClass, attributeUsageAttributeType))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidOnProperty(INamedTypeSymbol attributeClass, INamedTypeSymbol? attributeUsageAttributeType)
        {
            // 'AttributeUsageAttribute' is itself inherited, so a derived attribute type takes the
            // targets declared by the nearest base type that declares them.
            for (INamedTypeSymbol? type = attributeClass; type is not null; type = type.BaseType)
            {
                foreach (var usage in type.GetAttributes())
                {
                    if (SymbolEqualityComparer.Default.Equals(usage.AttributeClass, attributeUsageAttributeType) &&
                        usage.ConstructorArguments.Length == 1 &&
                        usage.ConstructorArguments[0].Value is int validOn)
                    {
                        return (validOn & (int)AttributeTargets.Property) != 0;
                    }
                }
            }

            // No '[AttributeUsage]' was found, so the attribute defaults to 'AttributeTargets.All'.
            return true;
        }

        /// <summary>
        /// Visits the parents of <see cref="IFieldReferenceOperation"/>s and eliminates candidates that
        /// are used in ways that prohibit conversion to <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        private sealed class FieldReferenceVisitor : OperationVisitor<VisitContext, Unit>
        {
            private readonly RequiredSymbols _symbols;
            private readonly Cache _cache;

            public FieldReferenceVisitor(RequiredSymbols symbols, Cache cache)
            {
                _symbols = symbols;
                _cache = cache;
            }

            public override Unit VisitArrayElementReference(IArrayElementReferenceOperation operation, VisitContext argument)
            {
                if (operation.GetValueUsageInfo(argument.ContainingSymbol).IsWrittenTo())
                {
                    _cache.EliminateCandidate(argument.Field);
                }
                else if (operation.Type is IArrayTypeSymbol)
                {
                    // A range index returns an array, so validate its parent as a whole-array use.
                    if (operation.Parent is IOperation parent)
                    {
                        parent.Accept(this, argument);
                    }
                    else
                    {
                        _cache.EliminateCandidate(argument.Field);
                    }
                }

                return default;
            }

            public override Unit VisitPropertyReference(IPropertyReferenceOperation operation, VisitContext argument)
            {
                if (!operation.Property.Equals(_symbols.ArrayLengthProperty, SymbolEqualityComparer.Default))
                {
                    _cache.EliminateCandidate(argument.Field);
                }

                return default;
            }

            public override Unit VisitArgument(IArgumentOperation operation, VisitContext argument)
            {
                if (operation.Parent is IInvocationOperation invocation &&
                    _symbols.IsAsSpanMethod(invocation.TargetMethod) &&
                    invocation.Parent is IConversionOperation conversion &&
                    conversion.Type is not null &&
                    conversion.Type.OriginalDefinition.Equals(_symbols.ReadOnlySpanType, SymbolEqualityComparer.Default))
                {
                    //  Save the field reference itself rather than the wrapping argument so the fixer can
                    //  locate it again by span. For a named argument (e.g. 'MemoryExtensions.AsSpan(array: a)')
                    //  the argument syntax is wider than the field reference and would otherwise resolve to
                    //  the wrong node in the fixer.
                    _cache.AddAsSpanReference(argument.Field, argument.FieldReference.Syntax.GetLocation());
                }
                else
                {
                    _cache.EliminateCandidate(argument.Field);
                }

                return default;
            }

            public override Unit VisitConversion(IConversionOperation operation, VisitContext argument)
            {
                if (operation.Parent is IForEachLoopOperation { Collection: var collection } &&
                    ReferenceEquals(operation, collection) &&
                    operation.IsImplicit &&
                    CanUseReadOnlySpanInForEach(argument))
                {
                    return default;
                }

                if (operation.Type is null ||
                    !operation.Type.OriginalDefinition.Equals(_symbols.ReadOnlySpanType, SymbolEqualityComparer.Default))
                {
                    _cache.EliminateCandidate(argument.Field);
                }

                return default;
            }

            public override Unit VisitNameOf(INameOfOperation operation, VisitContext argument)
            {
                return default;
            }

            public override Unit VisitForEachLoop(IForEachLoopOperation operation, VisitContext argument)
            {
                if (!ReferenceEquals(operation.Collection, argument.FieldReference) ||
                    !CanUseReadOnlySpanInForEach(argument))
                {
                    _cache.EliminateCandidate(argument.Field);
                }

                return default;
            }

            public override Unit DefaultVisit(IOperation operation, VisitContext argument)
            {
                // A whole array can only become a ReadOnlySpan<T> in the explicitly handled contexts above.
                // Conservatively reject new operation shapes so the fixer cannot produce uncompilable code.
                _cache.EliminateCandidate(argument.Field);
                return default;
            }

            private bool CanUseReadOnlySpanInForEach(VisitContext argument)
            {
                if (argument.ContainingSymbol is not IMethodSymbol { IsAsync: false } ||
                    argument.FieldReference.IsWithinLambdaOrLocalFunction(out _))
                {
                    return false;
                }

                IOperation root = argument.FieldReference;
                while (root.Parent is IOperation parent)
                {
                    root = parent;
                }

                return !root.HasAnyOperationDescendant(
                    operation =>
                        operation.Kind is OperationKind.YieldBreak or OperationKind.YieldReturn &&
                        !operation.IsWithinLambdaOrLocalFunction(out _));
            }
        }

        private sealed class RequiredSymbols
        {
            private RequiredSymbols(Compilation compilation, INamedTypeSymbol readOnlySpanType, IPropertySymbol arrayLengthProperty)
            {
                ReadOnlySpanType = readOnlySpanType;
                SupportsMultiBytePrimitiveTypes =
                    PreferReadOnlySpanPropertiesOverReadOnlyArrayFieldsAnalyzer.SupportsMultiBytePrimitiveTypes(
                        compilation,
                        readOnlySpanType);
                ArrayLengthProperty = arrayLengthProperty;
                MemoryExtensionsType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemMemoryExtensions);
                SpanType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSpan1);
                LinqExpressionTreeType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqExpressionsExpression1);
                AttributeUsageAttributeType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemAttributeUsageAttribute);
            }
            public static bool TryGetRequiredSymbols(Compilation compilation, [NotNullWhen(true)] out RequiredSymbols? requiredSymbols)
            {
                var arrayLengthProperty = compilation.GetSpecialType(SpecialType.System_Array).GetMembers(nameof(Array.Length)).OfType<IPropertySymbol>().FirstOrDefault();
                if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReadOnlySpan1, out var rosType) &&
                    arrayLengthProperty is not null)
                {
                    requiredSymbols = new(compilation, rosType, arrayLengthProperty);
                    return true;
                }

                requiredSymbols = null;
                return false;
            }

            public INamedTypeSymbol ReadOnlySpanType { get; }
            public bool SupportsMultiBytePrimitiveTypes { get; }
            public IPropertySymbol ArrayLengthProperty { get; }
            public INamedTypeSymbol? MemoryExtensionsType { get; }
            public INamedTypeSymbol? SpanType { get; }
            public INamedTypeSymbol? LinqExpressionTreeType { get; }
            public INamedTypeSymbol? AttributeUsageAttributeType { get; }

            public bool IsAsSpanMethod(IMethodSymbol? method)
                => method is not null &&
                    method.Name == nameof(MemoryExtensions.AsSpan) &&
                    SymbolEqualityComparer.Default.Equals(method.OriginalDefinition.ContainingType, MemoryExtensionsType) &&
                    (SymbolEqualityComparer.Default.Equals(method.ReturnType.OriginalDefinition, ReadOnlySpanType) ||
                    SymbolEqualityComparer.Default.Equals(method.ReturnType.OriginalDefinition, SpanType));
        }

        private sealed class Cache : IDisposable
        {
            public Cache()
            {
                _candidateStates = PooledConcurrentDictionary<IFieldSymbol, bool>.GetInstance(SymbolEqualityComparer.Default);
                _asSpanReferences = PooledConcurrentDictionary<IFieldSymbol, ConcurrentDictionary<(SyntaxTree? SourceTree, TextSpan SourceSpan), Location>>.GetInstance(SymbolEqualityComparer.Default);
            }

            private readonly PooledConcurrentDictionary<IFieldSymbol, bool> _candidateStates;
            private readonly PooledConcurrentDictionary<IFieldSymbol, ConcurrentDictionary<(SyntaxTree? SourceTree, TextSpan SourceSpan), Location>> _asSpanReferences;

            public void AddCandidate(IFieldSymbol field)
                => _candidateStates.TryAdd(field.OriginalDefinition, value: true);

            public void EliminateCandidate(IFieldSymbol field)
                => _candidateStates.AddOrUpdate(field.OriginalDefinition, addValue: false, static (_, _) => false);

            public bool IsCandidate(IFieldSymbol field)
                => _candidateStates.TryGetValue(field.OriginalDefinition, out bool isCandidate) && isCandidate;

            public void AddAsSpanReference(IFieldSymbol field, Location location)
                => _asSpanReferences
                    .GetOrAdd(field.OriginalDefinition, static _ => new ConcurrentDictionary<(SyntaxTree? SourceTree, TextSpan SourceSpan), Location>())
                    .TryAdd((location.SourceTree, location.SourceSpan), location);

            public IEnumerable<Location>? GetAsSpanReferences(IFieldSymbol field)
                => _asSpanReferences.TryGetValue(field.OriginalDefinition, out var references) ? references.Values : null;

            public void Dispose()
            {
                _candidateStates.Dispose();
                _asSpanReferences.Dispose();
            }
        }

        //  Not compared for equality.
#pragma warning disable CA1815 // Override equals and operator equals on value types
        private readonly struct VisitContext
#pragma warning restore CA1815 // Override equals and operator equals on value types
        {
            public VisitContext(IFieldReferenceOperation fieldReference, IFieldSymbol field, ISymbol containingSymbol)
            {
                FieldReference = fieldReference;
                Field = field;
                ContainingSymbol = containingSymbol;
            }

            public IFieldReferenceOperation FieldReference { get; }
            public IFieldSymbol Field { get; }
            public ISymbol ContainingSymbol { get; }
        }
    }
}
