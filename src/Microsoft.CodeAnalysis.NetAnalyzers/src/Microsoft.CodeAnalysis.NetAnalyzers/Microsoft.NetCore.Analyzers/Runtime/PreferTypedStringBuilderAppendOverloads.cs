// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>CA1830: Prefer strongly-typed StringBuilder.Append overloads.</summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PreferTypedStringBuilderAppendOverloads : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1830";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.PreferTypedStringBuilderAppendOverloadsTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.PreferTypedStringBuilderAppendOverloadsMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.PreferTypedStringBuilderAppendOverloadsDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(compilationContext =>
            {
                var typeProvider = WellKnownTypeProvider.GetOrCreate(compilationContext.Compilation);

                // Get the String and StringBuilder types.
                if (!typeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemTextStringBuilder, out var stringBuilderType))
                {
                    return;
                }

                // Get all of the one-arg StringBuilder.Append methods and two-arg Insert methods.  We skip over the object-based
                // overloads, as there's little benefit to removing such ToString calls.  And we skip over arrays, to avoid
                // differences of behavior between Append(char[]) and Append(char[].ToString()).
                var appendMethods = stringBuilderType
                    .GetMembers("Append")
                    .OfType<IMethodSymbol>()
                    .WhereAsArray(s =>
                        s.Parameters.Length == 1 &&
                        s.Parameters[0].Type.SpecialType != SpecialType.System_Object &&
                        s.Parameters[0].Type.TypeKind != TypeKind.Array);
                var insertMethods = stringBuilderType
                    .GetMembers("Insert")
                    .OfType<IMethodSymbol>()
                    .WhereAsArray(s =>
                        s.Parameters.Length == 2 &&
                        s.Parameters[0].Type.SpecialType == SpecialType.System_Int32 &&
                        s.Parameters[1].Type.SpecialType != SpecialType.System_Object &&
                        s.Parameters[1].Type.TypeKind != TypeKind.Array);

                // Get the StringBuilder.Append(string)/Insert(int, string) method, for comparison purposes.
                var appendStringMethod = appendMethods.FirstOrDefault(s =>
                    s.Parameters[0].Type.SpecialType == SpecialType.System_String);
                var insertStringMethod = insertMethods.FirstOrDefault(s =>
                    s.Parameters[0].Type.SpecialType == SpecialType.System_Int32 &&
                    s.Parameters[1].Type.SpecialType == SpecialType.System_String);
                if (appendStringMethod is null || insertStringMethod is null)
                {
                    return;
                }

                // Check every invocation to see if it's StringBuilder.Append(string)/Insert(int, string).
                compilationContext.RegisterOperationAction(operationContext =>
                {
                    var invocation = (IInvocationOperation)operationContext.Operation;

                    // We're interested in the string argument to Append(string) and Insert(int, string).
                    // Get the argument position, or bail if this isn't either method.
                    int stringParamIndex;
                    if (appendStringMethod.Equals(invocation.TargetMethod))
                    {
                        stringParamIndex = 0;
                    }
                    else if (insertStringMethod.Equals(invocation.TargetMethod))
                    {
                        stringParamIndex = 1;
                    }
                    else
                    {
                        return;
                    }

                    // We're only interested if the string argument is a "string ToString()" call.
                    if (invocation.Arguments.Length != stringParamIndex + 1 ||
                        invocation.Arguments[stringParamIndex] is not IArgumentOperation argument ||
                        argument.Value is not IInvocationOperation toStringInvoke ||
                        toStringInvoke.TargetMethod.Name != "ToString" ||
                        toStringInvoke.Type.SpecialType != SpecialType.System_String ||
                        !toStringInvoke.TargetMethod.Parameters.IsEmpty)
                    {
                        return;
                    }

                    // We're only interested if the receiver type of that ToString call has a corresponding strongly-typed overload.
                    IMethodSymbol? stronglyTypedAppend =
                        (stringParamIndex == 0 ? appendMethods : insertMethods)
                        .FirstOrDefault(s => s.Parameters[stringParamIndex].Type.Equals(toStringInvoke.TargetMethod.ReceiverType));
                    if (stronglyTypedAppend is null)
                    {
                        return;
                    }

                    // Warn.
                    operationContext.ReportDiagnostic(toStringInvoke.CreateDiagnostic(Rule));
                }, OperationKind.Invocation);
            });
        }
    }
}
