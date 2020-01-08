// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetFramework.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    internal sealed class DoNotCatchCorruptedStateExceptionsAnalyzer : DoNotCatchGeneralUnlessRethrownAnalyzer
    {
        internal const string RuleId = "CA2153";
        private const string MethodAttributeTypeName = "System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetFrameworkAnalyzersResources.DoNotCatchCorruptedStateExceptions), MicrosoftNetFrameworkAnalyzersResources.ResourceManager, typeof(MicrosoftNetFrameworkAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftNetFrameworkAnalyzersResources.DoNotCatchCorruptedStateExceptionsMessage), MicrosoftNetFrameworkAnalyzersResources.ResourceManager, typeof(MicrosoftNetFrameworkAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetFrameworkAnalyzersResources.DoNotCatchCorruptedStateExceptionsDescription), MicrosoftNetFrameworkAnalyzersResources.ResourceManager, typeof(MicrosoftNetFrameworkAnalyzersResources));

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Security,
                                                                             DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                             isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                                                                             description: s_localizableDescription,
                                                                             helpLinkUri: "https://docs.microsoft.com/visualstudio/code-quality/ca2153-avoid-handling-corrupted-state-exceptions",
                                                                             customTags: WellKnownDiagnosticTags.Telemetry);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        // for now there doesn't seem to be any way to annotate lambdas with attributes, so there is no way for them to catch corrupted state exceptions
        public DoNotCatchCorruptedStateExceptionsAnalyzer() : base(shouldCheckLambdas: false, enablingMethodAttributeFullyQualifiedName: MethodAttributeTypeName)
        {
        }

        protected override Diagnostic CreateDiagnostic(IMethodSymbol containingMethod, SyntaxToken catchKeyword)
        {
            return catchKeyword.CreateDiagnostic(Rule, containingMethod.ToDisplayString());
        }
    }
}