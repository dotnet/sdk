// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class SerializationRulesDiagnosticAnalyzer : DiagnosticAnalyzer
    {
        // Implement serialization constructors
        internal const string RuleCA2229Id = "CA2229";

        private static readonly LocalizableString s_localizableTitleCA2229 =
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.ImplementSerializationConstructorsTitle),
                MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        private static readonly LocalizableString s_localizableDescriptionCA2229 =
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.ImplementSerializationConstructorsDescription),
                MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor RuleCA2229Default = DiagnosticDescriptorHelper.Create(RuleCA2229Id,
                                                                        s_localizableTitleCA2229,
                                                                        MicrosoftNetCoreAnalyzersResources.ImplementSerializationConstructorsMessageCreateMagicConstructor,
                                                                        DiagnosticCategory.Usage,
                                                                        RuleLevel.IdeHidden_BulkConfigurable,
                                                                        description: s_localizableDescriptionCA2229,
                                                                        isPortedFxCopRule: true,
                                                                        isDataflowRule: false);

        internal static DiagnosticDescriptor RuleCA2229Sealed = DiagnosticDescriptorHelper.Create(RuleCA2229Id,
                                                                            s_localizableTitleCA2229,
                                                                            MicrosoftNetCoreAnalyzersResources.ImplementSerializationConstructorsMessageMakeSealedMagicConstructorPrivate,
                                                                            DiagnosticCategory.Usage,
                                                                            RuleLevel.IdeHidden_BulkConfigurable,
                                                                            description: s_localizableDescriptionCA2229,
                                                                            isPortedFxCopRule: true,
                                                                        isDataflowRule: false);

        internal static DiagnosticDescriptor RuleCA2229Unsealed = DiagnosticDescriptorHelper.Create(RuleCA2229Id,
                                                                                s_localizableTitleCA2229,
                                                                                MicrosoftNetCoreAnalyzersResources.ImplementSerializationConstructorsMessageMakeUnsealedMagicConstructorFamily,
                                                                                DiagnosticCategory.Usage,
                                                                                RuleLevel.IdeHidden_BulkConfigurable,
                                                                                description: s_localizableDescriptionCA2229,
                                                                                isPortedFxCopRule: true,
                                                                        isDataflowRule: false);

        // Mark ISerializable types with SerializableAttribute
        internal const string RuleCA2237Id = "CA2237";

        private static readonly LocalizableString s_localizableTitleCA2237 =
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.MarkISerializableTypesWithSerializableTitle),
                MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        private static readonly LocalizableString s_localizableMessageCA2237 =
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.MarkISerializableTypesWithSerializableMessage),
                MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        private static readonly LocalizableString s_localizableDescriptionCA2237 =
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.MarkISerializableTypesWithSerializableDescription),
                MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor RuleCA2237 = DiagnosticDescriptorHelper.Create(RuleCA2237Id,
                                                                        s_localizableTitleCA2237,
                                                                        s_localizableMessageCA2237,
                                                                        DiagnosticCategory.Usage,
                                                                        RuleLevel.CandidateForRemoval,   // Cannot implement this for .NET Core: https://github.com/dotnet/roslyn-analyzers/issues/1775#issuecomment-518457308
                                                                        description: s_localizableDescriptionCA2237,
                                                                        isPortedFxCopRule: true,
                                                                        isDataflowRule: false);

        // Mark all non-serializable fields
        internal const string RuleCA2235Id = "CA2235";

        private static readonly LocalizableString s_localizableTitleCA2235 =
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.MarkAllNonSerializableFieldsTitle),
                MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        private static readonly LocalizableString s_localizableMessageCA2235 =
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.MarkAllNonSerializableFieldsMessage),
                MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        private static readonly LocalizableString s_localizableDescriptionCA2235 =
            new LocalizableResourceString(
                nameof(MicrosoftNetCoreAnalyzersResources.MarkAllNonSerializableFieldsDescription),
                MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor RuleCA2235 = DiagnosticDescriptorHelper.Create(RuleCA2235Id,
                                                                        s_localizableTitleCA2235,
                                                                        s_localizableMessageCA2235,
                                                                        DiagnosticCategory.Usage,
                                                                        RuleLevel.CandidateForRemoval,   // Cannot implement this for .NET Core: https://github.com/dotnet/roslyn-analyzers/issues/1775#issuecomment-518457308
                                                                        description: s_localizableDescriptionCA2235,
                                                                        isPortedFxCopRule: true,
                                                                        isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(RuleCA2229Default, RuleCA2229Sealed, RuleCA2229Unsealed, RuleCA2235, RuleCA2237);

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

                    INamedTypeSymbol? serializationInfoTypeSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeSerializationSerializationInfo);
                    if (serializationInfoTypeSymbol == null)
                    {
                        return;
                    }

                    INamedTypeSymbol? streamingContextTypeSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeSerializationStreamingContext);
                    if (streamingContextTypeSymbol == null)
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

                    var symbolAnalyzer = new SymbolAnalyzer(iserializableTypeSymbol, serializationInfoTypeSymbol, streamingContextTypeSymbol, serializableAttributeTypeSymbol, nonSerializedAttributeTypeSymbol, isNetStandardAssembly);
                    context.RegisterSymbolAction(symbolAnalyzer.AnalyzeSymbol, SymbolKind.NamedType);
                });
        }

        private sealed class SymbolAnalyzer
        {
            private readonly INamedTypeSymbol _iserializableTypeSymbol;
            private readonly INamedTypeSymbol _serializationInfoTypeSymbol;
            private readonly INamedTypeSymbol _streamingContextTypeSymbol;
            private readonly INamedTypeSymbol _serializableAttributeTypeSymbol;
            private readonly INamedTypeSymbol _nonSerializedAttributeTypeSymbol;
            private readonly bool _isNetStandardAssembly;

            public SymbolAnalyzer(
                INamedTypeSymbol iserializableTypeSymbol,
                INamedTypeSymbol serializationInfoTypeSymbol,
                INamedTypeSymbol streamingContextTypeSymbol,
                INamedTypeSymbol serializableAttributeTypeSymbol,
                INamedTypeSymbol nonSerializedAttributeTypeSymbol,
                bool isNetStandardAssembly)
            {
                _iserializableTypeSymbol = iserializableTypeSymbol;
                _serializationInfoTypeSymbol = serializationInfoTypeSymbol;
                _streamingContextTypeSymbol = streamingContextTypeSymbol;
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

                var implementsISerializable = namedTypeSymbol.AllInterfaces.Contains(_iserializableTypeSymbol);
                var isSerializable = IsSerializable(namedTypeSymbol);

                // If the type is public and implements ISerializable
                if (namedTypeSymbol.DeclaredAccessibility == Accessibility.Public && implementsISerializable)
                {
                    if (!isSerializable)
                    {
                        // CA2237 : Mark serializable types with the SerializableAttribute
                        if (namedTypeSymbol.BaseType.SpecialType == SpecialType.System_Object ||
                            IsSerializable(namedTypeSymbol.BaseType))
                        {
                            context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(RuleCA2237, namedTypeSymbol.Name));
                        }
                    }
                    else
                    {
                        // Look for a serialization constructor.
                        // A serialization constructor takes two params of type SerializationInfo and StreamingContext.
                        IMethodSymbol serializationCtor = namedTypeSymbol.Constructors
                            .FirstOrDefault(c => c.IsSerializationConstructor(_serializationInfoTypeSymbol, _streamingContextTypeSymbol));

                        // There is no serialization ctor - issue a diagnostic.
                        if (serializationCtor == null)
                        {
                            context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(RuleCA2229Default, namedTypeSymbol.Name));
                        }
                        else
                        {
                            // Check the accessibility
                            // The serialization ctor should be protected if the class is unsealed and private if the class is sealed.
                            if (namedTypeSymbol.IsSealed &&
                                serializationCtor.DeclaredAccessibility != Accessibility.Private)
                            {
                                context.ReportDiagnostic(serializationCtor.CreateDiagnostic(RuleCA2229Sealed, namedTypeSymbol.Name));
                            }

                            if (!namedTypeSymbol.IsSealed &&
                                serializationCtor.DeclaredAccessibility != Accessibility.Protected)
                            {
                                context.ReportDiagnostic(serializationCtor.CreateDiagnostic(RuleCA2229Unsealed, namedTypeSymbol.Name));
                            }
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
                        if (field.HasAttribute(_nonSerializedAttributeTypeSymbol))
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
                    TypeKind.Enum => IsSerializable(((INamedTypeSymbol)type).EnumUnderlyingType),
                    TypeKind.TypeParameter or TypeKind.Interface => true,// The concrete type can't be determined statically,
                                                                         // so we assume true to cut down on noise.
                    TypeKind.Class or TypeKind.Struct => ((INamedTypeSymbol)type).IsSerializable,// Check SerializableAttribute or Serializable flag from metadata.
                    TypeKind.Delegate => true,// delegates are always serializable, even if
                                              // they aren't actually marked [Serializable]
                    _ => type.HasAttribute(_serializableAttributeTypeSymbol),
                };
            }
        }
    }
}
