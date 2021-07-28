// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1063: Implement IDisposable Correctly
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class ImplementIDisposableCorrectlyAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1063";

        private const string DisposeMethodName = "Dispose";
        private const string GarbageCollectorTypeName = "System.GC";
        private const string SuppressFinalizeMethodName = "SuppressFinalize";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessageIDisposableReimplementation = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyMessageIDisposableReimplementation), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageFinalizeOverride = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyMessageFinalizeOverride), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageDisposeOverride = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyMessageDisposeOverride), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageDisposeSignature = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyMessageDisposeSignature), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageRenameDispose = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyMessageRenameDispose), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageDisposeBoolSignature = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyMessageDisposeBoolSignature), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageDisposeImplementation = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyMessageDisposeImplementation), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageFinalizeImplementation = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyMessageFinalizeImplementation), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageProvideDisposeBool = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyMessageProvideDisposeBool), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableCorrectlyDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        // Rules disabled by default until https://github.com/dotnet/docs/issues/8463 is resolved.

        internal static DiagnosticDescriptor IDisposableReimplementationRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageIDisposableReimplementation,
                                                                             DiagnosticCategory.Design,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);
        internal static DiagnosticDescriptor FinalizeOverrideRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageFinalizeOverride,
                                                                             DiagnosticCategory.Design,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);
        internal static DiagnosticDescriptor DisposeOverrideRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageDisposeOverride,
                                                                             DiagnosticCategory.Design,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);
        internal static DiagnosticDescriptor DisposeSignatureRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageDisposeSignature,
                                                                             DiagnosticCategory.Design,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);
        internal static DiagnosticDescriptor RenameDisposeRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageRenameDispose,
                                                                             DiagnosticCategory.Design,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);
        internal static DiagnosticDescriptor DisposeBoolSignatureRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageDisposeBoolSignature,
                                                                             DiagnosticCategory.Design,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);
        internal static DiagnosticDescriptor DisposeImplementationRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageDisposeImplementation,
                                                                             DiagnosticCategory.Design,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);
        internal static DiagnosticDescriptor FinalizeImplementationRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageFinalizeImplementation,
                                                                             DiagnosticCategory.Design,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);
        internal static DiagnosticDescriptor ProvideDisposeBoolRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageProvideDisposeBool,
                                                                             DiagnosticCategory.Design,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(IDisposableReimplementationRule, FinalizeOverrideRule, DisposeOverrideRule, DisposeSignatureRule, RenameDisposeRule, DisposeBoolSignatureRule, DisposeImplementationRule, FinalizeImplementationRule, ProvideDisposeBoolRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(
                context =>
                {
                    INamedTypeSymbol? disposableType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIDisposable);
                    if (disposableType == null)
                    {
                        return;
                    }

                    if (disposableType.GetMembers(DisposeMethodName).FirstOrDefault() is not IMethodSymbol disposeInterfaceMethod)
                    {
                        return;
                    }

                    INamedTypeSymbol? garbageCollectorType = context.Compilation.GetOrCreateTypeByMetadataName(GarbageCollectorTypeName);
                    if (garbageCollectorType == null)
                    {
                        return;
                    }

                    if (garbageCollectorType.GetMembers(SuppressFinalizeMethodName).FirstOrDefault() is not IMethodSymbol suppressFinalizeMethod)
                    {
                        return;
                    }

                    var analyzer = new PerCompilationAnalyzer(disposableType, disposeInterfaceMethod, suppressFinalizeMethod);
                    analyzer.Initialize(context);
                });
        }

        /// <summary>
        /// Analyzes single instance of compilation.
        /// </summary>
        private class PerCompilationAnalyzer
        {
            private readonly INamedTypeSymbol _disposableType;
            private readonly IMethodSymbol _disposeInterfaceMethod;
            private readonly IMethodSymbol _suppressFinalizeMethod;

            public PerCompilationAnalyzer(INamedTypeSymbol disposableType, IMethodSymbol disposeInterfaceMethod, IMethodSymbol suppressFinalizeMethod)
            {
                _disposableType = disposableType;
                _disposeInterfaceMethod = disposeInterfaceMethod;
                _suppressFinalizeMethod = suppressFinalizeMethod;
            }

            public void Initialize(CompilationStartAnalysisContext context)
            {
                context.RegisterSymbolAction(AnalyzeNamedTypeSymbol, SymbolKind.NamedType);
                context.RegisterOperationBlockAction(AnalyzeOperationBlock);
            }

            private void AnalyzeNamedTypeSymbol(SymbolAnalysisContext context)
            {
                // Note all the descriptors/rules for this analyzer have the same ID and category and hence
                // will always have identical configured visibility.
                if (context.Symbol is INamedTypeSymbol type &&
                    type.TypeKind == TypeKind.Class &&
                    context.Options.MatchesConfiguredVisibility(IDisposableReimplementationRule, type, context.Compilation))
                {
                    bool implementsDisposableInBaseType = ImplementsDisposableInBaseType(type);

                    if (ImplementsDisposableDirectly(type))
                    {
                        if (type.Interfaces.Contains(_disposableType))
                        {
                            // This differs from FxCop implementation
                            // Reports violation when type redundantly declares IDisposable as implemented interface
                            CheckIDisposableReimplementationRule(type, context, implementsDisposableInBaseType);
                        }

                        IMethodSymbol? disposeMethod = FindDisposeMethod(type);
                        if (disposeMethod != null)
                        {
                            CheckDisposeSignatureRule(disposeMethod, type, context);
                            CheckRenameDisposeRule(disposeMethod, type, context);

                            if (!type.IsSealed)
                            {
                                IMethodSymbol disposeBoolMethod = FindDisposeBoolMethod(type);
                                if (disposeBoolMethod != null)
                                {
                                    CheckDisposeBoolSignatureRule(disposeBoolMethod, type, context);
                                }
                                else
                                {
                                    CheckProvideDisposeBoolRule(type, context);
                                }
                            }
                        }
                    }

                    if (implementsDisposableInBaseType && FindInheritedDisposeBoolMethod(type) != null)
                    {
                        foreach (IMethodSymbol method in type.GetMembers().OfType<IMethodSymbol>())
                        {
                            CheckDisposeOverrideRule(method, type, context);
                        }

                        CheckFinalizeOverrideRule(type, context);
                    }
                }
            }

            private void AnalyzeOperationBlock(OperationBlockAnalysisContext context)
            {
                if (context.OwningSymbol is not IMethodSymbol method)
                {
                    return;
                }

                bool isFinalizerMethod = method.IsFinalizer();
                bool isDisposeMethod = method.Name == DisposeMethodName;
                if (isFinalizerMethod || isDisposeMethod)
                {
                    // Note all the descriptors/rules for this analyzer have the same ID and category and hence
                    // will always have identical configured visibility.
                    INamedTypeSymbol type = method.ContainingType;
                    if (type != null && type.TypeKind == TypeKind.Class &&
                        !type.IsSealed && context.Options.MatchesConfiguredVisibility(IDisposableReimplementationRule, type, context.Compilation))
                    {
                        if (ImplementsDisposableDirectly(type))
                        {
                            IMethodSymbol? disposeMethod = FindDisposeMethod(type);
                            if (disposeMethod != null)
                            {
                                if (method.Equals(disposeMethod))
                                {
                                    CheckDisposeImplementationRule(method, type, context.OperationBlocks, context);
                                }
                                else if (isFinalizerMethod)
                                {
                                    CheckFinalizeImplementationRule(method, type, context.OperationBlocks, context);
                                }
                            }
                        }
                        else if (isFinalizerMethod &&
                            ImplementsDisposableInBaseType(type) &&
                            FindInheritedDisposeBoolMethod(type) != null)
                        {
                            // Finalizer must invoke Dispose(false) if any of its base type has a Dispose(bool) implementation.
                            CheckFinalizeImplementationRule(method, type, context.OperationBlocks, context);
                        }
                    }
                }
            }

            /// <summary>
            /// Check rule: Remove IDisposable from the list of interfaces implemented by {0} as it is already implemented by base type {1}.
            /// </summary>
            private static void CheckIDisposableReimplementationRule(INamedTypeSymbol type, SymbolAnalysisContext context, bool implementsDisposableInBaseType)
            {
                if (implementsDisposableInBaseType)
                {
                    context.ReportDiagnostic(type.CreateDiagnostic(IDisposableReimplementationRule, type.Name, type.BaseType.Name));
                }
            }

            /// <summary>
            /// Checks rule: Ensure that {0} is declared as public and sealed.
            /// </summary>
            private static void CheckDisposeSignatureRule(IMethodSymbol method, INamedTypeSymbol type, SymbolAnalysisContext context)
            {
                if (!method.IsPublic() ||
                    method.IsAbstract || method.IsVirtual || (method.IsOverride && !method.IsSealed))
                {
                    context.ReportDiagnostic(method.CreateDiagnostic(DisposeSignatureRule, $"{type.Name}.{method.Name}"));
                }
            }

            /// <summary>
            /// Checks rule: Rename {0} to 'Dispose' and ensure that it is declared as public and sealed.
            /// </summary>
            private static void CheckRenameDisposeRule(IMethodSymbol method, INamedTypeSymbol type, SymbolAnalysisContext context)
            {
                if (method.Name != DisposeMethodName)
                {
                    context.ReportDiagnostic(method.CreateDiagnostic(RenameDisposeRule, $"{type.Name}.{method.Name}"));
                }
            }

            /// <summary>
            /// Checks rule: Remove {0}, override Dispose(bool disposing), and put the dispose logic in the code path where 'disposing' is true.
            /// </summary>
            private void CheckDisposeOverrideRule(IMethodSymbol method, INamedTypeSymbol type, SymbolAnalysisContext context)
            {
                if (method.MethodKind == MethodKind.Ordinary && method.IsOverride && method.ReturnsVoid && method.Parameters.IsEmpty)
                {
                    bool isDisposeOverride = false;
                    for (IMethodSymbol m = method.OverriddenMethod; m != null; m = m.OverriddenMethod)
                    {
                        if (Equals(m, FindDisposeMethod(m.ContainingType)))
                        {
                            isDisposeOverride = true;
                            break;
                        }
                    }

                    if (isDisposeOverride)
                    {
                        context.ReportDiagnostic(method.CreateDiagnostic(DisposeOverrideRule, $"{type.Name}.{method.Name}"));
                    }
                }
            }

            /// <summary>
            /// Checks rule: Remove the finalizer from type {0}, override Dispose(bool disposing), and put the finalization logic in the code path where 'disposing' is false. Otherwise, it might lead to duplicate Dispose invocations as the Base type '{1}' also provides a finalizer.
            /// </summary>
            private static void CheckFinalizeOverrideRule(INamedTypeSymbol type, SymbolAnalysisContext context)
            {
                if (type.HasFinalizer())
                {
                    // Flag the finalizer if there is any base type with a finalizer, this can cause duplicate Dispose(false) invocations.
                    var baseTypeWithFinalizer = GetFirstBaseTypeWithFinalizerOrDefault(type);
                    if (baseTypeWithFinalizer != null)
                    {
                        context.ReportDiagnostic(type.CreateDiagnostic(FinalizeOverrideRule, type.Name, baseTypeWithFinalizer.Name));
                    }
                }
            }

            /// <summary>
            /// Checks rule: Provide an overridable implementation of Dispose(bool) on {0} or mark the type as sealed. A call to Dispose(false) should only clean up native resources. A call to Dispose(true) should clean up both managed and native resources.
            /// </summary>
            private static void CheckProvideDisposeBoolRule(INamedTypeSymbol type, SymbolAnalysisContext context)
            {
                context.ReportDiagnostic(type.CreateDiagnostic(ProvideDisposeBoolRule, type.Name));
            }

            /// <summary>
            /// Checks rule: Ensure that {0} is declared as protected, virtual, and unsealed.
            /// </summary>
            private static void CheckDisposeBoolSignatureRule(IMethodSymbol method, INamedTypeSymbol type, SymbolAnalysisContext context)
            {
                if (method.DeclaredAccessibility != Accessibility.Protected ||
                    !(method.IsVirtual || method.IsAbstract || method.IsOverride) || method.IsSealed)
                {
                    context.ReportDiagnostic(method.CreateDiagnostic(DisposeBoolSignatureRule, $"{type.Name}.{method.Name}"));
                }
            }

            /// <summary>
            /// Checks rule: Modify {0} so that it calls Dispose(true), then calls GC.SuppressFinalize on the current object instance ('this' or 'Me' in Visual Basic), and then returns.
            /// </summary>
            private void CheckDisposeImplementationRule(IMethodSymbol method, INamedTypeSymbol type, ImmutableArray<IOperation> operationBlocks, OperationBlockAnalysisContext context)
            {
                var validator = new DisposeImplementationValidator(_suppressFinalizeMethod, type);
                if (!validator.Validate(operationBlocks))
                {
                    context.ReportDiagnostic(method.CreateDiagnostic(DisposeImplementationRule, $"{type.Name}.{method.Name}"));
                }
            }

            /// <summary>
            /// Checks rule: Modify {0} so that it calls Dispose(false) and then returns.
            /// </summary>
            private static void CheckFinalizeImplementationRule(IMethodSymbol method, INamedTypeSymbol type, ImmutableArray<IOperation> operationBlocks, OperationBlockAnalysisContext context)
            {
                // Bail out if any base type also provides a finalizer - we will fire CheckFinalizeOverrideRule for that case.
                if (GetFirstBaseTypeWithFinalizerOrDefault(type) != null)
                {
                    return;
                }

                var validator = new FinalizeImplementationValidator(type);
                if (!validator.Validate(operationBlocks))
                {
                    context.ReportDiagnostic(method.CreateDiagnostic(FinalizeImplementationRule, $"{type.Name}.{method.Name}"));
                }
            }

            private static INamedTypeSymbol GetFirstBaseTypeWithFinalizerOrDefault(INamedTypeSymbol type)
                => type.GetBaseTypes().FirstOrDefault(baseType => baseType.SpecialType != SpecialType.System_Object && baseType.HasFinalizer());

            /// <summary>
            /// Checks if type implements IDisposable interface or an interface inherited from IDisposable.
            /// Only direct implementation is taken into account, implementation in base type is ignored.
            /// </summary>
            private bool ImplementsDisposableDirectly(ITypeSymbol type)
            {
                return type.Interfaces.Any(i => i.Inherits(_disposableType));
            }

            /// <summary>
            /// Checks if base type implements IDisposable interface directly or indirectly.
            /// </summary>
            private bool ImplementsDisposableInBaseType(ITypeSymbol type)
            {
                return type.BaseType != null && type.BaseType.AllInterfaces.Contains(_disposableType);
            }

            /// <summary>
            /// Returns method that implements IDisposable.Dispose operation.
            /// Only direct implementation is taken into account, implementation in base type is ignored.
            /// </summary>
            private IMethodSymbol? FindDisposeMethod(INamedTypeSymbol type)
            {
                if (type.FindImplementationForInterfaceMember(_disposeInterfaceMethod) is IMethodSymbol disposeMethod && Equals(disposeMethod.ContainingType, type))
                {
                    return disposeMethod;
                }

                return null;
            }

            /// <summary>
            /// Returns method: void Dispose(bool)
            /// </summary>
            private static IMethodSymbol FindDisposeBoolMethod(INamedTypeSymbol type)
            {
                return type.GetMembers(DisposeMethodName).OfType<IMethodSymbol>().FirstOrDefault(m => m.HasDisposeBoolMethodSignature());
            }

            /// <summary>
            /// Returns method defined in the nearest ancestor: void Dispose(bool)
            /// </summary>
            private IMethodSymbol? FindInheritedDisposeBoolMethod(INamedTypeSymbol type)
            {
                IMethodSymbol? method = null;

                while (type != null && method == null && ImplementsDisposableInBaseType(type))
                {
                    type = type.BaseType;
                    method = FindDisposeBoolMethod(type);
                }

                return method;
            }
        }

        private static bool IsDisposeBoolCall(IInvocationOperation invocationExpression, INamedTypeSymbol type, bool expectedValue)
        {
            if (!invocationExpression.TargetMethod.HasDisposeBoolMethodSignature())
            {
                return false;
            }

            if (invocationExpression.Instance == null)
            {
                if (!type.Equals(invocationExpression.TargetMethod.ContainingType))
                {
                    return false;
                }
            }
            else if (invocationExpression.Instance.Kind != OperationKind.InstanceReference ||
                !type.Equals(invocationExpression.Instance.Type))
            {
                return false;
            }

            if (invocationExpression.Arguments.Length != 1)
            {
                return false;
            }

            IArgumentOperation argument = invocationExpression.Arguments[0];
            if (argument.Value.Kind != OperationKind.Literal)
            {
                return false;
            }

            var literal = (ILiteralOperation)argument.Value;
            if (!literal.ConstantValue.HasValue || !expectedValue.Equals(literal.ConstantValue.Value))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates implementation of Dispose method. The method must call Dispose(true) and then GC.SuppressFinalize(this).
        /// </summary>
        private sealed class DisposeImplementationValidator
        {
            private readonly IMethodSymbol _suppressFinalizeMethod;
            private readonly INamedTypeSymbol _type;
            private bool _callsDisposeBool;
            private bool _callsSuppressFinalize;

            public DisposeImplementationValidator(IMethodSymbol suppressFinalizeMethod, INamedTypeSymbol type)
            {
                _callsDisposeBool = false;
                _callsSuppressFinalize = false;
                _suppressFinalizeMethod = suppressFinalizeMethod;
                _type = type;
            }

            public bool Validate(ImmutableArray<IOperation> operations)
            {
                _callsDisposeBool = false;
                _callsSuppressFinalize = false;

                if (ValidateOperations(operations))
                {
                    return _callsDisposeBool && (_callsSuppressFinalize || !_type.HasFinalizer());
                }

                return false;
            }

            private bool ValidateOperations(ImmutableArray<IOperation> operations)
            {
                foreach (IOperation operation in operations)
                {
                    if (!operation.IsImplicit && !ValidateOperation(operation))
                    {
                        return false;
                    }
                }

                return true;
            }

            private bool ValidateOperation(IOperation operation)
            {
                switch (operation.Kind)
                {
                    case OperationKind.Empty:
                    case OperationKind.Labeled:
                        return true;
                    case OperationKind.Block:
                        var blockStatement = (IBlockOperation)operation;
                        return ValidateOperations(blockStatement.Operations);
                    case OperationKind.ExpressionStatement:
                        var expressionStatement = (IExpressionStatementOperation)operation;
                        return ValidateExpression(expressionStatement);
                    default:
                        // Ignore operation roots with no IOperation API support (OperationKind.None)
                        return operation.IsOperationNoneRoot();
                }
            }

            private bool ValidateExpression(IExpressionStatementOperation expressionStatement)
            {
                if (expressionStatement.Operation == null || expressionStatement.Operation.Kind != OperationKind.Invocation)
                {
                    return false;
                }

                var invocationExpression = (IInvocationOperation)expressionStatement.Operation;
                if (!_callsDisposeBool)
                {
                    bool result = IsDisposeBoolCall(invocationExpression, _type, expectedValue: true);
                    if (result)
                    {
                        _callsDisposeBool = true;
                    }

                    return result;
                }
                else if (!_callsSuppressFinalize)
                {
                    bool result = IsSuppressFinalizeCall(invocationExpression);
                    if (result)
                    {
                        _callsSuppressFinalize = true;
                    }

                    return result;
                }

                return false;
            }

            private bool IsSuppressFinalizeCall(IInvocationOperation invocationExpression)
            {
                if (!Equals(invocationExpression.TargetMethod, _suppressFinalizeMethod))
                {
                    return false;
                }

                if (invocationExpression.Arguments.Length != 1)
                {
                    return false;
                }

                IOperation argumentValue = invocationExpression.Arguments[0].Value;
                if (argumentValue.Kind != OperationKind.Conversion)
                {
                    return false;
                }

                var conversion = (IConversionOperation)argumentValue;
                if (conversion.Operand == null || conversion.Operand.Kind != OperationKind.InstanceReference)
                {
                    return false;
                }

                if (!_type.Equals(conversion.Operand.Type))
                {
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Validates implementation of the finalizer. This method must call Dispose(false) and then return
        /// </summary>
        private sealed class FinalizeImplementationValidator
        {
            private readonly INamedTypeSymbol _type;
            private bool _callDispose;

            public FinalizeImplementationValidator(INamedTypeSymbol type)
            {
                _type = type;
                _callDispose = false;
            }

            public bool Validate(ImmutableArray<IOperation> operations)
            {
                _callDispose = false;

                if (ValidateOperations(operations))
                {
                    return _callDispose;
                }

                return false;
            }

            private bool ValidateOperations(ImmutableArray<IOperation> operations)
            {
                foreach (var operation in operations)
                {
                    // We need to analyze implicit try statements. This is because if the base type has
                    // a finalizer, C# will create a try/finally statement to wrap the finalizer, with a
                    // call to the base finalizer in the finally section. We need to validate the contents
                    // of the try block
                    // Also analyze the implicit expression statement created for expression bodied implementation.
                    var shouldAnalyze = !operation.IsImplicit || operation.Kind == OperationKind.Try || operation.Kind == OperationKind.ExpressionStatement;
                    if (shouldAnalyze && !ValidateOperation(operation))
                    {
                        return false;
                    }
                }

                return true;
            }

            private bool ValidateOperation(IOperation operation)
            {
                return operation.Kind switch
                {
                    OperationKind.Empty or OperationKind.Labeled => true,
                    OperationKind.Block => ValidateOperations(((IBlockOperation)operation).Operations),
                    OperationKind.ExpressionStatement => ValidateExpression((IExpressionStatementOperation)operation),
                    OperationKind.Try => ValidateTryOperation((ITryOperation)operation),
                    _ => operation.IsOperationNoneRoot(),// Ignore operation roots with no IOperation API support (OperationKind.None)
                };
            }

            private bool ValidateExpression(IExpressionStatementOperation expressionStatement)
            {
                if (expressionStatement.Operation?.Kind != OperationKind.Invocation)
                {
                    return false;
                }

                var invocation = (IInvocationOperation)expressionStatement.Operation;

                // Valid calls are either to Dispose(false), or to the Finalize method of the base type
                if (!_callDispose)
                {
                    bool result = IsDisposeBoolCall(invocation, _type, expectedValue: false);
                    if (result)
                    {
                        _callDispose = true;
                    }

                    return result;
                }
                else if (_type.BaseType != null && invocation.Instance != null && invocation.Instance.Kind == OperationKind.InstanceReference)
                {
                    IMethodSymbol methodSymbol = invocation.TargetMethod;
                    IInstanceReferenceOperation receiver = (IInstanceReferenceOperation)invocation.Instance;

                    return methodSymbol.IsFinalizer() && Equals(receiver.Type.OriginalDefinition, _type.BaseType.OriginalDefinition);
                }

                return false;
            }

            private bool ValidateTryOperation(ITryOperation tryOperation)
            {
                // The try operation must have been implicit, as we still analyze it if it isn't implicit
                if (!tryOperation.IsImplicit)
                {
                    return false;
                }

                // There is no way to pass this check without the finally block being correct,
                // as this try-finally is generated by the compiler. No need to verify
                // the contents of the finally.
                if (tryOperation.Finally == null || !tryOperation.Finally.IsImplicit)
                {
                    return false;
                }

                // The try statement is otherwise correct, so validate the main body
                return Validate(tryOperation.Body.Operations);
            }
        }
    }
}
