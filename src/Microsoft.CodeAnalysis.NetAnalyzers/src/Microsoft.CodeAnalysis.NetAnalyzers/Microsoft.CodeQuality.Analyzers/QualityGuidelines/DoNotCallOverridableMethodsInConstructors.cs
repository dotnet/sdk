// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    /// <summary>
    /// CA2214: Do not call overridable methods in constructors
    ///
    /// Cause: The constructor of an unsealed type calls a virtual method defined in its class.
    ///
    /// Description: When a virtual method is called, the actual type that executes the method is not selected
    /// until run time. When a constructor calls a virtual method, it is possible that the constructor for the
    /// instance that invokes the method has not executed.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotCallOverridableMethodsInConstructorsAnalyzer : DiagnosticAnalyzer
    {
        public const string RuleId = "CA2214";
        private static readonly LocalizableString s_localizableMessageAndTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotCallOverridableMethodsInConstructors), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotCallOverridableMethodsInConstructorsDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        public static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                         s_localizableMessageAndTitle,
                                                                         s_localizableMessageAndTitle,
                                                                         DiagnosticCategory.Usage,
                                                                         RuleLevel.Disabled,
                                                                         description: s_localizableDescription,
                                                                         isPortedFxCopRule: true,
                                                                         isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationContext =>
            {
                INamedTypeSymbol? webUiControlType = compilationContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebUIControl);
                INamedTypeSymbol? componentModelComponentType = compilationContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemComponentModelComponent);

                compilationContext.RegisterOperationBlockStartAction(context =>
                {
                    if (ShouldOmitThisDiagnostic(context.OwningSymbol, webUiControlType, componentModelComponentType))
                    {
                        return;
                    }

                    context.RegisterOperationAction(oc => AnalyzeOperation(oc, context.OwningSymbol.ContainingType), OperationKind.Invocation);
                });
            });
        }

        private static void AnalyzeOperation(OperationAnalysisContext context, INamedTypeSymbol containingType)
        {
            var operation = (IInvocationOperation)context.Operation;
            IMethodSymbol method = operation.TargetMethod;
            if (method != null &&
                (method.IsAbstract || method.IsVirtual) &&
                Equals(method.ContainingType, containingType) &&
                !operation.IsWithinLambdaOrLocalFunction(out _))
            {
                context.ReportDiagnostic(operation.Syntax.CreateDiagnostic(Rule));
            }
        }

        private static bool ShouldOmitThisDiagnostic(ISymbol symbol, INamedTypeSymbol? webUiControlType, INamedTypeSymbol? componentModelComponentType)
        {
            // This diagnostic is only relevant in constructors.
            // TODO: should this apply to instance field initializers for VB?
            if (symbol is not IMethodSymbol m || m.MethodKind != MethodKind.Constructor)
            {
                return true;
            }

            INamedTypeSymbol containingType = m.ContainingType;
            if (containingType == null)
            {
                return true;
            }

            // special case ASP.NET and Components constructors
            if (containingType.Inherits(webUiControlType))
            {
                return true;
            }

            if (containingType.Inherits(componentModelComponentType))
            {
                return true;
            }

            return false;
        }
    }
}