// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetFramework.Analyzers
{
    using static MicrosoftNetFrameworkAnalyzersResources;

    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    internal sealed class DoNotCatchCorruptedStateExceptionsAnalyzer : DoNotCatchGeneralUnlessRethrownAnalyzer
    {
        internal const string RuleId = "CA2153";
        private const string MethodAttributeTypeName = "System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
             CreateLocalizableResourceString(nameof(DoNotCatchCorruptedStateExceptions)),
             CreateLocalizableResourceString(nameof(DoNotCatchCorruptedStateExceptionsMessage)),
             DiagnosticCategory.Security,
             RuleLevel.CandidateForRemoval,     // Need confirmation from security team if this is no longer a security concern
             description: CreateLocalizableResourceString(nameof(DoNotCatchCorruptedStateExceptionsDescription)),
             isPortedFxCopRule: false,
             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

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