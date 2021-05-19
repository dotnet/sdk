// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Resx = Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseStringEqualsOverStringCompare : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2250";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(Resx.UseStringEqualsOverStringCompareTitle), Resx.ResourceManager, typeof(Resx));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(Resx.UseStringEqualsOverStringCompareMessage), Resx.ResourceManager, typeof(Resx));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(Resx.UseStringEqualsOverStringCompareDescription), Resx.ResourceManager, typeof(Resx));

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableMessage,
            DiagnosticCategory.Usage,
            RuleLevel.IdeHidden_BulkConfigurable,
            s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(context =>
            {
                if (!RequiredSymbols.TryGetSymbols(context.Compilation, out var symbols))
                    return;

                var selector = UseStringEqualsOverStringCompareFixer.GetOperationSelector(symbols);

                context.RegisterOperationAction(context =>
                {
                    if (selector(context.Operation))
                    {
                        var diagnostic = context.Operation.CreateDiagnostic(Rule);
                        context.ReportDiagnostic(diagnostic);
                    }
                }, OperationKind.Binary);
            });
        }

        internal sealed class RequiredSymbols
        {
            //  Named-constructor 'TryGetSymbols' inits all properties or doesn't construct an instance.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
            private RequiredSymbols() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

            public static bool TryGetSymbols(Compilation compilation, [NotNullWhen(true)] out RequiredSymbols? symbols)
            {
                symbols = default;

                var stringType = compilation.GetSpecialType(SpecialType.System_String);
                var boolType = compilation.GetSpecialType(SpecialType.System_Boolean);

                if (stringType is null || boolType is null)
                    return false;

                if (!compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemStringComparison, out var stringComparisonType))
                    return false;

                var compareMethods = stringType.GetMembers(nameof(string.Compare))
                    .OfType<IMethodSymbol>()
                    .Where(x => x.IsStatic);
                var compareStringString = compareMethods.GetFirstOrDefaultMemberWithParameterTypes(stringType, stringType);
                var compareStringStringBool = compareMethods.GetFirstOrDefaultMemberWithParameterTypes(stringType, stringType, boolType);
                var compareStringStringStringComparison = compareMethods.GetFirstOrDefaultMemberWithParameterTypes(stringType, stringType, stringComparisonType);

                if (compareStringString is null ||
                    compareStringStringBool is null ||
                    compareStringStringStringComparison is null)
                {
                    return false;
                }

                var equalsMethods = stringType.GetMembers(nameof(string.Equals))
                    .OfType<IMethodSymbol>()
                    .Where(x => x.IsStatic);
                var equalsStringString = equalsMethods.GetFirstOrDefaultMemberWithParameterTypes(stringType, stringType);
                var equalsStringStringStringComparison = equalsMethods.GetFirstOrDefaultMemberWithParameterTypes(stringType, stringType, stringComparisonType);

                if (equalsStringString is null ||
                    equalsStringStringStringComparison is null)
                {
                    return false;
                }

                symbols = new RequiredSymbols
                {
                    StringType = stringType,
                    BoolType = boolType,
                    StringComparisonType = stringComparisonType,
                    CompareStringString = compareStringString,
                    CompareStringStringBool = compareStringStringBool,
                    CompareStringStringStringComparison = compareStringStringStringComparison,
                    EqualsStringString = equalsStringString,
                    EqualsStringStringStringComparison = equalsStringStringStringComparison
                };
                return true;
            }

            public INamedTypeSymbol StringType { get; init; }
            public INamedTypeSymbol BoolType { get; init; }
            public INamedTypeSymbol StringComparisonType { get; init; }
            public IMethodSymbol CompareStringString { get; init; }
            public IMethodSymbol CompareStringStringBool { get; init; }
            public IMethodSymbol CompareStringStringStringComparison { get; init; }
            public IMethodSymbol EqualsStringString { get; init; }
            public IMethodSymbol EqualsStringStringStringComparison { get; init; }
        }
    }
}
