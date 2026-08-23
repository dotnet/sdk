// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using Resx = Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    //  Visual Basic doesn't allow ref struct APIs
#pragma warning disable RS1004 // Recommend adding language support to diagnostic analyzer
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1004 // Recommend adding language support to diagnostic analyzer
    public sealed class PreferReadOnlySpanPropertiesOverReadOnlyArrayFields : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1878";
        internal const string FixerDataPropertyName = nameof(FixerDataPropertyName);

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            Resx.CreateLocalizableResourceString(nameof(Resx.PreferReadOnlySpanPropertiesOverReadOnlyArrayFieldsTitle)),
            Resx.CreateLocalizableResourceString(nameof(Resx.PreferReadOnlySpanPropertiesOverReadOnlyArrayFieldsMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            Resx.CreateLocalizableResourceString(nameof(Resx.PreferReadOnlySpanPropertiesOverReadOnlyArrayFieldsDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false,
            isReportedAtCompilationEnd: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            //  Bail if we're missing required symbols.
            if (!RequiredSymbols.TryGetRequiredSymbols(context.Compilation, out RequiredSymbols? symbols))
                return;

            context.RegisterSymbolStartAction(OnSymbolStart, SymbolKind.NamedType);

            return;

            //  Local functions.

            void OnSymbolStart(SymbolStartAnalysisContext context)
            {
                var cache = new Cache();
                var fieldReferenceVisitor = new FieldReferenceVisitor(symbols, cache);

                context.RegisterOperationAction(AnalyzeOperation, OperationKind.FieldInitializer, OperationKind.FieldReference);
                context.RegisterSymbolEndAction(OnSymbolEnd);

                return;

                //  Local functions

                //  We analyze two types of operations: IFieldReferenceOperations and IFieldInitializerOperations.
                //  We maintain collections of candidate fields with valid field initializers.
                //  We analyze IFieldReferenceOperations and eliminate candidates that are used in ways that prohibit
                //  conversion to ReadOnlySpan.
                void AnalyzeOperation(OperationAnalysisContext context)
                {
                    switch (context.Operation)
                    {
                        case IFieldInitializerOperation fieldInitializer:
                            if (fieldInitializer.Value is IArrayCreationOperation arrayCreation &&
                                arrayCreation.Initializer is not null &&
                                arrayCreation.Initializer.ElementValues.All(x => x.ConstantValue.HasValue) &&
                                symbols.IsSupportedArrayElementType(arrayCreation.GetElementType()!))
                            {
                                foreach (var field in fieldInitializer.InitializedFields)
                                {
                                    if (field.IsStatic && field.IsReadOnly && field.IsPrivate() && symbols.CanMoveAttributesToProperty(field))
                                        cache.Candidates.Add(field);
                                }
                            }

                            break;
                        case IFieldReferenceOperation fieldReference:
                            if (fieldReference.IsWithinExpressionTree(symbols.LinqExpressionTreeType))
                            {
                                //  Eliminate candidates referenced within an expression tree, where a
                                //  ReadOnlySpan<T> property cannot be used (CS8640).
                                cache.Candidates.Eliminate(fieldReference.Field);
                            }
                            else if (fieldReference.GetValueUsageInfo(fieldReference.SemanticModel!.GetEnclosingSymbol(fieldReference.Syntax.SpanStart, context.CancellationToken)!) is
                                ValueUsageInfo.ReadableWritableReference or ValueUsageInfo.WritableReference)
                            {
                                //  Eliminate candidates that are assigned to ref or out variables.
                                cache.Candidates.Eliminate(fieldReference.Field);
                            }
                            else
                            {
                                //  Eliminate candidates that are used in ways that prohibit conversion to ReadOnlySpan.
                                fieldReference.Parent!.Accept(fieldReferenceVisitor, new VisitContext(fieldReference, fieldReference.Field, context.CancellationToken));
                            }

                            break;
                    }
                }

                //  Report diagnostics for all fields that survived candidate elimination and have a valid field initializer.
                void OnSymbolEnd(SymbolAnalysisContext context)
                {
                    try
                    {
                        var asSpanInvocationLookup = cache.SavedOperations.ToLookup(t => t.Field, t => t.Operation, SymbolEqualityComparer.Default);
                        foreach (var field in cache.Candidates)
                        {
                            //  Save the locations of all operations that need to be fixed by the fixer.
                            var savedLocations = asSpanInvocationLookup[field].Select(x => new SavedSpanLocation(x.Syntax.Span, x.Syntax.SyntaxTree.FilePath));
                            string propertyValue = SavedSpanLocation.Serialize(savedLocations);
                            var properties = ImmutableDictionary<string, string?>.Empty.Add(FixerDataPropertyName, propertyValue);
                            var messageArgument = ((IArrayTypeSymbol)field.Type).ElementType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                            var diagnostic = field.CreateDiagnostic(Rule, properties, messageArgument);
                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                    finally
                    {
                        cache.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Visits the parents of <see cref="IArrayElementReferenceOperation"/>s and eliminates candidates
        /// who's elements are used in ways that prohibit conversion to <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        private sealed class ArrayElementReferenceVisitor : OperationVisitor<VisitContext, Unit>
        {
            private readonly Cache _cache;

            public ArrayElementReferenceVisitor(Cache cache)
            {
                _cache = cache;
            }

            public override Unit VisitSimpleAssignment(ISimpleAssignmentOperation operation, VisitContext argument)
            {
                if (operation.Target.Equals(argument.Operation))
                    _cache.Candidates.Eliminate(argument.Field);
                return base.VisitSimpleAssignment(operation, argument);
            }

            public override Unit VisitCompoundAssignment(ICompoundAssignmentOperation operation, VisitContext argument)
            {
                if (operation.Target.Equals(argument.Operation))
                    _cache.Candidates.Eliminate(argument.Field);
                return base.VisitCompoundAssignment(operation, argument);
            }

            public override Unit VisitTuple(ITupleOperation operation, VisitContext argument)
            {
                if (operation.Parent is IDeconstructionAssignmentOperation deconstruction && deconstruction.Target.Equals(operation))
                    _cache.Candidates.Eliminate(argument.Field);
                return base.VisitTuple(operation, argument);
            }

            public override Unit VisitIncrementOrDecrement(IIncrementOrDecrementOperation operation, VisitContext argument)
            {
                _cache.Candidates.Eliminate(argument.Field);
                return base.VisitIncrementOrDecrement(operation, argument);
            }
        }

        /// <summary>
        /// Visits the parents of <see cref="IFieldReferenceOperation"/>s and eliminates candidates that
        /// are used in ways that prohibit conversion to <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        private sealed class FieldReferenceVisitor : OperationVisitor<VisitContext, Unit>
        {
            private readonly RequiredSymbols _symbols;
            private readonly Cache _cache;
            private readonly ArrayElementReferenceVisitor _arrayElementReferenceVisitor;

            public FieldReferenceVisitor(RequiredSymbols symbols, Cache cache)
            {
                _symbols = symbols;
                _cache = cache;
                _arrayElementReferenceVisitor = new(cache);
            }

            public override Unit VisitArrayElementReference(IArrayElementReferenceOperation operation, VisitContext argument)
            {
                if (operation.GetValueUsageInfo(operation.SemanticModel!.GetEnclosingSymbol(operation.Syntax.SpanStart, argument.CancellationToken)!) is
                    ValueUsageInfo.ReadableWritableReference or ValueUsageInfo.WritableReference)
                {
                    //  Eliminate candidates who's elements are assigned to ref or out variables.
                    _cache.Candidates.Eliminate(argument.Field);
                }
                else
                {
                    //  Eliminate candidates who's elements are used in ways that prohibit conversion to ReadOnlySpan.
                    operation.Parent!.Accept(_arrayElementReferenceVisitor, argument.With(operation));
                }

                return base.VisitArrayElementReference(operation, argument);
            }

            public override Unit VisitInvocation(IInvocationOperation operation, VisitContext argument)
            {
                _cache.Candidates.Eliminate(argument.Field);
                return base.VisitInvocation(operation, argument);
            }

            public override Unit VisitPropertyReference(IPropertyReferenceOperation operation, VisitContext argument)
            {
                if (!operation.Property.Equals(_symbols.ArrayLengthProperty, SymbolEqualityComparer.Default))
                    _cache.Candidates.Eliminate(argument.Field);
                return base.VisitPropertyReference(operation, argument);
            }

            public override Unit VisitArgument(IArgumentOperation operation, VisitContext argument)
            {
                var targetMethod = GetTargetMethod(operation.Parent);
                if (_symbols.IsAsSpanMethod(targetMethod) &&
                    operation.Parent?.Parent is IConversionOperation conversion &&
                    conversion.Type is not null &&
                    conversion.Type.OriginalDefinition.Equals(_symbols.ReadOnlySpanType, SymbolEqualityComparer.Default))
                {
                    //  Save the field reference itself rather than the wrapping argument so the fixer can
                    //  locate it again by span. For a named argument (e.g. 'MemoryExtensions.AsSpan(array: a)')
                    //  the argument syntax is wider than the field reference and would otherwise resolve to
                    //  the wrong node in the fixer.
                    _cache.SavedOperations.Add((argument.Field, argument.Operation));
                }
                else
                {
                    _cache.Candidates.Eliminate(argument.Field);
                }

                return base.VisitArgument(operation, argument);
            }

            public override Unit VisitConversion(IConversionOperation operation, VisitContext argument)
            {
                if (operation.Type is null || !operation.Type.OriginalDefinition.Equals(_symbols.ReadOnlySpanType, SymbolEqualityComparer.Default))
                    _cache.Candidates.Eliminate(argument.Field);
                return base.VisitConversion(operation, argument);
            }

            public override Unit VisitSimpleAssignment(ISimpleAssignmentOperation operation, VisitContext argument)
            {
                _cache.Candidates.Eliminate(argument.Field);
                return base.VisitSimpleAssignment(operation, argument);
            }

            public override Unit VisitCoalesceAssignment(ICoalesceAssignmentOperation operation, VisitContext argument)
            {
                _cache.Candidates.Eliminate(argument.Field);
                return base.VisitCoalesceAssignment(operation, argument);
            }

            public override Unit VisitVariableInitializer(IVariableInitializerOperation operation, VisitContext argument)
            {
                _cache.Candidates.Eliminate(argument.Field);
                return base.VisitVariableInitializer(operation, argument);
            }

            public override Unit VisitTuple(ITupleOperation operation, VisitContext argument)
            {
                _cache.Candidates.Eliminate(argument.Field);
                return base.VisitTuple(operation, argument);
            }

            public override Unit VisitReturn(IReturnOperation operation, VisitContext argument)
            {
                _cache.Candidates.Eliminate(argument.Field);
                return base.VisitReturn(operation, argument);
            }

            public override Unit VisitArrayInitializer(IArrayInitializerOperation operation, VisitContext argument)
            {
                _cache.Candidates.Eliminate(argument.Field);
                return base.VisitArrayInitializer(operation, argument);
            }

            private static IMethodSymbol? GetTargetMethod(IOperation? invocationOrObjectCreation)
            {
                IMethodSymbol? result = invocationOrObjectCreation switch
                {
                    IInvocationOperation invocation => invocation.TargetMethod,
                    IObjectCreationOperation objectCreation => objectCreation.Constructor,
                    _ => null
                };
                return result;
            }
        }

        private sealed class RequiredSymbols
        {
            private RequiredSymbols(Compilation compilation, INamedTypeSymbol readOnlySpanType, IPropertySymbol arrayLengthProperty)
            {
                ReadOnlySpanType = readOnlySpanType;
                ArrayLengthProperty = arrayLengthProperty;
                SupportedArrayElementTypes = GetSupportedArrayElementTypes(compilation);
                AsSpanMethods = GetAsSpanMethods(compilation, readOnlySpanType);
                LinqExpressionTreeType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqExpressionsExpression1);
                AttributeUsageAttributeType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemAttributeUsageAttribute);
                return;

                //  Local functions.

                static ImmutableHashSet<ITypeSymbol> GetSupportedArrayElementTypes(Compilation compilation)
                {
                    var builder = ImmutableHashSet.CreateBuilder<ITypeSymbol>(SymbolEqualityComparer.Default);

                    builder.AddIfNotNull(compilation.GetSpecialType(SpecialType.System_Boolean));
                    builder.AddIfNotNull(compilation.GetSpecialType(SpecialType.System_Byte));
                    builder.AddIfNotNull(compilation.GetSpecialType(SpecialType.System_SByte));

                    return builder.ToImmutable();
                }

                static ImmutableHashSet<IMethodSymbol> GetAsSpanMethods(Compilation compilation, ITypeSymbol readOnlySpanType)
                {
                    if (!compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemMemoryExtensions, out var memoryExtensionsType))
                        return ImmutableHashSet<IMethodSymbol>.Empty;

                    var spanType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSpan1);
                    var builder = ImmutableHashSet.CreateBuilder<IMethodSymbol>(SymbolEqualityComparer.Default);
                    var asSpanMethods = memoryExtensionsType.GetMembers(nameof(MemoryExtensions.AsSpan)).OfType<IMethodSymbol>()
                        .Where(x =>
                        {
                            return x.IsPublic() &&
                                (x.ReturnType.OriginalDefinition.Equals(readOnlySpanType, SymbolEqualityComparer.Default) ||
                                x.ReturnType.OriginalDefinition.Equals(spanType, SymbolEqualityComparer.Default));
                        });
                    builder.AddRange(asSpanMethods);
                    return builder.ToImmutable();
                }
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
            public IPropertySymbol ArrayLengthProperty { get; }
            public ImmutableHashSet<ITypeSymbol> SupportedArrayElementTypes { get; }
            public ImmutableHashSet<IMethodSymbol> AsSpanMethods { get; }
            public INamedTypeSymbol? LinqExpressionTreeType { get; }
            public INamedTypeSymbol? AttributeUsageAttributeType { get; }

            public bool IsSupportedArrayElementType(ITypeSymbol type) => SupportedArrayElementTypes.Contains(type);
            public bool IsAsSpanMethod(IMethodSymbol? method) => method is not null && AsSpanMethods.Contains(method.OriginalDefinition);

            /// <summary>
            /// Indicates whether every attribute applied to <paramref name="field"/> may also be applied to a
            /// property, which the fixer needs because it moves them onto the property it generates.
            /// </summary>
            public bool CanMoveAttributesToProperty(IFieldSymbol field)
            {
                foreach (var attribute in field.GetAttributes())
                {
                    if (attribute.AttributeClass is INamedTypeSymbol attributeClass && !IsValidOnProperty(attributeClass))
                    {
                        return false;
                    }
                }

                return true;
            }

            private bool IsValidOnProperty(INamedTypeSymbol attributeClass)
            {
                //  'AttributeUsageAttribute' is itself inherited, so a derived attribute type takes the
                //  targets declared by the nearest base type that declares them.
                for (INamedTypeSymbol? type = attributeClass; type is not null; type = type.BaseType)
                {
                    foreach (var usage in type.GetAttributes())
                    {
                        if (SymbolEqualityComparer.Default.Equals(usage.AttributeClass, AttributeUsageAttributeType) &&
                            usage.ConstructorArguments.Length == 1 &&
                            usage.ConstructorArguments[0].Value is int validOn)
                        {
                            return (validOn & (int)AttributeTargets.Property) != 0;
                        }
                    }
                }

                //  No '[AttributeUsage]' was found, so the attribute defaults to 'AttributeTargets.All'.
                return true;
            }
        }

        /// <summary>
        /// A collection of candidate <see cref="IFieldSymbol"/>s.
        /// Candidates can be removed from the collection using the <see cref="Eliminate(IFieldSymbol)"/> method
        /// before they have been added.
        /// If a candidate <see cref="IFieldSymbol"/> is added after having been eliminated, it is NOT a member of the collection.
        /// </summary>
        private sealed class CandidateCollection : IEnumerable<IFieldSymbol>, IDisposable
        {
            private readonly PooledConcurrentSet<IFieldSymbol> _potentialCandidates;
            private readonly PooledConcurrentSet<IFieldSymbol> _eliminatedCandidates;

            public CandidateCollection()
            {
                _potentialCandidates = PooledConcurrentSet<IFieldSymbol>.GetInstance();
                _eliminatedCandidates = PooledConcurrentSet<IFieldSymbol>.GetInstance();
            }

            /// <summary>
            /// Adds the specified potential candidate to the collection. If the candidate has previously been eliminated via a
            /// call to <see cref="Eliminate(IFieldSymbol)"/>, it will not be added.
            /// </summary>
            /// <param name="potentialCandidate">The candidate to add.</param>
            public void Add(IFieldSymbol potentialCandidate) => _potentialCandidates.Add(potentialCandidate);

            /// <summary>
            /// Eliminates the specified candidate from the collection. Once this method is called on a candidate,
            /// it cannot be added to the collection, even if <see cref="Add(IFieldSymbol)"/> is later called.
            /// </summary>
            /// <param name="candidate">The candidate to eliminate.</param>
            public void Eliminate(IFieldSymbol candidate) => _eliminatedCandidates.Add(candidate);

            /// <summary>
            /// Enumerates all potential candidates that have not been eliminated.
            /// </summary>
            /// <returns></returns>
            public IEnumerator<IFieldSymbol> GetEnumerator()
            {
                foreach (var field in _potentialCandidates)
                {
                    if (!_eliminatedCandidates.Contains(field))
                        yield return field;
                }
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

            public void Dispose()
            {
                _potentialCandidates.Dispose();
                _eliminatedCandidates.Dispose();
            }
        }

#pragma warning disable CA1815 // Override equals and operator equals on value types
        private readonly struct Cache : IDisposable
#pragma warning restore CA1815 // Override equals and operator equals on value types
        {
            public Cache()
            {
                Candidates = new CandidateCollection();
                SavedOperations = PooledConcurrentSet<(IFieldSymbol, IOperation)>.GetInstance();
            }

            public CandidateCollection Candidates { get; }
            public PooledConcurrentSet<(IFieldSymbol Field, IOperation Operation)> SavedOperations { get; }

            public void Dispose()
            {
                Candidates?.Dispose();
                SavedOperations?.Dispose();
            }
        }

        //  Not compared for equality.
#pragma warning disable CA1815 // Override equals and operator equals on value types
        private readonly struct VisitContext
#pragma warning restore CA1815 // Override equals and operator equals on value types
        {
            public VisitContext(IOperation operation, IFieldSymbol field, CancellationToken cancellationToken)
            {
                Operation = operation;
                Field = field;
                CancellationToken = cancellationToken;
            }

            public IOperation Operation { get; }
            public IFieldSymbol Field { get; }
            public CancellationToken CancellationToken { get; }
            public VisitContext With(IOperation operation) => new(operation, Field, CancellationToken);
        }

        /// <summary>
        /// Represents a saved span in source code that that needs to be fixed by the fixer.
        /// </summary>
        internal readonly struct SavedSpanLocation : IEquatable<SavedSpanLocation>
        {
            private const int SpanStartCaptureGroup = 1;
            private const int SpanLengthCaptureGroup = 2;
            private const int SourceFilePathCaptureGroup = 3;
            private const string FieldSeparator = "|";
            private const string SavedSpanSeparator = "\n";
            private static readonly Regex s_parseRegex = new($"([0-9]+){Regex.Escape(FieldSeparator)}([0-9]+){Regex.Escape(FieldSeparator)}(.*)$");

            public SavedSpanLocation(TextSpan span, string sourceFilePath)
            {
                Span = span;
                SourceFilePath = sourceFilePath;
            }

            public TextSpan Span { get; }
            public string SourceFilePath { get; }

            public override string ToString()
            {
                return $@"{Span.Start}{FieldSeparator}{Span.Length}{FieldSeparator}{SourceFilePath}";
            }

            public static SavedSpanLocation Parse(string text)
            {
                var match = s_parseRegex.Match(text);
                RoslynDebug.Assert(match.Success, "Invalid saved data format");

                return new SavedSpanLocation(
                    new TextSpan(
                        int.Parse(match.Groups[SpanStartCaptureGroup].Value, CultureInfo.InvariantCulture),
                        int.Parse(match.Groups[SpanLengthCaptureGroup].Value, CultureInfo.InvariantCulture)),
                    match.Groups[SourceFilePathCaptureGroup].Value);
            }

            public static string Serialize(IEnumerable<SavedSpanLocation> savedSpans)
            {
                return string.Join(SavedSpanSeparator, savedSpans.Select(x => x.ToString()));
            }

            public static ImmutableArray<SavedSpanLocation> Deserialize(string text)
            {
                var lines = text.Split(new[] { SavedSpanSeparator }, StringSplitOptions.RemoveEmptyEntries);
                var builder = ImmutableArray.CreateBuilder<SavedSpanLocation>(lines.Length);
                builder.Count = lines.Length;
                for (int i = 0; i < lines.Length; ++i)
                    builder[i] = Parse(lines[i]);

                return builder.MoveToImmutable();
            }

            public static bool Equals(SavedSpanLocation left, SavedSpanLocation right) => (left.Span, left.SourceFilePath) == (right.Span, right.SourceFilePath);
            public static bool operator ==(SavedSpanLocation left, SavedSpanLocation right) => Equals(left, right);
            public static bool operator !=(SavedSpanLocation left, SavedSpanLocation right) => !Equals(left, right);
            public bool Equals(SavedSpanLocation other) => Equals(this, other);
            public override bool Equals(object obj) => obj is SavedSpanLocation other && Equals(this, other);
            public override int GetHashCode() => (Span, SourceFilePath).GetHashCode();
        }
    }
}
