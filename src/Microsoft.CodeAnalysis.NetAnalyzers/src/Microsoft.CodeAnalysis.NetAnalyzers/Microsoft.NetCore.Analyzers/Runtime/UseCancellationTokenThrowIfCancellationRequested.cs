// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Resx = Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseCancellationTokenThrowIfCancellationRequested : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2250";

        private static readonly LocalizableString s_localizableTitle = CreateResource(nameof(Resx.UseCancellationTokenThrowIfCancellationRequestedTitle));
        private static readonly LocalizableString s_localizableMessage = CreateResource(nameof(Resx.UseCancellationTokenThrowIfCancellationRequestedMessage));
        private static readonly LocalizableString s_localizableDescription = CreateResource(nameof(Resx.UseCancellationTokenThrowIfCancellationRequestedDescription));

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableMessage,
            DiagnosticCategory.Usage,
            RuleLevel.IdeSuggestion,
            s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
        }

        private static LocalizableString CreateResource(string resourceName)
        {
            return new LocalizableResourceString(resourceName, Resx.ResourceManager, typeof(Resx));
        }
    }
}
