// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotSerializeTypeWithPointerFields : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "CA5367";
        private static readonly LocalizableString s_Title = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotSerializeTypesWithPointerFields),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_Message = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotSerializeTypesWithPointerFieldsMessage),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_Description = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotSerializeTypesWithPointerFieldsDescription),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
                DiagnosticId,
                s_Title,
                s_Message,
                DiagnosticCategory.Security,
                RuleLevel.Disabled,
                description: s_Description,
                isPortedFxCopRule: false,
                isDataflowRule: false,
                isReportedAtCompilationEnd: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(
                (CompilationStartAnalysisContext compilationStartAnalysisContext) =>
                {
                    var compilation = compilationStartAnalysisContext.Compilation;
                    var serializableAttributeTypeSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSerializableAttribute);

                    if (serializableAttributeTypeSymbol == null)
                    {
                        return;
                    }

                    var nonSerializedAttribute = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemNonSerializedAttribute);
                    ConcurrentDictionary<ITypeSymbol, bool> visitedType = new ConcurrentDictionary<ITypeSymbol, bool>();
                    ConcurrentDictionary<IFieldSymbol, bool> pointerFields = new ConcurrentDictionary<IFieldSymbol, bool>();

                    compilationStartAnalysisContext.RegisterSymbolAction(
                        (SymbolAnalysisContext symbolAnalysisContext) =>
                        {
                            LookForSerializationWithPointerFields((ITypeSymbol)symbolAnalysisContext.Symbol, null);
                        }, SymbolKind.NamedType);

                    compilationStartAnalysisContext.RegisterCompilationEndAction(
                        (CompilationAnalysisContext compilationAnalysisContext) =>
                        {
                            foreach (var pointerField in pointerFields.Keys)
                            {
                                var associatedSymbol = pointerField.AssociatedSymbol;
                                compilationAnalysisContext.ReportDiagnostic(
                                    pointerField.CreateDiagnostic(
                                        Rule,
                                        associatedSymbol == null ? pointerField.Name : associatedSymbol.Name));
                            }
                        });

                    // Look for serialization of a type with valid pointer fields directly and indirectly.
                    //
                    // typeSymbol: The symbol of the type to be analyzed
                    // relatedFieldSymbol: When relatedFieldSymbol is null, traverse all descendants of typeSymbol to
                    //     find pointer fields; otherwise, traverse to find if relatedFieldSymbol is a pointer field
                    void LookForSerializationWithPointerFields(ITypeSymbol typeSymbol, IFieldSymbol? relatedFieldSymbol)
                    {
                        if (typeSymbol is IPointerTypeSymbol pointerTypeSymbol)
                        {
                            // If the field is a valid pointer.
                            if (pointerTypeSymbol.PointedAtType.TypeKind is TypeKind.Struct or
                                TypeKind.Pointer)
                            {
                                RoslynDebug.Assert(relatedFieldSymbol != null);
                                pointerFields.TryAdd(relatedFieldSymbol, true);
                            }
                        }
                        else if (typeSymbol is INamedTypeSymbol namedTypeSymbol)
                        {
                            // If it is defined in source and not visited,
                            // mark it as visited and analyze all fields of it.
                            if (namedTypeSymbol.IsInSource() &&
                                namedTypeSymbol.HasAttribute(serializableAttributeTypeSymbol) &&
                                visitedType.TryAdd(namedTypeSymbol, true))
                            {
                                // Get all the fields can be serialized.
                                var fields = namedTypeSymbol.GetMembers().OfType<IFieldSymbol>().Where(s => (nonSerializedAttribute == null ||
                                                                                                            !s.HasAttribute(nonSerializedAttribute)) &&
                                                                                                            !s.IsStatic);

                                foreach (var field in fields)
                                {
                                    LookForSerializationWithPointerFields(field.Type, field);
                                }
                            }

                            // If it is a generic type, analyze all type arguments of it.
                            if (namedTypeSymbol.IsGenericType)
                            {
                                foreach (var arg in namedTypeSymbol.TypeArguments)
                                {
                                    LookForSerializationWithPointerFields(arg, relatedFieldSymbol);
                                }
                            }
                        }
                        else if (typeSymbol is IArrayTypeSymbol arrayTypeSymbol)
                        {
                            LookForSerializationWithPointerFields(arrayTypeSymbol.ElementType, relatedFieldSymbol);
                        }
                    }
                });
        }
    }
}
