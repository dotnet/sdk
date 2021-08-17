// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA2215: Dispose methods should call base class dispose
    ///
    /// A type that implements System.IDisposable inherits from a type that also implements IDisposable.
    /// The Dispose method of the inheriting type does not call the Dispose method of the parent type.
    /// To fix a violation of this rule, call base.Dispose in your Dispose method.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DisposeMethodsShouldCallBaseClassDispose : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2215";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DisposeMethodsShouldCallBaseClassDisposeTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DisposeMethodsShouldCallBaseClassDisposeMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DisposeMethodsShouldCallBaseClassDisposeDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Usage,
                                                                             RuleLevel.IdeHidden_BulkConfigurable,
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
                if (!DisposeAnalysisHelper.TryGetOrCreate(compilationContext.Compilation, out DisposeAnalysisHelper? disposeAnalysisHelper))
                {
                    return;
                }

                compilationContext.RegisterOperationBlockStartAction(operationBlockStartContext =>
                {
                    if (operationBlockStartContext.OwningSymbol is not IMethodSymbol containingMethod ||
                        containingMethod.OverriddenMethod == null ||
                        containingMethod.OverriddenMethod.IsAbstract)
                    {
                        return;
                    }

                    var disposeMethodKind = disposeAnalysisHelper.GetDisposeMethodKind(containingMethod);
                    switch (disposeMethodKind)
                    {
                        case DisposeMethodKind.Dispose:
                        case DisposeMethodKind.DisposeBool:
                        case DisposeMethodKind.DisposeAsync:
                        case DisposeMethodKind.DisposeCoreAsync:
                            break;

                        case DisposeMethodKind.Close:
                            // FxCop compat: Ignore Close methods due to high false positive rate.
                            return;

                        default:
                            return;
                    }

                    var invokesBaseDispose = false;
                    operationBlockStartContext.RegisterOperationAction(operationContext =>
                    {
                        if (invokesBaseDispose)
                        {
                            return;
                        }

                        var invocation = (IInvocationOperation)operationContext.Operation;
                        if (Equals(invocation.TargetMethod, containingMethod.OverriddenMethod) &&
                            invocation.Instance is IInstanceReferenceOperation instanceReference &&
                            instanceReference.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance)
                        {
                            Debug.Assert(disposeAnalysisHelper.GetDisposeMethodKind(invocation.TargetMethod) == disposeMethodKind);
                            invokesBaseDispose = true;
                        }
                    }, OperationKind.Invocation);

                    operationBlockStartContext.RegisterOperationBlockEndAction(operationEndContext =>
                    {
                        if (!invokesBaseDispose)
                        {
                            // Ensure that method '{0}' calls '{1}' in all possible control flow paths.
                            var arg1 = containingMethod.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                            var baseKeyword = containingMethod.Language == LanguageNames.CSharp ? "base" : "MyBase";
                            var disposeMethodParam = (disposeMethodKind is DisposeMethodKind.DisposeBool or DisposeMethodKind.DisposeCoreAsync) ?
                                containingMethod.Language == LanguageNames.CSharp ? "bool" : "Boolean" :
                                string.Empty;
                            var disposeMethodName = disposeMethodKind == DisposeMethodKind.DisposeBool ?
                                "Dispose" :
                                disposeMethodKind.ToString();
                            var arg2 = $"{baseKeyword}.{disposeMethodName}({disposeMethodParam})";
                            var diagnostic = containingMethod.CreateDiagnostic(Rule, arg1, arg2);
                            operationEndContext.ReportDiagnostic(diagnostic);
                        }
                    });
                });
            });
        }
    }
}
