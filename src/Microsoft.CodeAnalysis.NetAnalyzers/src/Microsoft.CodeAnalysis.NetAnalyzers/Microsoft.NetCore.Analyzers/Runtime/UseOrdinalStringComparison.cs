// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.NetAnalyzers;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    public abstract class UseOrdinalStringComparisonAnalyzer : AbstractGlobalizationDiagnosticAnalyzer
    {
        internal const string RuleId = "CA1309";

        private static readonly LocalizableString s_localizableMessageAndTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.UseOrdinalStringComparisonTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.UseOrdinalStringComparisonDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableMessageAndTitle,
                                                                             s_localizableMessageAndTitle,
                                                                             DiagnosticCategory.Globalization,
                                                                             RuleLevel.IdeHidden_BulkConfigurable,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        internal const string CompareMethodName = "Compare";
        internal const string EqualsMethodName = "Equals";
        internal const string OrdinalText = "Ordinal";
        internal const string OrdinalIgnoreCaseText = "OrdinalIgnoreCase";
        internal const string StringComparisonTypeName = "System.StringComparison";
        internal const string IgnoreCaseText = "IgnoreCase";

        protected abstract Location GetMethodNameLocation(SyntaxNode invocationNode);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        protected override void InitializeWorker(CompilationStartAnalysisContext context)
        {
            INamedTypeSymbol? stringComparisonType = context.Compilation.GetOrCreateTypeByMetadataName(StringComparisonTypeName);
            if (stringComparisonType != null)
            {
                context.RegisterOperationAction(operationContext =>
                {
                    var operation = (IInvocationOperation)operationContext.Operation;
                    IMethodSymbol methodSymbol = operation.TargetMethod;
                    if (methodSymbol != null &&
                        methodSymbol.ContainingType.SpecialType == SpecialType.System_String &&
                        IsEqualsOrCompare(methodSymbol.Name))
                    {
                        if (!IsAcceptableOverload(methodSymbol, stringComparisonType))
                        {
                            // wrong overload
                            operationContext.ReportDiagnostic(Diagnostic.Create(Rule, GetMethodNameLocation(operation.Syntax)));
                        }
                        else
                        {
                            IArgumentOperation lastArgument = operation.Arguments.Last();
                            if (lastArgument.Value.Kind == OperationKind.FieldReference)
                            {
                                IFieldSymbol fieldSymbol = ((IFieldReferenceOperation)lastArgument.Value).Field;
                                if (fieldSymbol != null &&
                                    fieldSymbol.ContainingType.Equals(stringComparisonType) &&
                                    !IsOrdinalOrOrdinalIgnoreCase(fieldSymbol.Name))
                                {
                                    // right overload, wrong value
                                    operationContext.ReportDiagnostic(lastArgument.Syntax.CreateDiagnostic(Rule));
                                }
                            }
                        }
                    }
                },
                    OperationKind.Invocation);
            }
        }

        private static bool IsEqualsOrCompare(string methodName)
        {
            return string.Equals(methodName, EqualsMethodName, StringComparison.Ordinal) ||
                string.Equals(methodName, CompareMethodName, StringComparison.Ordinal);
        }

        private static bool IsAcceptableOverload(IMethodSymbol methodSymbol, INamedTypeSymbol stringComparisonType)
        {
            return methodSymbol.IsStatic
                ? IsAcceptableStaticOverload(methodSymbol, stringComparisonType)
                : IsAcceptableInstanceOverload(methodSymbol, stringComparisonType);
        }

        private static bool IsAcceptableInstanceOverload(IMethodSymbol methodSymbol, INamedTypeSymbol stringComparisonType)
        {
            if (string.Equals(methodSymbol.Name, EqualsMethodName, StringComparison.Ordinal))
            {
                switch (methodSymbol.Parameters.Length)
                {
                    case 1:
                        // the instance method .Equals(object) is acceptable
                        return methodSymbol.Parameters[0].Type.SpecialType == SpecialType.System_Object;
                    case 2:
                        // .Equals(string, System.StringComparison) is acceptable
                        return methodSymbol.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                            methodSymbol.Parameters[1].Type.Equals(stringComparisonType);
                }
            }

            // all other overloads are unacceptable
            return false;
        }

        private static bool IsAcceptableStaticOverload(IMethodSymbol methodSymbol, INamedTypeSymbol stringComparisonType)
        {
            if (string.Equals(methodSymbol.Name, CompareMethodName, StringComparison.Ordinal))
            {
                switch (methodSymbol.Parameters.Length)
                {
                    case 3:
                        // (string, string, StringComparison) is acceptable
                        return methodSymbol.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                            methodSymbol.Parameters[1].Type.SpecialType == SpecialType.System_String &&
                            methodSymbol.Parameters[2].Type.Equals(stringComparisonType);
                    case 6:
                        // (string, int, string, int, int, StringComparison) is acceptable
                        return methodSymbol.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                            methodSymbol.Parameters[1].Type.SpecialType == SpecialType.System_Int32 &&
                            methodSymbol.Parameters[2].Type.SpecialType == SpecialType.System_String &&
                            methodSymbol.Parameters[3].Type.SpecialType == SpecialType.System_Int32 &&
                            methodSymbol.Parameters[4].Type.SpecialType == SpecialType.System_Int32 &&
                            methodSymbol.Parameters[5].Type.Equals(stringComparisonType);
                }
            }
            else if (string.Equals(methodSymbol.Name, EqualsMethodName, StringComparison.Ordinal))
            {
                switch (methodSymbol.Parameters.Length)
                {
                    case 2:
                        // (object, object) is acceptable
                        return methodSymbol.Parameters[0].Type.SpecialType == SpecialType.System_Object &&
                            methodSymbol.Parameters[1].Type.SpecialType == SpecialType.System_Object;
                    case 3:
                        // (string, string, StringComparison) is acceptable
                        return methodSymbol.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                            methodSymbol.Parameters[1].Type.SpecialType == SpecialType.System_String &&
                            methodSymbol.Parameters[2].Type.Equals(stringComparisonType);
                }
            }

            // all other overloads are unacceptable
            return false;
        }

        private static bool IsOrdinalOrOrdinalIgnoreCase(string name)
        {
            return string.Compare(name, OrdinalText, StringComparison.Ordinal) == 0 ||
                string.Compare(name, OrdinalIgnoreCaseText, StringComparison.Ordinal) == 0;
        }
    }
}
