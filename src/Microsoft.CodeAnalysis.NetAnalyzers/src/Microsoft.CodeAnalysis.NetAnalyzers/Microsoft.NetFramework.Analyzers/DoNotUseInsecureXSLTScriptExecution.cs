// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

// TODO(dotpaul): Enable nullable analysis when rewriting this to use DFA.
#nullable disable

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Microsoft.NetFramework.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetFramework.Analyzers
{
    public abstract class DoNotUseInsecureXSLTScriptExecutionAnalyzer<TLanguageKindEnum> : DiagnosticAnalyzer where TLanguageKindEnum : struct
    {
        internal const string RuleId = "CA3076";
        internal static DiagnosticDescriptor RuleDoNotUseInsecureXSLTScriptExecution = CreateDiagnosticDescriptor(SecurityDiagnosticHelpers.GetLocalizableResourceString(nameof(MicrosoftNetFrameworkAnalyzersResources.DoNotUseInsecureDtdProcessingGenericMessage)),
                                                                                                                  SecurityDiagnosticHelpers.GetLocalizableResourceString(nameof(MicrosoftNetFrameworkAnalyzersResources.DoNotUseInsecureXSLTScriptExecutionDescription)));

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(RuleDoNotUseInsecureXSLTScriptExecution);

#pragma warning disable RS1026 // Enable concurrent execution
        public override void Initialize(AnalysisContext analysisContext)
#pragma warning restore RS1026 // Enable concurrent execution
        {
            // TODO: Make analyzer thread-safe.
            //analysisContext.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics in generated code.
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            analysisContext.RegisterCompilationStartAction(
                (context) =>
                {
                    Compilation compilation = context.Compilation;
                    var xmlTypes = new CompilationSecurityTypes(compilation);
                    if (xmlTypes.XslCompiledTransform != null)
                    {
                        context.RegisterCodeBlockStartAction<TLanguageKindEnum>(
                            (c) =>
                            {
                                GetAnalyzer(c, xmlTypes);
                            });
                    }
                });
        }

        private static DiagnosticDescriptor CreateDiagnosticDescriptor(LocalizableResourceString messageFormat, LocalizableResourceString description)
        {
            return DiagnosticDescriptorHelper.Create(RuleId,
                                            SecurityDiagnosticHelpers.GetLocalizableResourceString(nameof(MicrosoftNetFrameworkAnalyzersResources.InsecureXsltScriptProcessingMessage)),
                                            messageFormat,
                                            DiagnosticCategory.Security,
                                            RuleLevel.IdeHidden_BulkConfigurable,
                                            description,
                                            isPortedFxCopRule: false,
                                            isDataflowRule: false);
        }

        protected abstract SyntaxNodeAnalyzer GetAnalyzer(CodeBlockStartAnalysisContext<TLanguageKindEnum> context, CompilationSecurityTypes types);

        protected sealed class SyntaxNodeAnalyzer
        {
            private readonly CompilationSecurityTypes _xmlTypes;
            private readonly SyntaxNodeHelper _syntaxNodeHelper;

            private readonly Dictionary<ISymbol, XsltSettingsEnvironment> _xsltSettingsEnvironments = new Dictionary<ISymbol, XsltSettingsEnvironment>();

            public SyntaxNodeAnalyzer(CompilationSecurityTypes xmlTypes, SyntaxNodeHelper helper)
            {
                _xmlTypes = xmlTypes;
                _syntaxNodeHelper = helper;
            }

            public void AnalyzeNode(SyntaxNodeAnalysisContext context)
            {
                AnalyzeNodeForXsltSettings(context);
                AnalyzeNodeForXslCompiledTransformLoad(context);
            }

            private void AnalyzeNodeForXslCompiledTransformLoad(SyntaxNodeAnalysisContext context)
            {
                SyntaxNode node = context.Node;
                SemanticModel model = context.SemanticModel;
                IMethodSymbol methodSymbol = _syntaxNodeHelper.GetCalleeMethodSymbol(node, model);

                if (SecurityDiagnosticHelpers.IsXslCompiledTransformLoad(methodSymbol, _xmlTypes))
                {
                    bool isSecureResolver;
                    bool isSecureSettings;
                    bool isSetInBlock;

                    int xmlResolverIndex = SecurityDiagnosticHelpers.GetXmlResolverParameterIndex(methodSymbol, _xmlTypes);
                    int xsltSettingsIndex = SecurityDiagnosticHelpers.GetXsltSettingsParameterIndex(methodSymbol, _xmlTypes);

                    // Overloads with no XmlResolver and XstlSettings specified are secure since they all have folowing behavior:
                    //  1. An XmlUrlResolver with no user credentials is used to process any xsl:import or xsl:include elements.
                    //  2. The document() function is disabled.
                    //  3. Embedded scripts are not supported.
                    if (xmlResolverIndex >= 0 &&
                        xsltSettingsIndex >= 0)
                    {
                        IEnumerable<SyntaxNode> argumentExpressionNodes = _syntaxNodeHelper.GetInvocationArgumentExpressionNodes(node);
                        SyntaxNode resolverNode = argumentExpressionNodes.ElementAt(xmlResolverIndex);

                        isSecureResolver = SyntaxNodeHelper.NodeHasConstantValueNull(resolverNode, model) ||
                                           SecurityDiagnosticHelpers.IsXmlSecureResolverType(model.GetTypeInfo(resolverNode).Type, _xmlTypes);

                        SyntaxNode settingsNode = argumentExpressionNodes.ElementAt(xsltSettingsIndex);
                        ISymbol settingsSymbol = SyntaxNodeHelper.GetSymbol(settingsNode, model);

                        // 1. pass null or XsltSettings.Default as XsltSetting : secure
                        if (settingsSymbol == null || SecurityDiagnosticHelpers.IsXsltSettingsDefaultProperty(settingsSymbol as IPropertySymbol, _xmlTypes))
                        {
                            isSetInBlock = true;
                            isSecureSettings = true;
                        }
                        // 2. XsltSettings.TrustedXslt : insecure
                        else if (SecurityDiagnosticHelpers.IsXsltSettingsTrustedXsltProperty(settingsSymbol as IPropertySymbol, _xmlTypes))
                        {
                            isSetInBlock = true;
                            isSecureSettings = false;
                        }
                        // 3. check xsltSettingsEnvironments, if IsScriptDisabled && IsDocumentFunctionDisabled then secure, else insecure
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
                                isSetInBlock ? nameof(MicrosoftNetFrameworkAnalyzersResources.XslCompiledTransformLoadInsecureConstructedMessage) :
                                    nameof(MicrosoftNetFrameworkAnalyzersResources.XslCompiledTransformLoadInsecureInputMessage),
                                SecurityDiagnosticHelpers.GetNonEmptyParentName(node, model, context.CancellationToken)
                            );

                            context.ReportDiagnostic(
                                Diagnostic.Create(
                                    RuleDoNotUseInsecureXSLTScriptExecution,
                                    node.GetLocation(),
                                    message
                                )
                            );
                        }
                    }
                }
            }

            private void AnalyzeNodeForXsltSettings(SyntaxNodeAnalysisContext context)
            {
                SyntaxNode node = context.Node;
                SemanticModel model = context.SemanticModel;

                SyntaxNode lhs = _syntaxNodeHelper.GetAssignmentLeftNode(node);
                SyntaxNode rhs = _syntaxNodeHelper.GetAssignmentRightNode(node);

                if (lhs == null || rhs == null)
                {
                    return;
                }

                ISymbol lhsSymbol = SyntaxNodeHelper.GetSymbol(lhs, model);
                if (lhsSymbol == null)
                {
                    return;
                }

                IMethodSymbol rhsMethodSymbol = _syntaxNodeHelper.GetCalleeMethodSymbol(rhs, model);
                IPropertySymbol rhsPropertySymbol = SyntaxNodeHelper.GetCalleePropertySymbol(rhs, model);

                if (SecurityDiagnosticHelpers.IsXsltSettingsCtor(rhsMethodSymbol, _xmlTypes))
                {
                    XsltSettingsEnvironment env = new XsltSettingsEnvironment();
                    _xsltSettingsEnvironments[lhsSymbol] = env;

                    env.XsltSettingsSymbol = lhsSymbol;
                    env.XsltSettingsDefinitionSymbol = rhsMethodSymbol;
                    env.XsltSettingsDefinition = node;
                    env.EnclosingConstructSymbol = _syntaxNodeHelper.GetEnclosingConstructSymbol(node, model);
                    //default both properties are disbled
                    env.IsDocumentFunctionDisabled = true;
                    env.IsScriptDisabled = true;

                    // XsltSettings Constructor (Boolean, Boolean)
                    if (rhsMethodSymbol.Parameters.Any())
                    {
                        IEnumerable<SyntaxNode> argumentExpressionNodes = _syntaxNodeHelper.GetObjectCreationArgumentExpressionNodes(rhs);
                        env.IsDocumentFunctionDisabled = SyntaxNodeHelper.NodeHasConstantValueBoolFalse(argumentExpressionNodes.ElementAt(0), model);
                        env.IsScriptDisabled = SyntaxNodeHelper.NodeHasConstantValueBoolFalse(argumentExpressionNodes.ElementAt(1), model);
                    }

                    foreach (SyntaxNode arg in _syntaxNodeHelper.GetObjectInitializerExpressionNodes(rhs))
                    {
                        SyntaxNode argLhs = _syntaxNodeHelper.GetAssignmentLeftNode(arg);
                        SyntaxNode argRhs = _syntaxNodeHelper.GetAssignmentRightNode(arg);

                        ISymbol argLhsSymbol = SyntaxNodeHelper.GetSymbol(argLhs, model);

                        // anything other than a constant false is treated as true
                        if (SecurityDiagnosticHelpers.IsXsltSettingsEnableDocumentFunctionProperty(argLhsSymbol as IPropertySymbol, _xmlTypes))
                        {
                            env.IsDocumentFunctionDisabled = SyntaxNodeHelper.NodeHasConstantValueBoolFalse(argRhs, model);
                        }
                        else if (SecurityDiagnosticHelpers.IsXsltSettingsEnableScriptProperty(argLhsSymbol as IPropertySymbol, _xmlTypes))
                        {
                            env.IsScriptDisabled = SyntaxNodeHelper.NodeHasConstantValueBoolFalse(argRhs, model);
                        }
                    }
                }
                else if (SecurityDiagnosticHelpers.IsXsltSettingsDefaultProperty(rhsPropertySymbol, _xmlTypes))
                {
                    XsltSettingsEnvironment env = new XsltSettingsEnvironment();
                    _xsltSettingsEnvironments[lhsSymbol] = env;

                    env.XsltSettingsSymbol = lhsSymbol;
                    env.XsltSettingsDefinitionSymbol = rhsPropertySymbol;
                    env.XsltSettingsDefinition = node;
                    env.EnclosingConstructSymbol = _syntaxNodeHelper.GetEnclosingConstructSymbol(node, model);
                    env.IsDocumentFunctionDisabled = true;
                    env.IsScriptDisabled = true;
                }
                else if (SecurityDiagnosticHelpers.IsXsltSettingsTrustedXsltProperty(rhsPropertySymbol, _xmlTypes))
                {
                    XsltSettingsEnvironment env = new XsltSettingsEnvironment();
                    _xsltSettingsEnvironments[lhsSymbol] = env;

                    env.XsltSettingsSymbol = lhsSymbol;
                    env.XsltSettingsDefinitionSymbol = rhsPropertySymbol;
                    env.XsltSettingsDefinition = node;
                    env.EnclosingConstructSymbol = _syntaxNodeHelper.GetEnclosingConstructSymbol(node, model);
                }
                else
                {
                    bool isXlstSettingsEnableDocumentFunctionProperty = SecurityDiagnosticHelpers.IsXsltSettingsEnableDocumentFunctionProperty(lhsSymbol as IPropertySymbol, _xmlTypes);
                    bool isXlstSettingsEnableScriptProperty = SecurityDiagnosticHelpers.IsXsltSettingsEnableScriptProperty(lhsSymbol as IPropertySymbol, _xmlTypes);

                    if (isXlstSettingsEnableDocumentFunctionProperty ||
                        isXlstSettingsEnableScriptProperty)
                    {
                        SyntaxNode lhsExpressionNode = _syntaxNodeHelper.GetMemberAccessExpressionNode(lhs);
                        if (lhsExpressionNode == null)
                        {
                            return;
                        }

                        ISymbol lhsExpressionSymbol = SyntaxNodeHelper.GetSymbol(lhsExpressionNode, model);
                        if (lhsExpressionSymbol == null)
                        {
                            return;
                        }

                        if (!_xsltSettingsEnvironments.TryGetValue(lhsExpressionSymbol, out XsltSettingsEnvironment env))
                        {
                            env = new XsltSettingsEnvironment
                            {
                                XsltSettingsSymbol = lhsExpressionSymbol
                            };
                            _xsltSettingsEnvironments[lhsExpressionSymbol] = env;
                        }

                        if (isXlstSettingsEnableDocumentFunctionProperty)
                        {
                            env.IsDocumentFunctionDisabled = SyntaxNodeHelper.NodeHasConstantValueBoolFalse(rhs, model);
                        }
                        else if (isXlstSettingsEnableScriptProperty)
                        {
                            env.IsScriptDisabled = SyntaxNodeHelper.NodeHasConstantValueBoolFalse(rhs, model);
                        }
                    }
                }
            }

            private class XsltSettingsEnvironment
            {
                internal ISymbol XsltSettingsSymbol { get; set; }
                internal ISymbol XsltSettingsDefinitionSymbol { get; set; }
                internal SyntaxNode XsltSettingsDefinition { get; set; }
                internal ISymbol EnclosingConstructSymbol { get; set; }
                internal bool IsScriptDisabled { get; set; }
                internal bool IsDocumentFunctionDisabled { get; set; }
            }
        }
    }
}
