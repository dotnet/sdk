// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotUseWeakKDFAlgorithm : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "CA5379";
        private static readonly LocalizableString s_Title = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseWeakKDFAlgorithm),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_Message = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseWeakKDFAlgorithmMessage),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_Description = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseWeakKDFAlgorithmDescription),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
                DiagnosticId,
                s_Title,
                s_Message,
                DiagnosticCategory.Security,
                RuleLevel.IdeHidden_BulkConfigurable,
                description: s_Description,
                isPortedFxCopRule: false,
                isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        private static readonly ImmutableHashSet<string> s_WeakHashAlgorithmNames = ImmutableHashSet.Create("MD5", "SHA1");

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
            {
                var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilationStartAnalysisContext.Compilation);

                if (!wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                    WellKnownTypeNames.SystemSecurityCryptographyRfc2898DeriveBytes,
                    out INamedTypeSymbol? rfc2898DeriveBytesTypeSymbol))
                {
                    return;
                }

                wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                    WellKnownTypeNames.SystemSecurityCryptographyHashAlgorithmName,
                    out INamedTypeSymbol? hashAlgorithmNameTypeSymbol);

                compilationStartAnalysisContext.RegisterOperationAction(operationAnalysisContext =>
                {
                    var objectCreationOperation = (IObjectCreationOperation)operationAnalysisContext.Operation;
                    var typeSymbol = objectCreationOperation.Constructor.ContainingType;
                    var baseTypeSymbol = typeSymbol;

                    while (!rfc2898DeriveBytesTypeSymbol.Equals(baseTypeSymbol))
                    {
                        baseTypeSymbol = baseTypeSymbol.BaseType;

                        if (baseTypeSymbol == null)
                        {
                            return;
                        }
                    }

                    var hashAlgorithmNameArgumentOperation = objectCreationOperation.Arguments.FirstOrDefault(s => s.Parameter.Name == "hashAlgorithm");

                    if (hashAlgorithmNameArgumentOperation != null)
                    {
                        if (hashAlgorithmNameArgumentOperation.Value is IPropertyReferenceOperation propertyReferenceOperation &&
                            !s_WeakHashAlgorithmNames.Contains(propertyReferenceOperation.Property.Name) &&
                            hashAlgorithmNameTypeSymbol != null &&
                            hashAlgorithmNameTypeSymbol.Equals(propertyReferenceOperation.Property.ContainingType))
                        {
                            return;
                        }
                    }

                    operationAnalysisContext.ReportDiagnostic(
                                objectCreationOperation.CreateDiagnostic(
                                    Rule,
                                    typeSymbol.Name));
                }, OperationKind.ObjectCreation);
            });
        }
    }
}
