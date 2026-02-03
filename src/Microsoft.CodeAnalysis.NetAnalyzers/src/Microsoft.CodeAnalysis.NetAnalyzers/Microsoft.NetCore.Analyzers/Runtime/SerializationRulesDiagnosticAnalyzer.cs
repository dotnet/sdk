// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA2237: <inheritdoc cref="MarkISerializableTypesWithSerializableTitle"/>
    /// CA2235: <inheritdoc cref="MarkAllNonSerializableFieldsTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class SerializationRulesDiagnosticAnalyzer : DiagnosticAnalyzer
    {
        // Mark ISerializable types with SerializableAttribute
        internal const string RuleCA2237Id = "CA2237";

        internal static readonly DiagnosticDescriptor RuleCA2237 = DiagnosticDescriptorHelper.Create(
            RuleCA2237Id,
            CreateLocalizableResourceString(nameof(MarkISerializableTypesWithSerializableTitle)),
            CreateLocalizableResourceString(nameof(MarkISerializableTypesWithSerializableMessage)),
            DiagnosticCategory.Usage,
            RuleLevel.CandidateForRemoval,   // Cannot implement this for .NET Core: https://github.com/dotnet/roslyn-analyzers/issues/1775#issuecomment-518457308
            description: CreateLocalizableResourceString(nameof(MarkISerializableTypesWithSerializableDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        // Mark all non-serializable fields
        internal const string RuleCA2235Id = "CA2235";

        internal static readonly DiagnosticDescriptor RuleCA2235 = DiagnosticDescriptorHelper.Create(
            RuleCA2235Id,
            CreateLocalizableResourceString(nameof(MarkAllNonSerializableFieldsTitle)),
            CreateLocalizableResourceString(nameof(MarkAllNonSerializableFieldsMessage)),
            DiagnosticCategory.Usage,
            RuleLevel.CandidateForRemoval,   // Cannot implement this for .NET Core: https://github.com/dotnet/roslyn-analyzers/issues/1775#issuecomment-518457308
            description: CreateLocalizableResourceString(nameof(MarkAllNonSerializableFieldsDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(RuleCA2235, RuleCA2237);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(
                (context) =>
                {
                    INamedTypeSymbol? iserializableTypeSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeSerializationISerializable);
                    if (iserializableTypeSymbol == null)
                    {
                        return;
                    }

                    INamedTypeSymbol? serializableAttributeTypeSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSerializableAttribute);
                    if (serializableAttributeTypeSymbol == null)
                    {
                        return;
                    }

                    INamedTypeSymbol? nonSerializedAttributeTypeSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemNonSerializedAttribute);
                    if (nonSerializedAttributeTypeSymbol == null)
                    {
                        return;
                    }

                    var isNetStandardAssembly = !context.Compilation.TargetsDotNetFramework();

                    var symbolAnalyzer = new SymbolAnalyzer(iserializableTypeSymbol, serializableAttributeTypeSymbol, nonSerializedAttributeTypeSymbol, isNetStandardAssembly);
                    context.RegisterSymbolAction(symbolAnalyzer.AnalyzeSymbol, SymbolKind.NamedType);
                });
        }

        private sealed class SymbolAnalyzer
        {
            private readonly INamedTypeSymbol _iserializableTypeSymbol;
            private readonly INamedTypeSymbol _serializableAttributeTypeSymbol;
            private readonly INamedTypeSymbol _nonSerializedAttributeTypeSymbol;
            private readonly bool _isNetStandardAssembly;

            public SymbolAnalyzer(
                INamedTypeSymbol iserializableTypeSymbol,
                INamedTypeSymbol serializableAttributeTypeSymbol,
                INamedTypeSymbol nonSerializedAttributeTypeSymbol,
                bool isNetStandardAssembly)
            {
                _iserializableTypeSymbol = iserializableTypeSymbol;
                _serializableAttributeTypeSymbol = serializableAttributeTypeSymbol;
                _nonSerializedAttributeTypeSymbol = nonSerializedAttributeTypeSymbol;
                _isNetStandardAssembly = isNetStandardAssembly;
            }

            public void AnalyzeSymbol(SymbolAnalysisContext context)
            {
                var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;
                if (namedTypeSymbol.TypeKind is TypeKind.Delegate or TypeKind.Interface)
                {
                    return;
                }

                var implementsISerializable = namedTypeSymbol.Interfaces.Contains(_iserializableTypeSymbol);
                var isSerializable = IsSerializable(namedTypeSymbol);

                // If the type is public and implements ISerializable
                if (namedTypeSymbol.DeclaredAccessibility == Accessibility.Public && implementsISerializable)
                {
                    if (!isSerializable)
                    {
                        // CA2237 : Mark serializable types with the SerializableAttribute
                        if (namedTypeSymbol.BaseType is { } baseType &&
                            (baseType.SpecialType == SpecialType.System_Object ||
                             IsSerializable(baseType)))
                        {
                            context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(RuleCA2237, namedTypeSymbol.Name));
                        }
                    }
                }

                // If this is type is marked Serializable and doesn't implement ISerializable, check its fields' types as well
                if (isSerializable && !implementsISerializable)
                {
                    foreach (ISymbol member in namedTypeSymbol.GetMembers())
                    {
                        // Only process field members
                        if (member is not IFieldSymbol field)
                        {
                            continue;
                        }

                        // Only process instance fields
                        if (field.IsStatic)
                        {
                            continue;
                        }

                        // Only process non-serializable fields
                        if (IsSerializable(field.Type))
                        {
                            continue;
                        }

                        // We bail out from reporting CA2235 in netstandard assemblies for types in metadata
                        // due to missing support: https://github.com/dotnet/roslyn-analyzers/issues/1775#issuecomment-519686818
                        if (_isNetStandardAssembly && field.Type.Locations.All(l => !l.IsInSource))
                        {
                            continue;
                        }

                        // Check for [NonSerialized]
                        if (field.HasAnyAttribute(_nonSerializedAttributeTypeSymbol))
                        {
                            continue;
                        }

                        // Handle compiler-generated fields (without source declaration) that have an associated symbol in code.
                        // For example, auto-property backing fields.
                        ISymbol targetSymbol = field.IsImplicitlyDeclared && field.AssociatedSymbol != null
                            ? field.AssociatedSymbol
                            : field;

                        context.ReportDiagnostic(
                            targetSymbol.CreateDiagnostic(
                                RuleCA2235,
                                targetSymbol.Name,
                                namedTypeSymbol.Name,
                                field.Type));
                    }
                }
            }

            private bool IsSerializable(ITypeSymbol type)
            {
                if (type.IsPrimitiveType())
                {
                    return true;
                }

                return type.TypeKind switch
                {
                    TypeKind.Array => IsSerializable(((IArrayTypeSymbol)type).ElementType),
                    TypeKind.Enum => IsSerializable(((INamedTypeSymbol)type).EnumUnderlyingType!),
                    TypeKind.TypeParameter or TypeKind.Interface => true,// The concrete type can't be determined statically,
                                                                         // so we assume true to cut down on noise.
                    TypeKind.Class or TypeKind.Struct => ((INamedTypeSymbol)type).IsSerializable,// Check SerializableAttribute or Serializable flag from metadata.
                    TypeKind.Delegate => true,// delegates are always serializable, even if
                                              // they aren't actually marked [Serializable]
                    _ => type.HasAnyAttribute(_serializableAttributeTypeSymbol),
                };
            }
        }
    }
}
