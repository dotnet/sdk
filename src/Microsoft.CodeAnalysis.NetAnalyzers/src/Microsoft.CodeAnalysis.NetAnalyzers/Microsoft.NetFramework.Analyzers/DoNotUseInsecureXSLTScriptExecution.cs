// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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
                RuleLevel.IdeSuggestion,
                SecurityDiagnosticHelpers.GetLocalizableResourceString(nameof(MicrosoftNetFrameworkAnalyzersResources.DoNotUseInsecureXSLTScriptExecutionDescription)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(RuleDoNotUseInsecureXSLTScriptExecution);

#pragma warning disable RS1026 // Enable concurrent execution
        public override void Initialize(AnalysisContext analysisContext)
#pragma warning restore RS1026 // Enable concurrent execution
        {
            //analysisContext.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics in generated code.
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            analysisContext.RegisterCompilationStartAction(
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
                            analyzer.AnalyzeNodeForXsltSettings(variableDeclarator, variableDeclarator.GetVariableInitializer().Value);
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

            public void AnalyzeNodeForXslCompiledTransformLoad(IMethodSymbol methodSymbol, ImmutableArray<IArgumentOperation> arguments,
                Action<Diagnostic> reportDiagnostic)
            {
                if (!SecurityDiagnosticHelpers.IsXslCompiledTransformLoad(methodSymbol, _xmlTypes))
                {
                    return;
                }

                int xmlResolverIndex = SecurityDiagnosticHelpers.GetXmlResolverParameterIndex(methodSymbol, _xmlTypes);
                int xsltSettingsIndex = SecurityDiagnosticHelpers.GetXsltSettingsParameterIndex(methodSymbol, _xmlTypes);

                // Overloads with no XmlResolver and XstlSettings specified are secure since they all have following behavior:
                //  1. An XmlUrlResolver with no user credentials is used to process any xsl:import or xsl:include elements.
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
                    || SecurityDiagnosticHelpers.IsXmlSecureResolverType(resolverArgumentOperation.Type, _xmlTypes);

                var settingsOperation = arguments[xsltSettingsIndex].Value;
                ISymbol? settingsSymbol = settingsOperation.Kind switch
                {
                    OperationKind.ParameterReference => ((IParameterReferenceOperation)settingsOperation).Parameter,
                    OperationKind.LocalReference => ((ILocalReferenceOperation)settingsOperation).Local,
                    OperationKind.FieldReference => ((IFieldReferenceOperation)settingsOperation).Field,
                    OperationKind.PropertyReference => ((IPropertyReferenceOperation)settingsOperation).Property,
                    _ => null,
                };

                bool isSecureSettings;
                bool isSetInBlock;

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
                        isSetInBlock
                            ? nameof(MicrosoftNetFrameworkAnalyzersResources.XslCompiledTransformLoadInsecureConstructedMessage)
                            : nameof(MicrosoftNetFrameworkAnalyzersResources.XslCompiledTransformLoadInsecureInputMessage),
                        SecurityDiagnosticHelpers.GetNonEmptyParentName(_enclosingSymbol)
                    );

                    var invocationOrObjCreation = settingsOperation.Parent.Parent;
                    reportDiagnostic(invocationOrObjCreation.CreateDiagnostic(RuleDoNotUseInsecureXSLTScriptExecution, message));
                }
            }

            public void AnalyzeNodeForXsltSettings(IOperation lhs, IOperation rhs)
            {
                ISymbol? lhsSymbol = lhs.Kind switch
                {
                    OperationKind.VariableDeclarator => ((IVariableDeclaratorOperation)lhs).Symbol,
                    OperationKind.PropertyReference => ((IPropertyReferenceOperation)lhs).Property,
                    OperationKind.ParameterReference => ((IParameterReferenceOperation)lhs).Parameter,
                    OperationKind.LocalReference => ((ILocalReferenceOperation)lhs).Local,
                    OperationKind.FieldReference => ((IFieldReferenceOperation)lhs).Field,
                    _ => null,
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

                if (SecurityDiagnosticHelpers.IsXsltSettingsCtor(rhsMethodSymbol, _xmlTypes))
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
                        env.IsDocumentFunctionDisabled = arguments[0].Value.TryGetBoolConstantValue(out var value) && !value;
                        env.IsScriptDisabled = arguments[1].Value.TryGetBoolConstantValue(out value) && !value;
                    }

                    var initializers = rhs.Kind switch
                    {
                        OperationKind.ObjectCreation => ((IObjectCreationOperation)rhs).Initializer?.Initializers,
                        _ => null,
                    } ?? ImmutableArray<IOperation>.Empty;

                    foreach (var arg in initializers)
                    {
                        if (arg is not ISimpleAssignmentOperation assignment)
                        {
                            continue;
                        }

                        var argLhsPropertySymbol = (assignment.Target as IPropertyReferenceOperation)?.Property;
                        var argRhs = assignment.Value;

                        // anything other than a constant false is treated as true
                        if (SecurityDiagnosticHelpers.IsXsltSettingsEnableDocumentFunctionProperty(argLhsPropertySymbol, _xmlTypes))
                        {
                            env.IsDocumentFunctionDisabled = argRhs.TryGetBoolConstantValue(out var value) && !value;
                        }
                        else if (SecurityDiagnosticHelpers.IsXsltSettingsEnableScriptProperty(argLhsPropertySymbol, _xmlTypes))
                        {
                            env.IsScriptDisabled = argRhs.TryGetBoolConstantValue(out var value) && !value;
                        }
                    }
                }
                else if (SecurityDiagnosticHelpers.IsXsltSettingsDefaultProperty(rhsPropertySymbol, _xmlTypes))
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
                else if (SecurityDiagnosticHelpers.IsXsltSettingsTrustedXsltProperty(rhsPropertySymbol, _xmlTypes))
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
                    bool isXlstSettingsEnableDocumentFunctionProperty = SecurityDiagnosticHelpers.IsXsltSettingsEnableDocumentFunctionProperty(lhsPropertySymbol, _xmlTypes);
                    bool isXlstSettingsEnableScriptProperty = SecurityDiagnosticHelpers.IsXsltSettingsEnableScriptProperty(lhsPropertySymbol, _xmlTypes);

                    if (!isXlstSettingsEnableDocumentFunctionProperty &&
                        !isXlstSettingsEnableScriptProperty)
                    {
                        return;
                    }

                    IOperation? lhsExpression = lhs.Kind switch
                    {
                        OperationKind.FieldReference => ((IFieldReferenceOperation)lhs).Instance,
                        OperationKind.PropertyReference => ((IPropertyReferenceOperation)lhs).Instance,
                        _ => null,
                    };

                    ISymbol? lhsExpressionSymbol = lhsExpression?.Kind switch
                    {
                        OperationKind.ParameterReference => ((IParameterReferenceOperation)lhsExpression).Parameter,
                        OperationKind.LocalReference => ((ILocalReferenceOperation)lhsExpression).Local,
                        OperationKind.FieldReference => ((IFieldReferenceOperation)lhsExpression).Field,
                        OperationKind.PropertyReference => ((IPropertyReferenceOperation)lhsExpression).Property,
                        _ => null,
                    };

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
