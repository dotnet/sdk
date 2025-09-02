// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>Analyzer that recommends using exception Throw helpers on built-in exception types.</summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseExceptionThrowHelpers : DiagnosticAnalyzer
    {
        internal const string UseArgumentNullExceptionThrowIfNullRuleId = "CA1510";
        internal const string UseArgumentExceptionThrowIfNullOrEmptyRuleId = "CA1511";
        internal const string UseArgumentOutOfRangeExceptionThrowIfRuleId = "CA1512";
        internal const string UseObjectDisposedExceptionThrowIfRuleId = "CA1513";

        /// <summary>Name of the key into the properties dictionary where an optional throw helper method name is stored for the fixer.</summary>
        internal const string MethodNamePropertyKey = "MethodNameProperty";

        // if (arg is null) throw new ArgumentNullException(nameof(arg)); => ArgumentNullException.ThrowIfNull(arg);
        // if (arg == null) throw new ArgumentNullException(nameof(arg)); => ArgumentNullException.ThrowIfNull(arg);
        internal static readonly DiagnosticDescriptor UseArgumentNullExceptionThrowIfNullRule = DiagnosticDescriptorHelper.Create(UseArgumentNullExceptionThrowIfNullRuleId,
            CreateLocalizableResourceString(nameof(UseArgumentNullExceptionThrowHelperTitle)),
            CreateLocalizableResourceString(nameof(UseThrowHelperMessage)),
            DiagnosticCategory.Maintainability,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(UseThrowHelperDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        // if (string.IsNullOrEmpty(arg)) throw new ArgumentException(...); => ArgumentException.ThrowIfNullOrEmpty(arg);
        // if (arg is null || arg.Length == 0) throw new ArgumentException(...); => ArgumentException.ThrowIfNullOrEmpty(arg);
        // if (arg == null || arg == string.Empty) throw new ArgumentException(...); => ArgumentException.ThrowIfNullOrEmpty(arg);
        internal static readonly DiagnosticDescriptor UseArgumentExceptionThrowIfNullOrEmptyRule = DiagnosticDescriptorHelper.Create(UseArgumentExceptionThrowIfNullOrEmptyRuleId,
            CreateLocalizableResourceString(nameof(UseArgumentExceptionThrowHelperTitle)),
            CreateLocalizableResourceString(nameof(UseThrowHelperMessage)),
            DiagnosticCategory.Maintainability,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(UseThrowHelperDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        // if (arg == 0) throw new ArgumentOutOfRangeException(...); => ArgumentOutOfRangeException.ThrowIfZero(arg);
        // if (arg is 0) throw new ArgumentOutOfRangeException(...); => ArgumentOutOfRangeException.ThrowIfZero(arg);
        // if (arg < 0) throw new ArgumentOutOfRangeException(...); => ArgumentOutOfRangeException.ThrowIfNegative(arg);
        // if (arg <= 0) throw new ArgumentOutOfRangeException(...); => ArgumentOutOfRangeException.ThrowIfNegativeOrZero(arg);
        // if (arg > 42) throw new ArgumentOutOfRangeException(...); => ArgumentOutOfRangeException.ThrowIfGreaterThan(arg, 42);
        // if (arg >= 42) throw new ArgumentOutOfRangeException(...); => ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(arg, 42);
        // if (arg < 42) throw new ArgumentOutOfRangeException(...); => ArgumentOutOfRangeException.ThrowIfLessThan(arg, 42);
        // if (arg <= 42) throw new ArgumentOutOfRangeException(...); => ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(arg, 42);
        // if (arg == 42) throw new ArgumentOutOfRangeException(...); => ArgumentOutOfRangeException.ThrowIfEqual(arg, 42);
        // if (arg != 42) throw new ArgumentOutOfRangeException(...); => ArgumentOutOfRangeException.ThrowIfNotEqual(arg, 42);
        internal static readonly DiagnosticDescriptor UseArgumentOutOfRangeExceptionThrowIfRule = DiagnosticDescriptorHelper.Create(UseArgumentOutOfRangeExceptionThrowIfRuleId,
            CreateLocalizableResourceString(nameof(UseArgumentOutOfRangeExceptionThrowHelperTitle)),
            CreateLocalizableResourceString(nameof(UseThrowHelperMessage)),
            DiagnosticCategory.Maintainability,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(UseThrowHelperDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        // if (condition) throw new ObjectDisposedException(...)
        internal static readonly DiagnosticDescriptor UseObjectDisposedExceptionThrowIfRule = DiagnosticDescriptorHelper.Create(UseObjectDisposedExceptionThrowIfRuleId,
            CreateLocalizableResourceString(nameof(UseObjectDisposedExceptionThrowHelperTitle)),
            CreateLocalizableResourceString(nameof(UseThrowHelperMessage)),
            DiagnosticCategory.Maintainability,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(UseThrowHelperDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
            UseArgumentNullExceptionThrowIfNullRule,
            UseArgumentExceptionThrowIfNullOrEmptyRule,
            UseArgumentOutOfRangeExceptionThrowIfRule,
            UseObjectDisposedExceptionThrowIfRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(context =>
            {
                // Get the relevant exception types.
                INamedTypeSymbol? ane = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemArgumentNullException);
                INamedTypeSymbol? aoore = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemArgumentOutOfRangeException);
                INamedTypeSymbol? ae = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemArgumentException);
                INamedTypeSymbol? ode = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemObjectDisposedException);
                if (ane is null || aoore is null || ae is null || ode is null)
                {
                    return;
                }

                // Get other required helper methods. Some diagnostics don't require these, but we also don't care
                // about reporting those on targets that don't have these, as they've been around forever.
                INamedTypeSymbol stringType = context.Compilation.GetSpecialType(SpecialType.System_String);
                INamedTypeSymbol objectType = context.Compilation.GetSpecialType(SpecialType.System_Object);
                ISymbol? stringIsNullOrEmpty = stringType.GetMembers("IsNullOrEmpty").FirstOrDefault();
                ISymbol? stringLength = stringType.GetMembers("Length").FirstOrDefault();
                ISymbol? stringEmpty = stringType.GetMembers("Empty").FirstOrDefault();
                ISymbol? getType = objectType.GetMembers("GetType").FirstOrDefault();
                if (stringIsNullOrEmpty is null || stringLength is null || stringEmpty is null || getType is null)
                {
                    return;
                }

                // Get the ThrowXx helpers on those types.  Some of these may not exist.
                // If we don't have any such helpers available, there's nothing to do.
                ISymbol? aneThrowIfNull = ane.GetMembers("ThrowIfNull").FirstOrDefault();
                ISymbol? aeThrowIfNullOrEmpty = ae.GetMembers("ThrowIfNullOrEmpty").FirstOrDefault();
                ISymbol? odeThrowIf = ode.GetMembers("ThrowIf").FirstOrDefault();
                ISymbol? aooreThrowIfZero = aoore.GetMembers("ThrowIfZero").FirstOrDefault();
                ISymbol? aooreThrowIfNegative = aoore.GetMembers("ThrowIfNegative").FirstOrDefault();
                ISymbol? aooreThrowIfNegativeOrZero = aoore.GetMembers("ThrowIfNegativeOrZero").FirstOrDefault();
                ISymbol? aooreThrowIfGreaterThan = aoore.GetMembers("ThrowIfGreaterThan").FirstOrDefault();
                ISymbol? aooreThrowIfGreaterThanOrEqual = aoore.GetMembers("ThrowIfGreaterThanOrEqual").FirstOrDefault();
                ISymbol? aooreThrowIfLessThan = aoore.GetMembers("ThrowIfLessThan").FirstOrDefault();
                ISymbol? aooreThrowIfLessThanOrEqual = aoore.GetMembers("ThrowIfLessThanOrEqual").FirstOrDefault();
                ISymbol? aooreThrowIfEqual = aoore.GetMembers("ThrowIfEqual").FirstOrDefault();
                ISymbol? aooreThrowIfNotEqual = aoore.GetMembers("ThrowIfNotEqual").FirstOrDefault();
                if (aneThrowIfNull is null && aeThrowIfNullOrEmpty is null && odeThrowIf is null &&
                    aooreThrowIfZero is null && aooreThrowIfNegative is null && aooreThrowIfNegativeOrZero is null &&
                    aooreThrowIfGreaterThan is null && aooreThrowIfGreaterThanOrEqual is null &&
                    aooreThrowIfLessThan is null && aooreThrowIfLessThanOrEqual is null &&
                    aooreThrowIfEqual is null && aooreThrowIfNotEqual is null)
                {
                    return;
                }

                // If we have any of the ArgumentOutOfRangeException.Throw methods, we're likely to have all of them.
                bool hasAnyAooreThrow =
                    aooreThrowIfZero is not null || aooreThrowIfNegative is not null || aooreThrowIfNegativeOrZero is not null ||
                    aooreThrowIfGreaterThan is not null || aooreThrowIfGreaterThanOrEqual is not null ||
                    aooreThrowIfLessThan is not null || aooreThrowIfLessThanOrEqual is not null ||
                    aooreThrowIfEqual is not null || aooreThrowIfNotEqual is not null;

                // Look for throw operations.
                context.RegisterOperationAction(context =>
                {
                    var throwOperation = (IThrowOperation)context.Operation;

                    // Try to get the exception object creation operation. As a heuristic, avoid recommending replacing
                    // any exceptions where a meaningful message may have been provided.  This is an attempt to reduce
                    // false positives, at the expense of potentially more false negatives in cases where a non-valuable
                    // error message was used.
                    if (throwOperation.GetThrownException() is not IObjectCreationOperation objectCreationOperation ||
                        HasPossiblyMeaningfulAdditionalArguments(objectCreationOperation))
                    {
                        return;
                    }

                    // Make sure the throw's parent is a binary expression (or a block that contains only the throw
                    // and whose parent is that binary expression).
                    IConditionalOperation? condition = throwOperation.Parent as IConditionalOperation;
                    if (condition is null)
                    {
                        if (throwOperation.Parent is IBlockOperation parentBlock && parentBlock.Children.Count() == 1)
                        {
                            condition = parentBlock.Parent as IConditionalOperation;
                        }

                        if (condition is null)
                        {
                            return;
                        }
                    }

                    // If the condition has an else block, give up.  These are rare.
                    if (condition.WhenFalse is not null)
                    {
                        return;
                    }

                    // Now match the exception type against one of our known types.

                    // Handle ArgumentNullException.ThrowIfNull.
                    if (SymbolEqualityComparer.Default.Equals(objectCreationOperation.Type, ane))
                    {
                        if (aneThrowIfNull is not null &&
                            IsParameterNullCheck(condition.Condition, out IParameterReferenceOperation? nullCheckParameter) &&
                            nullCheckParameter.Type!.IsReferenceType &&
                            HasReplaceableArgumentName(objectCreationOperation, 0))
                        {
                            context.ReportDiagnostic(condition.CreateDiagnostic(
                                UseArgumentNullExceptionThrowIfNullRule,
                                additionalLocations: ImmutableArray.Create(nullCheckParameter.Syntax.GetLocation()),
                                properties: null,
                                args: new object[] { nameof(ArgumentNullException), "ThrowIfNull" }));
                        }

                        return;
                    }

                    // Handle ArgumentException.ThrowIfNullOrEmpty
                    if (SymbolEqualityComparer.Default.Equals(objectCreationOperation.Type, ae))
                    {
                        if (aeThrowIfNullOrEmpty is not null &&
                            IsNullOrEmptyCheck(stringIsNullOrEmpty, stringLength, stringEmpty, condition.Condition, out IParameterReferenceOperation? nullOrEmptyCheckParameter) &&
                            HasReplaceableArgumentName(objectCreationOperation, 1))
                        {
                            context.ReportDiagnostic(condition.CreateDiagnostic(
                                UseArgumentExceptionThrowIfNullOrEmptyRule,
                                additionalLocations: ImmutableArray.Create(nullOrEmptyCheckParameter.Syntax.GetLocation()),
                                properties: null,
                                args: new object[] { nameof(ArgumentException), "ThrowIfNullOrEmpty" }));
                        }

                        return;
                    }

                    // Handle ArgumentOutOfRangeException.ThrowIfZero
                    // Handle ArgumentOutOfRangeException.ThrowIfNegative
                    // Handle ArgumentOutOfRangeException.ThrowIfNegativeOrZero
                    // Handle ArgumentOutOfRangeException.ThrowIfGreaterThan
                    // Handle ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual
                    // Handle ArgumentOutOfRangeException.ThrowIfLessThan
                    // Handle ArgumentOutOfRangeException.ThrowIfLessThanOrEqual
                    // Handle ArgumentOutOfRangeException.ThrowIfEqual
                    // Handle ArgumentOutOfRangeException.ThrowIfNotEqual
                    if (SymbolEqualityComparer.Default.Equals(objectCreationOperation.Type, aoore))
                    {
                        if (hasAnyAooreThrow &&
                            HasReplaceableArgumentName(objectCreationOperation, 0))
                        {
                            ImmutableArray<Location> additionalLocations = ImmutableArray<Location>.Empty;

                            if (IsNegativeAndOrZeroComparison(condition.Condition, out IParameterReferenceOperation? aooreParameter, out string? methodName))
                            {
                                additionalLocations = ImmutableArray.Create(aooreParameter.Syntax.GetLocation());
                            }
                            else if (IsGreaterLessEqualThanComparison(condition.Condition, out aooreParameter, out methodName, out SyntaxNode? other))
                            {
                                additionalLocations = ImmutableArray.Create(aooreParameter.Syntax.GetLocation(), other.GetLocation());
                            }

                            if (additionalLocations.Length != 0 && !AvoidComparing(aooreParameter!))
                            {
                                switch (methodName)
                                {
                                    case "ThrowIfZero" when aooreThrowIfZero is not null:
                                    case "ThrowIfNegative" when aooreThrowIfNegative is not null:
                                    case "ThrowIfNegativeOrZero" when aooreThrowIfNegativeOrZero is not null:
                                    case "ThrowIfGreaterThan" when aooreThrowIfGreaterThan is not null:
                                    case "ThrowIfGreaterThanOrEqual" when aooreThrowIfGreaterThanOrEqual is not null:
                                    case "ThrowIfLessThan" when aooreThrowIfLessThan is not null:
                                    case "ThrowIfLessThanOrEqual" when aooreThrowIfLessThanOrEqual is not null:
                                    case "ThrowIfEqual" when aooreThrowIfEqual is not null:
                                    case "ThrowIfNotEqual" when aooreThrowIfNotEqual is not null:
                                        context.ReportDiagnostic(condition.CreateDiagnostic(
                                            UseArgumentOutOfRangeExceptionThrowIfRule,
                                            additionalLocations,
                                            properties: ImmutableDictionary<string, string?>.Empty.Add(MethodNamePropertyKey, methodName),
                                            args: new object[] { nameof(ArgumentOutOfRangeException), methodName! }));
                                        break;
                                }
                            }

                            static bool AvoidComparing(IParameterReferenceOperation p) =>
                                p.Type.IsNullableValueType() ||
                                p.Type?.TypeKind == TypeKind.Enum;
                        }

                        return;
                    }

                    // Handle ObjectDisposedException.ThrowIf.
                    if (SymbolEqualityComparer.Default.Equals(objectCreationOperation.Type, ode))
                    {
                        // If we have ObjectDisposedException.ThrowIf and if this operation is in a reference type, issue a diagnostic.
                        // We check whether the containing type is a reference type because we want to avoid passing `this` at the call
                        // site to ThrowIf for a struct as that will box, and we want to avoid using `GetType()` at the call site as
                        // that adds additional cost prior to the guard check.
                        if (odeThrowIf is not null &&
                            context.ContainingSymbol.ContainingType.IsReferenceType)
                        {
                            // We always report a diagnostic. However, the fixer is only currently provided in the case
                            // of the argument to the ObjectDisposedException constructor containing a call to {something.}GetType().{Full}Name,
                            // in which case we can use the "something" as the argument to ThrowIf.

                            ImmutableArray<Location> additionalLocations = ImmutableArray.Create(condition.Condition.Syntax.GetLocation());

                            if (objectCreationOperation.Arguments is [IArgumentOperation arg, ..] &&
                                arg.Value is IPropertyReferenceOperation nameReference &&
                                nameReference.Member.Name is "Name" or "FullName" &&
                                nameReference.Instance is IInvocationOperation getTypeCall &&
                                SymbolEqualityComparer.Default.Equals(getType, getTypeCall.TargetMethod))
                            {
                                additionalLocations = additionalLocations.Add(
                                    getTypeCall.Instance is IInstanceReferenceOperation { IsImplicit: true } ?
                                        Location.None :
                                        getTypeCall.Instance!.Syntax.GetLocation());
                            }

                            context.ReportDiagnostic(condition.CreateDiagnostic(
                                UseObjectDisposedExceptionThrowIfRule,
                                additionalLocations,
                                properties: null,
                                args: new object[] { nameof(ObjectDisposedException), "ThrowIf" }));
                        }

                        return;
                    }
                }, OperationKind.Throw);

                // As a heuristic, we only want to replace throws with ThrowIfNull if either there isn't currently
                // a specified parameter name, e.g. the parameterless constructor was used, or if it's specified as a
                // constant, e.g. a nameof or a literal string.  This is primarily to avoid false positives
                // with complicated expressions for computing the parameter name to use, which with ThrowIfNull would
                // need to be done prior to the guard check, and thus something we want to avoid.
                bool HasReplaceableArgumentName(IObjectCreationOperation creationOperation, int argumentIndex)
                {
                    ImmutableArray<IArgumentOperation> args = creationOperation.Arguments;
                    return
                        argumentIndex >= args.Length ||
                        args.GetArgumentForParameterAtIndex(argumentIndex).Value.ConstantValue.HasValue;
                }

                // As a heuristic, we avoid issuing diagnostics if there are additional arguments (e.g. message)
                // to the exception that could be useful.
                bool HasPossiblyMeaningfulAdditionalArguments(IObjectCreationOperation objectCreationOperation)
                {
                    ImmutableArray<IArgumentOperation> args = objectCreationOperation.Arguments;

                    if (args.IsEmpty)
                    {
                        // No arguments, so nothing meaningful.
                        return false;
                    }

                    if (args.Length >= 3)
                    {
                        // More than just parameter name and message/inner exception.
                        // It'll be rare for those to be default values, so just assume
                        // it's meaningful at this point.
                        return true;
                    }

                    if (SymbolEqualityComparer.Default.Equals(objectCreationOperation.Type, ae))
                    {
                        // ArgumentException's message is first. If it's not null nor "", it's "meaningful".
                        return !IsNullOrEmptyMessage(args[0]);
                    }

                    // The other exceptions all have the message second. If they only have 0 or 1 arguments,
                    // there's no message, and if they have more than one, it's again "meaningful" if it's not null nor "".
                    return args.Length >= 2 && !IsNullOrEmptyMessage(args[1]);

                    static bool IsNullOrEmptyMessage(IArgumentOperation arg) =>
                        arg.Value.WalkDownConversion() is ILiteralOperation { ConstantValue.HasValue: true } literal &&
                        literal.ConstantValue.Value is null or "";
                }
            });
        }

        /// <summary>Gets the <see cref="IParameterReferenceOperation"/> being null checked by the condition, or else null.</summary>
        private static bool IsParameterNullCheck(IOperation condition, [NotNullWhen(true)] out IParameterReferenceOperation? parameterReference)
        {
            parameterReference = null;

            if (condition is IIsPatternOperation isPattern &&
                isPattern.Pattern is IConstantPatternOperation { Value.ConstantValue: { HasValue: true, Value: null } })
            {
                // arg is null
                parameterReference = isPattern.Value as IParameterReferenceOperation;
            }
            else if (condition is IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals } equalsOp)
            {
                if (equalsOp.RightOperand.HasNullConstantValue())
                {
                    // arg == null
                    parameterReference = equalsOp.LeftOperand.WalkDownConversion() as IParameterReferenceOperation;
                }
                else if (equalsOp.LeftOperand.HasNullConstantValue())
                {
                    // null == arg
                    parameterReference = equalsOp.RightOperand.WalkDownConversion() as IParameterReferenceOperation;
                }
            }

            return parameterReference is not null;
        }

        /// <summary>Gets the string <see cref="IParameterReferenceOperation"/> being checked by the condition for being null or empty, or else null.</summary>
        private static bool IsNullOrEmptyCheck(ISymbol stringIsNullOrEmpty, ISymbol stringLength, ISymbol stringEmpty, IOperation condition, [NotNullWhen(true)] out IParameterReferenceOperation? parameterReference)
        {
            if (condition is IInvocationOperation invocationOperation)
            {
                // (string.IsNullOrEmpty(arg))
                if (SymbolEqualityComparer.Default.Equals(invocationOperation.TargetMethod, stringIsNullOrEmpty) &&
                    invocationOperation.Arguments is [IArgumentOperation arg] &&
                    arg.Value is IParameterReferenceOperation parameterReferenceOperation)
                {
                    parameterReference = parameterReferenceOperation;
                    return true;
                }
            }
            else if (condition is IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalOr } orOp &&
                IsParameterNullCheck(orOp.LeftOperand, out IParameterReferenceOperation? nullCheckParameter) &&
                orOp.RightOperand is IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals } lengthCheckOperation)
            {
                // arg is null ||

                // arg.Length == 0
                if (IsArgLengthEqual0(stringLength, nullCheckParameter.Parameter, lengthCheckOperation.LeftOperand, lengthCheckOperation.RightOperand) ||
                    IsArgLengthEqual0(stringLength, nullCheckParameter.Parameter, lengthCheckOperation.RightOperand, lengthCheckOperation.LeftOperand))
                {
                    parameterReference = nullCheckParameter;
                    return true;
                }

                // arg == string.Empty
                if (IsArgEqualStringEmpty(stringEmpty, nullCheckParameter.Parameter, lengthCheckOperation.LeftOperand, lengthCheckOperation.RightOperand) ||
                    IsArgEqualStringEmpty(stringEmpty, nullCheckParameter.Parameter, lengthCheckOperation.RightOperand, lengthCheckOperation.LeftOperand))
                {
                    parameterReference = nullCheckParameter;
                    return true;
                }
            }

            parameterReference = null;
            return false;
        }

        /// <summary>Determines whether the left and right operations are performing a comparison of the specified argument against string.Empty.</summary>
        private static bool IsArgEqualStringEmpty(ISymbol stringEmpty, IParameterSymbol arg, IOperation left, IOperation right) =>
            left is IParameterReferenceOperation parameterReferenceOperation &&
            SymbolEqualityComparer.Default.Equals(parameterReferenceOperation.Parameter, arg) &&
            parameterReferenceOperation.Type?.SpecialType == SpecialType.System_String &&
            right is IFieldReferenceOperation fieldReferenceOperation &&
            SymbolEqualityComparer.Default.Equals(stringEmpty, fieldReferenceOperation.Member);

        /// <summary>Determines whether the left and right operations are performing a comparison of the specified argument's Length against 0.</summary>
        private static bool IsArgLengthEqual0(ISymbol stringLength, IParameterSymbol arg, IOperation left, IOperation right) =>
            right.WalkDownConversion() is ILiteralOperation literalOperation &&
            literalOperation.ConstantValue is { HasValue: true, Value: 0 } &&
            left is IPropertyReferenceOperation propertyReferenceOperation &&
            propertyReferenceOperation.Instance is IParameterReferenceOperation referencedParameter &&
            SymbolEqualityComparer.Default.Equals(referencedParameter.Parameter, arg) &&
            SymbolEqualityComparer.Default.Equals(stringLength, propertyReferenceOperation.Member);

        /// <summary>Gets the <see cref="IParameterReferenceOperation"/> being compared for being negative and/or zero.</summary>
        private static bool IsNegativeAndOrZeroComparison(IOperation condition, [NotNullWhen(true)] out IParameterReferenceOperation? parameterReferenceOperation, [NotNullWhen(true)] out string? methodName)
        {
            const string ThrowIfZero = nameof(ThrowIfZero);
            const string ThrowIfNegative = nameof(ThrowIfNegative);
            const string ThrowIfNegativeOrZero = nameof(ThrowIfNegativeOrZero);

            if (condition is IIsPatternOperation patternOperation &&
                patternOperation.Pattern is IConstantPatternOperation { Value.ConstantValue: { HasValue: true, Value: 0 } })
            {
                // arg is 0
                methodName = ThrowIfZero;
                parameterReferenceOperation = patternOperation.Value as IParameterReferenceOperation;
                return parameterReferenceOperation is not null;
            }

            // TODO: Update to include IRelationalPatternOperation when it's available:
            //     arg is < 0
            //     arg is <= 0

            if (condition is IBinaryOperation binaryOperation)
            {
                switch (binaryOperation.OperatorKind)
                {
                    case BinaryOperatorKind.Equals:
                        if (binaryOperation.LeftOperand.WalkDownConversion() is ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } })
                        {
                            // arg == 0
                            methodName = ThrowIfZero;
                            parameterReferenceOperation = binaryOperation.RightOperand as IParameterReferenceOperation;
                            return parameterReferenceOperation is not null;
                        }

                        if (binaryOperation.RightOperand.WalkDownConversion() is ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } })
                        {
                            // 0 == arg
                            methodName = ThrowIfZero;
                            parameterReferenceOperation = binaryOperation.LeftOperand as IParameterReferenceOperation;
                            return parameterReferenceOperation is not null;
                        }

                        break;

                    case BinaryOperatorKind.LessThanOrEqual or BinaryOperatorKind.LessThan:
                        if (binaryOperation.LeftOperand is IParameterReferenceOperation leftOperandParameterReference &&
                            binaryOperation.RightOperand.WalkDownConversion() is ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } })
                        {
                            // arg < 0
                            // arg <= 0
                            methodName = binaryOperation.OperatorKind == BinaryOperatorKind.LessThanOrEqual ? ThrowIfNegativeOrZero : ThrowIfNegative;
                            parameterReferenceOperation = leftOperandParameterReference;
                            return true;
                        }

                        break;

                    case BinaryOperatorKind.GreaterThanOrEqual or BinaryOperatorKind.GreaterThan:
                        if (binaryOperation.RightOperand is IParameterReferenceOperation rightOperationParameterReference &&
                            binaryOperation.LeftOperand.WalkDownConversion() is ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } })
                        {
                            // 0 > arg
                            // 0 >= arg
                            methodName = binaryOperation.OperatorKind == BinaryOperatorKind.GreaterThanOrEqual ? ThrowIfNegativeOrZero : ThrowIfNegative;
                            parameterReferenceOperation = rightOperationParameterReference;
                            return true;
                        }

                        break;
                }
            }

            methodName = null;
            parameterReferenceOperation = null;
            return false;
        }

        /// <summary>Gets the <see cref="IParameterReferenceOperation"/> being compared to another expression.</summary>
        private static bool IsGreaterLessEqualThanComparison(IOperation condition, [NotNullWhen(true)] out IParameterReferenceOperation? parameterReferenceOperation, [NotNullWhen(true)] out string? methodName, [NotNullWhen(true)] out SyntaxNode? other)
        {
            const string ThrowIfGreaterThan = nameof(ThrowIfGreaterThan);
            const string ThrowIfGreaterThanOrEqual = nameof(ThrowIfGreaterThanOrEqual);
            const string ThrowIfLessThan = nameof(ThrowIfLessThan);
            const string ThrowIfLessThanOrEqual = nameof(ThrowIfLessThanOrEqual);
            const string ThrowIfEqual = nameof(ThrowIfEqual);
            const string ThrowIfNotEqual = nameof(ThrowIfNotEqual);

            if (condition is IBinaryOperation binaryOperation)
            {
                switch (binaryOperation.OperatorKind)
                {
                    case BinaryOperatorKind.GreaterThan or BinaryOperatorKind.GreaterThanOrEqual or BinaryOperatorKind.LessThan or BinaryOperatorKind.LessThanOrEqual or BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals:
                        if (binaryOperation.LeftOperand is IParameterReferenceOperation leftParameter)
                        {
                            // arg > other
                            // arg >= other
                            // arg < other
                            // arg <= other
                            // arg == other
                            // arg != other
                            methodName = binaryOperation.OperatorKind switch
                            {
                                BinaryOperatorKind.GreaterThan => ThrowIfGreaterThan,
                                BinaryOperatorKind.GreaterThanOrEqual => ThrowIfGreaterThanOrEqual,
                                BinaryOperatorKind.LessThan => ThrowIfLessThan,
                                BinaryOperatorKind.LessThanOrEqual => ThrowIfLessThanOrEqual,
                                BinaryOperatorKind.Equals => ThrowIfEqual,
                                _ => ThrowIfNotEqual
                            };
                            other = binaryOperation.RightOperand.Syntax;
                            parameterReferenceOperation = leftParameter;
                            return true;
                        }

                        if (binaryOperation.RightOperand is IParameterReferenceOperation rightParameter)
                        {
                            // other > arg
                            // other >= arg
                            // other < arg
                            // other <= arg
                            // other == arg
                            // other != arg
                            methodName = binaryOperation.OperatorKind switch
                            {
                                BinaryOperatorKind.GreaterThan => ThrowIfLessThan,
                                BinaryOperatorKind.GreaterThanOrEqual => ThrowIfLessThanOrEqual,
                                BinaryOperatorKind.LessThan => ThrowIfGreaterThan,
                                BinaryOperatorKind.LessThanOrEqual => ThrowIfGreaterThanOrEqual,
                                BinaryOperatorKind.Equals => ThrowIfEqual,
                                _ => ThrowIfNotEqual
                            };
                            other = binaryOperation.LeftOperand.Syntax;
                            parameterReferenceOperation = rightParameter;
                            return true;
                        }

                        break;
                }
            }

            methodName = null;
            parameterReferenceOperation = null;
            other = null;
            return false;
        }
    }
}