// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Publish
{
    /// <summary>
    /// IL3000, IL3001: Do not use Assembly file path in single-file publish
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class AvoidAssemblyLocationInSingleFile : DiagnosticAnalyzer
    {
        public const string IL3000 = nameof(IL3000);
        public const string IL3001 = nameof(IL3001);

        internal static DiagnosticDescriptor LocationRule = DiagnosticDescriptorHelper.Create(
            IL3000,
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.AvoidAssemblyLocationInSingleFileTitle),
                MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.AvoidAssemblyLocationInSingleFileMessage),
                MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
            DiagnosticCategory.Publish,
            RuleLevel.BuildWarning,
            description: null,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static DiagnosticDescriptor GetFilesRule = DiagnosticDescriptorHelper.Create(
            IL3001,
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.AvoidAssemblyLocationInSingleFileTitle),
                MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.AvoidAssemblyGetFilesInSingleFileMessage),
                MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
            DiagnosticCategory.Publish,
            RuleLevel.BuildWarning,
            description: null,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(LocationRule, GetFilesRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(context =>
            {
                var compilation = context.Compilation;
                var isSingleFilePublish = context.Options.GetMSBuildPropertyValue(
                    MSBuildPropertyOptionNames.PublishSingleFile, compilation);
                if (!string.Equals(isSingleFilePublish?.Trim(), "true", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
                var includesAllContent = context.Options.GetMSBuildPropertyValue(
                    MSBuildPropertyOptionNames.IncludeAllContentForSelfExtract, compilation);
                if (string.Equals(includesAllContent?.Trim(), "true", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var propertiesBuilder = ImmutableArray.CreateBuilder<IPropertySymbol>();
                var methodsBuilder = ImmutableArray.CreateBuilder<IMethodSymbol>();

                if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReflectionAssembly, out var assemblyType))
                {
                    // properties
                    AddIfNotNull(propertiesBuilder, TryGetSingleSymbol<IPropertySymbol>(assemblyType.GetMembers("Location")));

                    // methods
                    methodsBuilder.AddRange(assemblyType.GetMembers("GetFile").OfType<IMethodSymbol>());
                    methodsBuilder.AddRange(assemblyType.GetMembers("GetFiles").OfType<IMethodSymbol>());
                }

                if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReflectionAssemblyName, out var assemblyNameType))
                {
                    AddIfNotNull(propertiesBuilder, TryGetSingleSymbol<IPropertySymbol>(assemblyNameType.GetMembers("CodeBase")));
                    AddIfNotNull(propertiesBuilder, TryGetSingleSymbol<IPropertySymbol>(assemblyNameType.GetMembers("EscapedCodeBase")));
                }

                var properties = propertiesBuilder.ToImmutable();
                var methods = methodsBuilder.ToImmutable();

                context.RegisterOperationAction(operationContext =>
                {
                    var access = (IPropertyReferenceOperation)operationContext.Operation;
                    var property = access.Property;
                    if (!Contains(properties, property, SymbolEqualityComparer.Default))
                    {
                        return;
                    }

                    operationContext.ReportDiagnostic(access.CreateDiagnostic(LocationRule, property));
                }, OperationKind.PropertyReference);

                context.RegisterOperationAction(operationContext =>
                {
                    var invocation = (IInvocationOperation)operationContext.Operation;
                    var targetMethod = invocation.TargetMethod;
                    if (!Contains(methods, targetMethod, SymbolEqualityComparer.Default))
                    {
                        return;
                    }

                    operationContext.ReportDiagnostic(invocation.CreateDiagnostic(GetFilesRule, targetMethod));
                }, OperationKind.Invocation);

                return;

                static bool Contains<T, TComp>(ImmutableArray<T> list, T elem, TComp comparer)
                    where TComp : IEqualityComparer<T>
                {
                    foreach (var e in list)
                    {
                        if (comparer.Equals(e, elem))
                        {
                            return true;
                        }
                    }
                    return false;
                }

                static TSymbol? TryGetSingleSymbol<TSymbol>(ImmutableArray<ISymbol> members) where TSymbol : class, ISymbol
                {
                    TSymbol? candidate = null;
                    foreach (var m in members)
                    {
                        if (m is TSymbol tsym)
                        {
                            if (candidate is null)
                            {
                                candidate = tsym;
                            }
                            else
                            {
                                return null;
                            }
                        }
                    }
                    return candidate;
                }

                static void AddIfNotNull<TSymbol>(ImmutableArray<TSymbol>.Builder properties, TSymbol? p) where TSymbol : class, ISymbol
                {
                    if (p is not null)
                    {
                        properties.Add(p);
                    }
                }
            });
        }
    }
}
