// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA2201: Do not raise reserved exception types
    ///
    /// Too generic:
    ///     System.Exception
    ///     System.ApplicationException
    ///     System.SystemException
    ///
    /// Reserved:
    ///     System.OutOfMemoryException
    ///     System.IndexOutOfRangeException
    ///     System.ExecutionEngineException
    ///     System.NullReferenceException
    ///     System.StackOverflowException
    ///     System.Runtime.InteropServices.ExternalException
    ///     System.Runtime.InteropServices.COMException
    ///     System.Runtime.InteropServices.SEHException
    ///     System.AccessViolationException
    ///
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotRaiseReservedExceptionTypesAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2201";

        private static readonly ImmutableArray<string> s_tooGenericExceptions = ImmutableArray.Create("System.Exception",
                                                                                                      "System.ApplicationException",
                                                                                                      "System.SystemException");

        private static readonly ImmutableArray<string> s_reservedExceptions = ImmutableArray.Create("System.OutOfMemoryException",
                                                                                                    "System.IndexOutOfRangeException",
                                                                                                    "System.ExecutionEngineException",
                                                                                                    "System.NullReferenceException",
                                                                                                    "System.StackOverflowException",
                                                                                                    "System.Runtime.InteropServices.ExternalException",
                                                                                                    "System.Runtime.InteropServices.COMException",
                                                                                                    "System.Runtime.InteropServices.SEHException",
                                                                                                    "System.AccessViolationException");

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotRaiseReservedExceptionTypesTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageTooGeneric = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotRaiseReservedExceptionTypesMessageTooGeneric), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageReserved = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotRaiseReservedExceptionTypesMessageReserved), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotRaiseReservedExceptionTypesDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor TooGenericRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageTooGeneric,
                                                                             DiagnosticCategory.Usage,
                                                                             RuleLevel.IdeHidden_BulkConfigurable,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);
        internal static DiagnosticDescriptor ReservedRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageReserved,
                                                                             DiagnosticCategory.Usage,
                                                                             RuleLevel.IdeHidden_BulkConfigurable,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        private static readonly SymbolDisplayFormat s_symbolDisplayFormat = new(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(TooGenericRule, ReservedRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(
                compilationStartContext =>
                {
                    ImmutableHashSet<INamedTypeSymbol> tooGenericExceptionSymbols = CreateSymbolSet(compilationStartContext.Compilation, s_tooGenericExceptions);
                    ImmutableHashSet<INamedTypeSymbol> reservedExceptionSymbols = CreateSymbolSet(compilationStartContext.Compilation, s_reservedExceptions);

                    if (tooGenericExceptionSymbols.IsEmpty && reservedExceptionSymbols.IsEmpty)
                    {
                        return;
                    }

                    compilationStartContext.RegisterOperationAction(
                        context => AnalyzeObjectCreation(context, tooGenericExceptionSymbols, reservedExceptionSymbols),
                        OperationKind.ObjectCreation);
                });
        }

        private static ImmutableHashSet<INamedTypeSymbol> CreateSymbolSet(Compilation compilation, IEnumerable<string> exceptionNames)
        {
            HashSet<INamedTypeSymbol>? set = null;
            foreach (string exp in exceptionNames)
            {
                INamedTypeSymbol? symbol = compilation.GetOrCreateTypeByMetadataName(exp);
                if (symbol == null)
                {
                    continue;
                }
                if (set == null)
                {
                    set = new HashSet<INamedTypeSymbol>();
                }
                set.Add(symbol);
            }

            return set != null ? set.ToImmutableHashSet() : ImmutableHashSet<INamedTypeSymbol>.Empty;
        }

        private static void AnalyzeObjectCreation(
            OperationAnalysisContext context,
            ImmutableHashSet<INamedTypeSymbol> tooGenericExceptionSymbols,
            ImmutableHashSet<INamedTypeSymbol> reservedExceptionSymbols)
        {
            var objectCreation = (IObjectCreationOperation)context.Operation;
            var typeSymbol = objectCreation.Constructor.ContainingType;
            if (tooGenericExceptionSymbols.Contains(typeSymbol))
            {
                context.ReportDiagnostic(objectCreation.CreateDiagnostic(TooGenericRule, typeSymbol.ToDisplayString(s_symbolDisplayFormat)));
            }
            else if (reservedExceptionSymbols.Contains(typeSymbol))
            {
                context.ReportDiagnostic(objectCreation.CreateDiagnostic(ReservedRule, typeSymbol.ToDisplayString(s_symbolDisplayFormat)));
            }
        }
    }
}