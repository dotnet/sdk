// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.NetFramework.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetFramework.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotUseInsecureXSLTScriptExecutionAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA3076";

        internal static DiagnosticDescriptor RuleDoNotUseInsecureXSLTScriptExecution =
            DiagnosticDescriptorHelper.Create(
                RuleId,
                SecurityDiagnosticHelpers.GetLocalizableResourceString(nameof(MicrosoftNetFrameworkAnalyzersResources.InsecureXsltScriptProcessingMessage)),
                SecurityDiagnosticHelpers.GetLocalizableResourceString(nameof(MicrosoftNetFrameworkAnalyzersResources.DoNotUseInsecureDtdProcessingGenericMessage)),
                DiagnosticCategory.Security,
                RuleLevel.IdeHidden_BulkConfigurable,
                SecurityDiagnosticHelpers.GetLocalizableResourceString(nameof(MicrosoftNetFrameworkAnalyzersResources.DoNotUseInsecureXSLTScriptExecutionDescription)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(RuleDoNotUseInsecureXSLTScriptExecution);

#pragma warning disable RS1026 // Enable concurrent execution
        public override void Initialize(AnalysisContext context)
#pragma warning restore RS1026 // Enable concurrent execution
        {
            //analysisContext.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics in generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(
                context =>
                {
                    Compilation compilation = context.Compilation;
                    var xmlTypes = new CompilationSecurityTypes(compilation);
                    if (xmlTypes.XslCompiledTransform == null)
                    {
                        return;
                    }

                    context.RegisterOperationBlockStartAction(context =>
                    {
                        var analyzer = new OperationBlockAnalyzer(xmlTypes, context.OwningSymbol);

                        context.RegisterOperationAction(context =>
                        {
                            var assignment = (IAssignmentOperation)context.Operation;
                            analyzer.AnalyzeNodeForXsltSettings(assignment.Target, assignment.Value);
                        }, OperationKind.SimpleAssignment);

                        context.RegisterOperationAction(context =>
                        {
                            var variableDeclarator = (IVariableDeclaratorOperation)context.Operation;
                            if (variableDeclarator.GetVariableInitializer() is IVariableInitializerOperation initializer)
                            {
                                analyzer.AnalyzeNodeForXsltSettings(variableDeclarator, initializer.Value);
                            }
                        }, OperationKind.VariableDeclarator);

                        context.RegisterOperationAction(context =>
                        {
                            var invocation = (IInvocationOperation)context.Operation;
                            analyzer.AnalyzeNodeForXslCompiledTransformLoad(invocation.TargetMethod, invocation.Arguments,
                                context.ReportDiagnostic);
                        }, OperationKind.Invocation);

                        context.RegisterOperationAction(context =>
                        {
                            var objectCreation = (IObjectCreationOperation)context.Operation;
                            analyzer.AnalyzeNodeForXslCompiledTransformLoad(
                                objectCreation.Constructor, objectCreation.Arguments, context.ReportDiagnostic);
                        }, OperationKind.ObjectCreation);
                    });
                });
        }

        private sealed class OperationBlockAnalyzer
        {
            private readonly CompilationSecurityTypes _xmlTypes;
            private readonly ISymbol _enclosingSymbol;

            private readonly ConcurrentDictionary<ISymbol, XsltSettingsEnvironment> _xsltSettingsEnvironments = new();

            public OperationBlockAnalyzer(CompilationSecurityTypes xmlTypes, ISymbol enclosingSymbol)
            {
                _xmlTypes = xmlTypes;
                _enclosingSymbol = enclosingSymbol;
            }

            public void AnalyzeNodeForXslCompiledTransformLoad(IMethodSymbol methodSymbol,
                ImmutableArray<IArgumentOperation> arguments, Action<Diagnostic> reportDiagnostic)
            {
                if (!methodSymbol.IsXslCompiledTransformLoad(_xmlTypes))
                {
                    return;
                }

                int xmlResolverIndex = methodSymbol.GetXmlResolverParameterIndex(_xmlTypes);
                int xsltSettingsIndex = methodSymbol.GetXsltSettingsParameterIndex(_xmlTypes);

                // Overloads with no XmlResolver and XstlSettings specified are secure since they all have
                // following behavior:
                //  1. An XmlUrlResolver with no user credentials is used to process any xsl:import
                //     or xsl:include elements.
                //  2. The document() function is disabled.
                //  3. Embedded scripts are not supported.
                if (xmlResolverIndex < 0 || xsltSettingsIndex < 0)
                {
                    return;
                }

                var resolverArgumentOperation = arguments[xmlResolverIndex].Value;
                if (resolverArgumentOperation is IConversionOperation conversion)
                {
                    resolverArgumentOperation = conversion.Operand;
                }

                var isSecureResolver = resolverArgumentOperation.HasNullConstantValue()
                    || resolverArgumentOperation.Type.IsXmlSecureResolverType(_xmlTypes);

                var settingsOperation = arguments[xsltSettingsIndex].Value;
                var settingsSymbol = settingsOperation.GetReferencedMemberOrLocalOrParameter();

                bool isSecureSettings;
                bool isSetInBlock;

                // 1. pass null or XsltSettings.Default as XsltSetting : secure
                if (settingsSymbol is null || (settingsSymbol as IPropertySymbol).IsXsltSettingsDefaultProperty(_xmlTypes))
                {
                    isSetInBlock = true;
                    isSecureSettings = true;
                }
                // 2. XsltSettings.TrustedXslt : insecure
                else if ((settingsSymbol as IPropertySymbol).IsXsltSettingsTrustedXsltProperty(_xmlTypes))
                {
                    isSetInBlock = true;
                    isSecureSettings = false;
                }
                // 3. check xsltSettingsEnvironments, if IsScriptDisabled && IsDocumentFunctionDisabled then secure,
                //    else insecure
                else if (_xsltSettingsEnvironments.TryGetValue(settingsSymbol, out XsltSettingsEnvironment env))
                {
                    isSetInBlock = false;
                    isSecureSettings = env.IsDocumentFunctionDisabled && env.IsScriptDisabled;
                }
                //4. symbol for settings is not found => passed in without any change => assume insecure
                else
                {
                    isSetInBlock = true;
                    isSecureSettings = false;
                }

                if (!isSecureSettings && !isSecureResolver)
                {
                    LocalizableResourceString message = SecurityDiagnosticHelpers.GetLocalizableResourceString(
                        isSetInBlock
                            ? nameof(MicrosoftNetFrameworkAnalyzersResources.XslCompiledTransformLoadInsecureConstructedMessage)
                            : nameof(MicrosoftNetFrameworkAnalyzersResources.XslCompiledTransformLoadInsecureInputMessage),
                        SecurityDiagnosticHelpers.GetNonEmptyParentName(_enclosingSymbol)
                    );

                    var invocationOrObjCreation = settingsOperation.Parent.Parent;
                    RoslynDebug.Assert(invocationOrObjCreation.Kind is OperationKind.Invocation
                        or OperationKind.ObjectCreation);
                    reportDiagnostic(
                        invocationOrObjCreation.CreateDiagnostic(RuleDoNotUseInsecureXSLTScriptExecution, message));
                }
            }

            public void AnalyzeNodeForXsltSettings(IOperation lhs, IOperation rhs)
            {
                var lhsSymbol = lhs.Kind switch
                {
                    OperationKind.VariableDeclarator => ((IVariableDeclaratorOperation)lhs).Symbol,
                    _ => lhs.GetReferencedMemberOrLocalOrParameter(),
                };
                if (lhsSymbol is null)
                {
                    return;
                }

                IMethodSymbol? rhsMethodSymbol = rhs.Kind switch
                {
                    OperationKind.Invocation => ((IInvocationOperation)rhs).TargetMethod,
                    OperationKind.ObjectCreation => ((IObjectCreationOperation)rhs).Constructor,
                    _ => null,
                };
                IPropertySymbol? rhsPropertySymbol = (rhs as IPropertyReferenceOperation)?.Property;

                if (rhsMethodSymbol.IsXsltSettingsCtor(_xmlTypes))
                {
                    XsltSettingsEnvironment env = new XsltSettingsEnvironment();
                    _xsltSettingsEnvironments[lhsSymbol] = env;

                    env.XsltSettingsSymbol = lhsSymbol;
                    env.XsltSettingsDefinitionSymbol = rhsMethodSymbol;
                    env.XsltSettingsDefinition = lhs.Parent;
                    env.EnclosingConstructSymbol = _enclosingSymbol;
                    // default both properties are disabled
                    env.IsDocumentFunctionDisabled = true;
                    env.IsScriptDisabled = true;

                    // XsltSettings Constructor (Boolean, Boolean)
                    if (!rhsMethodSymbol.Parameters.IsEmpty)
                    {
                        var arguments = rhs.Kind switch
                        {
                            OperationKind.Invocation => ((IInvocationOperation)rhs).Arguments,
                            OperationKind.ObjectCreation => ((IObjectCreationOperation)rhs).Arguments,
                            _ => ImmutableArray<IArgumentOperation>.Empty,
                        };
                        env.IsDocumentFunctionDisabled = arguments[0].Value.TryGetBoolConstantValue(out var value)
                            && !value;
                        env.IsScriptDisabled = arguments[1].Value.TryGetBoolConstantValue(out value) && !value;
                    }

                    var initializers = (rhs as IObjectCreationOperation)?.Initializer?.Initializers
                        ?? ImmutableArray<IOperation>.Empty;
                    foreach (var arg in initializers)
                    {
                        if (arg is not ISimpleAssignmentOperation assignment)
                        {
                            continue;
                        }

                        var argLhsPropertySymbol = (assignment.Target as IPropertyReferenceOperation)?.Property;
                        var argRhs = assignment.Value;

                        // anything other than a constant false is treated as true
                        if (argLhsPropertySymbol.IsXsltSettingsEnableDocumentFunctionProperty(_xmlTypes))
                        {
                            env.IsDocumentFunctionDisabled = argRhs.TryGetBoolConstantValue(out var value) && !value;
                        }
                        else if (argLhsPropertySymbol.IsXsltSettingsEnableScriptProperty(_xmlTypes))
                        {
                            env.IsScriptDisabled = argRhs.TryGetBoolConstantValue(out var value) && !value;
                        }
                    }
                }
                else if (rhsPropertySymbol.IsXsltSettingsDefaultProperty(_xmlTypes))
                {
                    XsltSettingsEnvironment env = new XsltSettingsEnvironment();
                    _xsltSettingsEnvironments[lhsSymbol] = env;

                    env.XsltSettingsSymbol = lhsSymbol;
                    env.XsltSettingsDefinitionSymbol = rhsPropertySymbol;
                    env.XsltSettingsDefinition = lhs.Parent;
                    env.EnclosingConstructSymbol = _enclosingSymbol;
                    env.IsDocumentFunctionDisabled = true;
                    env.IsScriptDisabled = true;
                }
                else if (rhsPropertySymbol.IsXsltSettingsTrustedXsltProperty(_xmlTypes))
                {
                    XsltSettingsEnvironment env = new XsltSettingsEnvironment();
                    _xsltSettingsEnvironments[lhsSymbol] = env;

                    env.XsltSettingsSymbol = lhsSymbol;
                    env.XsltSettingsDefinitionSymbol = rhsPropertySymbol;
                    env.XsltSettingsDefinition = lhs.Parent;
                    env.EnclosingConstructSymbol = _enclosingSymbol;
                }
                else
                {
                    var lhsPropertySymbol = lhsSymbol as IPropertySymbol;
                    bool isXlstSettingsEnableDocumentFunctionProperty =
                        lhsPropertySymbol.IsXsltSettingsEnableDocumentFunctionProperty(_xmlTypes);
                    bool isXlstSettingsEnableScriptProperty =
                        lhsPropertySymbol.IsXsltSettingsEnableScriptProperty(_xmlTypes);

                    if (!isXlstSettingsEnableDocumentFunctionProperty &&
                        !isXlstSettingsEnableScriptProperty)
                    {
                        return;
                    }

                    IOperation? lhsExpression = (lhs as IMemberReferenceOperation)?.Instance;
                    ISymbol? lhsExpressionSymbol = lhsExpression?.GetReferencedMemberOrLocalOrParameter();
                    if (lhsExpressionSymbol is null)
                    {
                        return;
                    }

                    if (!_xsltSettingsEnvironments.TryGetValue(lhsExpressionSymbol, out var env))
                    {
                        env = new XsltSettingsEnvironment
                        {
                            XsltSettingsSymbol = lhsExpressionSymbol,
                        };
                        _xsltSettingsEnvironments[lhsExpressionSymbol] = env;
                    }

                    if (isXlstSettingsEnableDocumentFunctionProperty)
                    {
                        env.IsDocumentFunctionDisabled = rhs.TryGetBoolConstantValue(out var value) && !value;
                    }
                    else if (isXlstSettingsEnableScriptProperty)
                    {
                        env.IsScriptDisabled = rhs.TryGetBoolConstantValue(out var value) && !value;
                    }
                }
            }

            private class XsltSettingsEnvironment
            {
                internal ISymbol? XsltSettingsSymbol { get; set; }
                internal ISymbol? XsltSettingsDefinitionSymbol { get; set; }
                internal IOperation? XsltSettingsDefinition { get; set; }
                internal ISymbol? EnclosingConstructSymbol { get; set; }
                internal bool IsScriptDisabled { get; set; }
                internal bool IsDocumentFunctionDisabled { get; set; }
            }
        }
    }
}
