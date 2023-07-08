// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1001: <inheritdoc cref="TypesThatOwnDisposableFieldsShouldBeDisposableTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class TypesThatOwnDisposableFieldsShouldBeDisposableAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1001";
        internal const string Dispose = "Dispose";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(TypesThatOwnDisposableFieldsShouldBeDisposableTitle)),
            CreateLocalizableResourceString(nameof(TypesThatOwnDisposableFieldsShouldBeDisposableMessageNonBreaking)),
            DiagnosticCategory.Design,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: CreateLocalizableResourceString(nameof(TypesThatOwnDisposableFieldsShouldBeDisposableDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        private readonly List<string> _disposableFieldNames = new();

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationContext =>
            {
                var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilationContext.Compilation);
                if (!wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIDisposable, out _)
                    || !DisposeAnalysisHelper.TryGetOrCreate(compilationContext.Compilation, out var disposeAnalysisHelper))
                {
                    return;
                }

                compilationContext.RegisterSymbolStartAction(ctx => AnalyzeSymbolStart(ctx, disposeAnalysisHelper), SymbolKind.NamedType);
            });
        }

        private void AnalyzeSymbolStart(SymbolStartAnalysisContext ctx, DisposeAnalysisHelper disposeAnalysisHelper)
        {
            INamedTypeSymbol namedType = (INamedTypeSymbol)ctx.Symbol;
            if (disposeAnalysisHelper.IsDisposable(namedType))
            {
                return;
            }

            var disposableFields = namedType
                .GetMembers()
                .OfType<IFieldSymbol>()
                .Where(f => !f.IsStatic && disposeAnalysisHelper.IsDisposable(f.Type))
                .ToSet();
            if (disposableFields.Count == 0)
            {
                return;
            }

            ctx.RegisterOperationAction(context => AnalyzeOperation(context, namedType, disposableFields), OperationKind.SimpleAssignment, OperationKind.FieldInitializer);
            ctx.RegisterSymbolEndAction(AnalyzeSymbolEnd);
        }

        private void AnalyzeOperation(OperationAnalysisContext ctx, INamedTypeSymbol parent, ISet<IFieldSymbol> disposableFields)
        {
            if (ctx.Operation is IAssignmentOperation { Target: IFieldReferenceOperation field } assignment
                && IsObjectCreation(assignment.Value)
                && !ctx.Options.IsConfiguredToSkipAnalysis(Rule, field.Field.Type, parent, ctx.Compilation)
                && disposableFields.Contains(field.Field))
            {
                _disposableFieldNames.Add(field.Field.Name);
            }
            else if (ctx.Operation is IFieldInitializerOperation initializer && IsObjectCreation(initializer.Value))
            {
                var candidateFields = initializer.InitializedFields
                    .Where(f => !ctx.Options.IsConfiguredToSkipAnalysis(Rule, f.Type, parent, ctx.Compilation))
                    .Intersect(disposableFields);
                foreach (var f in candidateFields)
                {
                    _disposableFieldNames.Add(f.Name);
                }
            }
        }

        private static bool IsObjectCreation(IOperation op)
        {
            if (op is IConversionOperation conversion)
            {
                return conversion.Operand.Kind == OperationKind.ObjectCreation;
            }

            return op.Kind == OperationKind.ObjectCreation;
        }

        private void AnalyzeSymbolEnd(SymbolAnalysisContext ctx)
        {
            if (_disposableFieldNames.Count > 0)
            {
                _disposableFieldNames.Sort();
                // Type '{0}' owns disposable field(s) '{1}' but is not disposable
                ctx.ReportDiagnostic(ctx.Symbol.CreateDiagnostic(Rule, ctx.Symbol.Name, string.Join("', '", _disposableFieldNames)));
            }
        }
    }
}