// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA2216: Disposable types should declare finalizer
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DisposableTypesShouldDeclareFinalizerAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2216";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DisposableTypesShouldDeclareFinalizerTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DisposableTypesShouldDeclareFinalizerMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DisposableTypesShouldDeclareFinalizerDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Usage,
                                                                             RuleLevel.CandidateForRemoval,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(
                compilationStartAnalysisContext =>
                {
                    Compilation compilation = compilationStartAnalysisContext.Compilation;

                    ImmutableHashSet<INamedTypeSymbol?> nativeResourceTypes = ImmutableHashSet.Create(
                        compilation.GetSpecialType(SpecialType.System_IntPtr),
                        compilation.GetSpecialType(SpecialType.System_UIntPtr),
                        compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesHandleRef)
                    );
                    var disposableType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIDisposable);

                    compilationStartAnalysisContext.RegisterOperationAction(
                        operationAnalysisContext =>
                        {
                            var assignment = (IAssignmentOperation)operationAnalysisContext.Operation;

                            IOperation target = assignment.Target;
                            if (target == null)
                            {
                                // This can happen if the left-hand side is an undefined symbol.
                                return;
                            }

                            if (target.Kind != OperationKind.FieldReference)
                            {
                                return;
                            }

                            var fieldReference = (IFieldReferenceOperation)target;
                            if (fieldReference.Member is not IFieldSymbol field || field.Kind != SymbolKind.Field || field.IsStatic)
                            {
                                return;
                            }

                            if (!nativeResourceTypes.Contains(field.Type))
                            {
                                return;
                            }

                            INamedTypeSymbol containingType = field.ContainingType;
                            if (containingType == null || containingType.IsValueType)
                            {
                                return;
                            }

                            if (!containingType.AllInterfaces.Contains(disposableType))
                            {
                                return;
                            }

                            if (containingType.HasFinalizer())
                            {
                                return;
                            }

                            if (assignment.Value == null || assignment.Value.Kind != OperationKind.Invocation)
                            {
                                return;
                            }

                            var invocation = (IInvocationOperation)assignment.Value;
                            if (invocation == null)
                            {
                                return;
                            }

                            IMethodSymbol method = invocation.TargetMethod;

                            // TODO: What about COM?
                            if (method.GetDllImportData() == null)
                            {
                                return;
                            }

                            operationAnalysisContext.ReportDiagnostic(containingType.CreateDiagnostic(Rule));
                        },
                        OperationKind.SimpleAssignment);
                });
        }
    }
}
