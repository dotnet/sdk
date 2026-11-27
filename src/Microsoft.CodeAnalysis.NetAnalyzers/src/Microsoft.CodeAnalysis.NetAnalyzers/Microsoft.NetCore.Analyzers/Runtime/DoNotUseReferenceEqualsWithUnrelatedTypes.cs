// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA2030: <inheritdoc cref="DoNotUseReferenceEqualsWithUnrelatedTypesTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotUseReferenceEqualsWithUnrelatedTypesAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2030";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(DoNotUseReferenceEqualsWithUnrelatedTypesTitle)),
            CreateLocalizableResourceString(nameof(DoNotUseReferenceEqualsWithUnrelatedTypesMessage)),
            DiagnosticCategory.Reliability,
            RuleLevel.BuildWarning,
            description: CreateLocalizableResourceString(nameof(DoNotUseReferenceEqualsWithUnrelatedTypesDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationStartContext =>
            {
                var compilation = compilationStartContext.Compilation;
                var objectType = compilation.GetSpecialType(SpecialType.System_Object);

                var referenceEqualsMethod = objectType.GetMembers("ReferenceEquals").OfType<IMethodSymbol>()
                    .GetFirstOrDefaultMemberWithParameterInfos(
                        ParameterInfo.GetParameterInfo(objectType),
                        ParameterInfo.GetParameterInfo(objectType));

                if (referenceEqualsMethod == null)
                {
                    return;
                }

                compilationStartContext.RegisterOperationAction(operationContext =>
                {
                    var invocation = (IInvocationOperation)operationContext.Operation;

                    if (!referenceEqualsMethod.Equals(invocation.TargetMethod, SymbolEqualityComparer.Default))
                    {
                        return;
                    }

                    var left = GetArgumentType(invocation.Arguments[0]);
                    var right = GetArgumentType(invocation.Arguments[1]);

                    if (left == null || right == null || IsRelated(compilation, left, right))
                    {
                        return;
                    }

                    operationContext.ReportDiagnostic(invocation.CreateDiagnostic(
                        Rule,
                        left.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                        right.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                },
                OperationKind.Invocation);
            });
        }

        private static ITypeSymbol? GetArgumentType(IArgumentOperation argument)
        {
            var type = argument.Value.WalkDownConversion(c => c.Conversion.IsIdentity || c.Conversion.IsReference).Type;
            return type != null && IsFullyKnown(type) ? type : null;
        }

        private static bool IsFullyKnown(ITypeSymbol type)
        {
            switch (type)
            {
                case ITypeParameterSymbol:
                    return false;
                case IArrayTypeSymbol array:
                    return IsFullyKnown(array.ElementType);
                case IPointerTypeSymbol pointer:
                    return IsFullyKnown(pointer.PointedAtType);
            }

            if (type.TypeKind == TypeKind.Error)
            {
                return false;
            }

            if (type is INamedTypeSymbol named)
            {
                for (var current = named; current != null; current = current.ContainingType)
                {
                    if (current.TypeArguments.Any(t => !IsFullyKnown(t)))
                    {
                        return false;
                    }
                }
            }

            for (var baseType = type.BaseType; baseType != null; baseType = baseType.BaseType)
            {
                if (baseType.TypeKind == TypeKind.Error)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsRelated(Compilation compilation, ITypeSymbol source, ITypeSymbol destination)
        {
            var conversion = compilation.ClassifyCommonConversion(source, destination);
            return conversion.Exists && !conversion.IsUserDefined;
        }
    }
}
