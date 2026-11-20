// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.Performance
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1882: <inheritdoc cref="AvoidNonEquatableValueTypesInRecordsTitle"/>
    /// </summary>
    /// <remarks>
    /// The equality members the compiler generates for a record compare every instance field through
    /// <see cref="System.Collections.Generic.EqualityComparer{T}.Default"/>. For a value type that does not
    /// implement <see cref="System.IEquatable{T}"/> of itself, that comparer calls <see cref="object.Equals(object)"/>,
    /// which boxes the value on every comparison.
    /// </remarks>
#pragma warning disable RS1004 // Recommend adding language support to diagnostic analyzer - Records only exist in C#
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1004 // Recommend adding language support to diagnostic analyzer
    public sealed class AvoidNonEquatableValueTypesInRecordsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1882";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(AvoidNonEquatableValueTypesInRecordsTitle)),
            CreateLocalizableResourceString(nameof(AvoidNonEquatableValueTypesInRecordsMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(AvoidNonEquatableValueTypesInRecordsDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            var typeProvider = WellKnownTypeProvider.GetOrCreate(context.Compilation);

            if (!typeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIEquatable1, out var equatableType))
            {
                return;
            }

            context.RegisterSymbolAction(symbolContext => AnalyzeNamedType(symbolContext, equatableType), SymbolKind.NamedType);
        }

        private static void AnalyzeNamedType(SymbolAnalysisContext context, INamedTypeSymbol equatableType)
        {
            var recordType = (INamedTypeSymbol)context.Symbol;

            if (!recordType.IsRecord)
            {
                return;
            }

            ImmutableArray<ISymbol> members = recordType.GetMembers();

            // The compiler only generates member-wise equality when the record does not declare 'Equals(TRecord)' itself.
            if (DeclaresTypedEquals(recordType, members))
            {
                return;
            }

            // Generated equality compares the instance fields declared in the record, which includes the backing
            // fields of auto-properties and of the properties synthesized for positional parameters. Fields of a
            // base record are compared by the base record and reported there.
            foreach (ISymbol member in members)
            {
                if (member is not IFieldSymbol { IsStatic: false } field)
                {
                    continue;
                }

                ITypeSymbol fieldType = field.Type;

                // 'EqualityComparer<T?>.Default' compares the underlying values with 'EqualityComparer<T>.Default'.
                if (fieldType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                {
                    fieldType = ((INamedTypeSymbol)fieldType).TypeArguments[0];
                }

                if (fieldType is not INamedTypeSymbol { TypeKind: TypeKind.Struct } valueType ||
                    ImplementsEquatableOfSelf(valueType, equatableType))
                {
                    continue;
                }

                // Report on the property or positional parameter the user wrote rather than on its backing field.
                ISymbol reportedSymbol = field.AssociatedSymbol ?? field;

                context.ReportDiagnostic(reportedSymbol.CreateDiagnostic(
                    Rule,
                    reportedSymbol.Name,
                    valueType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    recordType.Name));
            }
        }

        private static bool DeclaresTypedEquals(INamedTypeSymbol recordType, ImmutableArray<ISymbol> members)
        {
            foreach (ISymbol member in members)
            {
                if (member is IMethodSymbol
                    {
                        Name: WellKnownMemberNames.ObjectEquals,
                        MethodKind: MethodKind.Ordinary,
                        IsStatic: false,
                        IsImplicitlyDeclared: false,
                        Parameters.Length: 1
                    } method &&
                    SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, recordType))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ImplementsEquatableOfSelf(INamedTypeSymbol valueType, INamedTypeSymbol equatableType)
        {
            // A tuple type with element names implements 'IEquatable<T>' of the 'ValueTuple' type without those names.
            INamedTypeSymbol equatableArgument = valueType.TupleUnderlyingType ?? valueType;

            foreach (INamedTypeSymbol @interface in valueType.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(@interface.OriginalDefinition, equatableType) &&
                    SymbolEqualityComparer.Default.Equals(@interface.TypeArguments[0], equatableArgument))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
