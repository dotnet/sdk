// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class SetViewStateUserKey : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "CA5368";
        private static readonly LocalizableString s_Title = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.SetViewStateUserKey),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_Message = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.SetViewStateUserKeyMessage),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_Description = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.SetViewStateUserKeyDescription),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
                DiagnosticId,
                s_Title,
                s_Message,
                DiagnosticCategory.Security,
                RuleLevel.IdeHidden_BulkConfigurable,
                description: s_Description,
                isPortedFxCopRule: false,
                isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
            {
                Compilation compilation = compilationStartAnalysisContext.Compilation;
                var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilationStartAnalysisContext.Compilation);

                if (!wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebUIPage, out var pageTypeSymbol) ||
                    !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemEventArgs, out var eventArgsTypeSymbol))
                {
                    return;
                }

                compilationStartAnalysisContext.RegisterSymbolAction(symbolAnalysisContext =>
                {
                    var classSymbol = (INamedTypeSymbol)symbolAnalysisContext.Symbol;
                    var baseClassSymbol = classSymbol.BaseType;

                    if (pageTypeSymbol.Equals(baseClassSymbol))
                    {
                        var methods = classSymbol.GetMembers().OfType<IMethodSymbol>();
                        var setViewStateUserKeyInOnInit = SetViewStateUserKeyCorrectly(methods.FirstOrDefault(s => s.Name == "OnInit" &&
                                                                                                                    s.Parameters.Length == 1 &&
                                                                                                                    s.Parameters[0].Type.Equals(eventArgsTypeSymbol) &&
                                                                                                                    s.IsProtected() &&
                                                                                                                    !s.IsStatic));
                        var setViewStateUserKeyInPage_Init = SetViewStateUserKeyCorrectly(methods.FirstOrDefault(s => s.Name == "Page_Init" &&
                                                                                                                        s.HasEventHandlerSignature(eventArgsTypeSymbol)));

                        if (setViewStateUserKeyInOnInit || setViewStateUserKeyInPage_Init)
                        {
                            return;
                        }

                        symbolAnalysisContext.ReportDiagnostic(
                                    classSymbol.CreateDiagnostic(
                                        Rule,
                                        classSymbol.Name));
                    }
                }, SymbolKind.NamedType);

                bool SetViewStateUserKeyCorrectly(IMethodSymbol methodSymbol)
                {
                    return methodSymbol?.GetTopmostOperationBlock(compilation)
                                        .Descendants()
                                        .Any(s => s is ISimpleAssignmentOperation simpleAssignmentOperation &&
                                                    simpleAssignmentOperation.Target is IPropertyReferenceOperation propertyReferenceOperation &&
                                                    propertyReferenceOperation.Property.Name == "ViewStateUserKey" &&
                                                    propertyReferenceOperation.Property.Type.SpecialType == SpecialType.System_String &&
                                                    (propertyReferenceOperation.Instance is IInstanceReferenceOperation instanceReferenceOperation &&
                                                    instanceReferenceOperation.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance ||
                                                    propertyReferenceOperation.Instance is IPropertyReferenceOperation propertyReferenceOperation2 &&
                                                    propertyReferenceOperation2.Property.IsVirtual &&
                                                    propertyReferenceOperation2.Instance is IInstanceReferenceOperation instanceReferenceOperation2 &&
                                                    instanceReferenceOperation2.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance))
                                        ?? false;
                }
            });
        }
    }
}
