// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA1810: Initialize reference type static fields inline
    /// CA2207: Initialize value type static fields inline
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class InitializeStaticFieldsInlineAnalyzer : DiagnosticAnalyzer
    {
        internal const string CA1810RuleId = "CA1810";
        internal const string CA2207RuleId = "CA2207";

        private static readonly LocalizableString s_CA1810_LocalizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.InitializeReferenceTypeStaticFieldsInlineTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_CA2207_LocalizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.InitializeValueTypeStaticFieldsInlineTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.InitializeStaticFieldsInlineMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_CA1810_LocalizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.InitializeReferenceTypeStaticFieldsInlineDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_CA2207_LocalizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.InitializeValueTypeStaticFieldsInlineDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor CA1810Rule = DiagnosticDescriptorHelper.Create(CA1810RuleId,
                                                                             s_CA1810_LocalizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Performance,
                                                                             RuleLevel.Disabled,    // May tie this to performance sensitive attribute.
                                                                             description: s_CA1810_LocalizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        internal static DiagnosticDescriptor CA2207Rule = DiagnosticDescriptorHelper.Create(CA2207RuleId,
                                                                             s_CA2207_LocalizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Usage,
                                                                             RuleLevel.Disabled,    // May tie this to performance sensitive attribute.
                                                                             description: s_CA2207_LocalizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(CA1810Rule, CA2207Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterOperationBlockStartAction(context =>
            {
                if (context.OwningSymbol is not IMethodSymbol method ||
                    !method.IsStatic ||
                    method.MethodKind != MethodKind.StaticConstructor)
                {
                    return;
                }

                var isStaticCtorMandatory = new AtomicBoolean();
                var initializesStaticField = false;
                context.RegisterOperationAction(context =>
                {
                    var assignment = (IAssignmentOperation)context.Operation;

                    if (!isStaticCtorMandatory.Value &&
                        assignment.Target is IFieldReferenceOperation fieldReference &&
                        fieldReference.Member.IsStatic)
                    {
                        if (assignment.GetAncestor<IAnonymousFunctionOperation>(OperationKind.AnonymousFunction) != null)
                        {
                            isStaticCtorMandatory.Value = true;
                        }
                        else
                        {
                            initializesStaticField = true;
                        }
                    }
                }, OperationKind.SimpleAssignment);

                context.RegisterOperationAction(context =>
                {
                    isStaticCtorMandatory.Value = true;
                }, OperationKind.EventAssignment);

                context.RegisterOperationBlockEndAction(context =>
                {
                    if (initializesStaticField && !isStaticCtorMandatory.Value)
                    {
                        context.ReportDiagnostic(
                            method.CreateDiagnostic(
                                method.ContainingType.IsReferenceType ? CA1810Rule : CA2207Rule,
                                method.ContainingType.Name));
                    }
                });
            });
        }

        private class AtomicBoolean
        {
            private const int TRUE_VALUE = 1;
            private const int FALSE_VALUE = 0;
            private int zeroOrOne = FALSE_VALUE;

            public AtomicBoolean(bool initialValue = false)
            {
                zeroOrOne = initialValue ? TRUE_VALUE : FALSE_VALUE;
            }

            public bool Value
            {
                get => Interlocked.CompareExchange(ref zeroOrOne, TRUE_VALUE, TRUE_VALUE) == TRUE_VALUE;
                set
                {
                    if (value)
                    {
                        Interlocked.CompareExchange(ref zeroOrOne, TRUE_VALUE, FALSE_VALUE);
                    }
                    else
                    {
                        Interlocked.CompareExchange(ref zeroOrOne, FALSE_VALUE, TRUE_VALUE);
                    }
                }
            }
        }
    }
}