// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

// TODO(dotpaul): Enable nullable analysis after rewriting these to use DFA.
#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetFramework.Analyzers.Helpers;

namespace Microsoft.NetFramework.Analyzers
{
    /// <summary>
    /// Secure DTD processing and entity resolution in XML
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotUseInsecureDtdProcessingAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA3075";

        // Use 'DiagnosticDescriptorHelper.Create' for one of the descriptors so the rule "CA3075" can be captured in AnalyzerReleases.*.md
        internal static DiagnosticDescriptor RuleXmlDocumentWithNoSecureResolver =
            DiagnosticDescriptorHelper.Create(RuleId,
                    SecurityDiagnosticHelpers.GetLocalizableResourceString(nameof(MicrosoftNetFrameworkAnalyzersResources.InsecureXmlDtdProcessing)),
                    SecurityDiagnosticHelpers.GetLocalizableResourceString(nameof(MicrosoftNetFrameworkAnalyzersResources.XmlDocumentWithNoSecureResolverMessage)),
                    DiagnosticCategory.Security,
                    RuleLevel.IdeHidden_BulkConfigurable,
                    SecurityDiagnosticHelpers.GetLocalizableResourceString(nameof(MicrosoftNetFrameworkAnalyzersResources.DoNotUseInsecureDtdProcessingDescription)),
                    isPortedFxCopRule: false,
                    isDataflowRule: false);

        internal static DiagnosticDescriptor RuleXmlTextReaderConstructedWithNoSecureResolution =
            CreateDiagnosticDescriptor(SecurityDiagnosticHelpers.GetLocalizableResourceString(nameof(MicrosoftNetFrameworkAnalyzersResources.XmlTextReaderConstructedWithNoSecureResolutionMessage)));

        internal static DiagnosticDescriptor RuleDoNotUseDtdProcessingOverloads =
            CreateDiagnosticDescriptor(SecurityDiagnosticHelpers.GetLocalizableResourceString(nameof(MicrosoftNetFrameworkAnalyzersResources.DoNotUseDtdProcessingOverloadsMessage)));

        internal static DiagnosticDescriptor RuleXmlReaderCreateWrongOverload =
            CreateDiagnosticDescriptor(SecurityDiagnosticHelpers.GetLocalizableResourceString(nameof(MicrosoftNetFrameworkAnalyzersResources.XmlReaderCreateWrongOverloadMessage)));

        internal static DiagnosticDescriptor RuleXmlReaderCreateInsecureInput =
            CreateDiagnosticDescriptor(SecurityDiagnosticHelpers.GetLocalizableResourceString(nameof(MicrosoftNetFrameworkAnalyzersResources.XmlReaderCreateInsecureInputMessage)));

        internal static DiagnosticDescriptor RuleXmlReaderCreateInsecureConstructed =
            CreateDiagnosticDescriptor(SecurityDiagnosticHelpers.GetLocalizableResourceString(nameof(MicrosoftNetFrameworkAnalyzersResources.XmlReaderCreateInsecureConstructedMessage)));

        internal static DiagnosticDescriptor RuleXmlTextReaderSetInsecureResolution =
            CreateDiagnosticDescriptor(SecurityDiagnosticHelpers.GetLocalizableResourceString(nameof(MicrosoftNetFrameworkAnalyzersResources.XmlTextReaderSetInsecureResolutionMessage)));

        internal static DiagnosticDescriptor RuleDoNotUseSetInnerXml =
            CreateDiagnosticDescriptor(SecurityDiagnosticHelpers.GetLocalizableResourceString(nameof(MicrosoftNetFrameworkAnalyzersResources.DoNotUseSetInnerXmlMessage)));

        internal static DiagnosticDescriptor RuleReviewDtdProcessingProperties =
            CreateDiagnosticDescriptor(SecurityDiagnosticHelpers.GetLocalizableResourceString(nameof(MicrosoftNetFrameworkAnalyzersResources.ReviewDtdProcessingPropertiesMessage)));

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(
                RuleXmlDocumentWithNoSecureResolver,
                RuleXmlTextReaderConstructedWithNoSecureResolution,
                RuleDoNotUseDtdProcessingOverloads,
                RuleXmlReaderCreateWrongOverload,
                RuleXmlReaderCreateInsecureInput,
                RuleXmlReaderCreateInsecureConstructed,
                RuleXmlTextReaderSetInsecureResolution,
                RuleDoNotUseSetInnerXml,
                RuleReviewDtdProcessingProperties);

        private static void RegisterAnalyzer(OperationBlockStartAnalysisContext context, CompilationSecurityTypes types, Version frameworkVersion)
        {
            var analyzer = new OperationAnalyzer(types, frameworkVersion);
            context.RegisterOperationAction(
                analyzer.AnalyzeOperation,
                OperationKind.Invocation,
                OperationKind.SimpleAssignment,
                OperationKind.VariableDeclarator,
                OperationKind.ObjectCreation,
                OperationKind.FieldInitializer
            );
            context.RegisterOperationBlockEndAction(
                analyzer.AnalyzeOperationBlock
            );
        }

#pragma warning disable RS1026 // Enable concurrent execution
        public override void Initialize(AnalysisContext context)
#pragma warning restore RS1026 // Enable concurrent execution
        {
            // TODO: Make analyzer thread-safe
            //analysisContext.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics in generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(
                (context) =>
                {
                    Compilation compilation = context.Compilation;
                    var xmlTypes = new CompilationSecurityTypes(compilation);

                    if (ReferencesAnyTargetType(xmlTypes))
                    {
                        Version version = SecurityDiagnosticHelpers.GetDotNetFrameworkVersion(compilation);

                        if (version != null)
                        {
                            context.RegisterOperationBlockStartAction(
                                (c) =>
                                {
                                    RegisterAnalyzer(c, xmlTypes, version);
                                });
                        }
                    }
                });
        }

        private class OperationAnalyzer
        {
            #region Environment classes
            private class XmlDocumentEnvironment
            {
                internal SyntaxNode XmlDocumentDefinition { get; set; }
                internal bool IsXmlResolverSet { get; set; }
                internal bool IsSecureResolver { get; set; }

                internal XmlDocumentEnvironment(bool isTargetFrameworkSecure)
                {
                    IsXmlResolverSet = false;
                    IsSecureResolver = isTargetFrameworkSecure;
                }
            }

            private class XmlTextReaderEnvironment
            {
                internal SyntaxNode XmlTextReaderDefinition { get; set; }
                internal bool IsDtdProcessingSet { get; set; }
                internal bool IsDtdProcessingDisabled { get; set; }
                internal bool IsXmlResolverSet { get; set; }
                internal bool IsSecureResolver { get; set; }

                internal XmlTextReaderEnvironment(bool isTargetFrameworkSecure)
                {
                    // for .NET framework >= 4.5.2, the default value for XmlResolver property is null
                    if (isTargetFrameworkSecure)
                    {
                        IsSecureResolver = true;
                    }
                }
            }

            private class XmlReaderSettingsEnvironment
            {
                internal SyntaxNode XmlReaderSettingsDefinition { get; set; }
                internal bool IsDtdProcessingDisabled { get; set; }
                internal bool IsMaxCharactersFromEntitiesLimited { get; set; }
                internal bool IsSecureResolver { get; set; }
                internal bool IsConstructedInCodeBlock { get; set; }

                // this constructor is used for keep track of XmlReaderSettings not created in the code block
                internal XmlReaderSettingsEnvironment() { }

                // this constructor is used for keep track of XmlReaderSettings craeted in the code block
                internal XmlReaderSettingsEnvironment(bool isTargetFrameworkSecure)
                {
                    IsConstructedInCodeBlock = true;
                    IsDtdProcessingDisabled = true;
                    // for .NET framework >= 4.5.2, the default value for XmlResolver property is null
                    if (isTargetFrameworkSecure)
                    {
                        IsSecureResolver = true;
                        IsMaxCharactersFromEntitiesLimited = true;
                    }
                }
            }
            #endregion

            // .NET frameworks >= 4.5.2 have secure default settings
            private static readonly Version s_minSecureFxVersion = new(4, 5, 2);

            private readonly CompilationSecurityTypes _xmlTypes;
            private readonly bool _isFrameworkSecure;
            private readonly HashSet<IOperation> _objectCreationOperationsAnalyzed = new();
            private readonly Dictionary<ISymbol, XmlDocumentEnvironment> _xmlDocumentEnvironments = new();
            private readonly Dictionary<ISymbol, XmlTextReaderEnvironment> _xmlTextReaderEnvironments = new();
            private readonly Dictionary<ISymbol, XmlReaderSettingsEnvironment> _xmlReaderSettingsEnvironments = new();

            public OperationAnalyzer(CompilationSecurityTypes xmlTypes, Version targetFrameworkVersion)
            {
                _xmlTypes = xmlTypes;
                _isFrameworkSecure = targetFrameworkVersion != null && targetFrameworkVersion >= s_minSecureFxVersion;
            }

            public void AnalyzeOperationBlock(OperationBlockAnalysisContext context)
            {
                foreach (KeyValuePair<ISymbol, XmlDocumentEnvironment> p in _xmlDocumentEnvironments)
                {
                    XmlDocumentEnvironment env = p.Value;

                    if (!(env.IsXmlResolverSet | env.IsSecureResolver))
                    {
                        context.ReportDiagnostic(env.XmlDocumentDefinition.CreateDiagnostic(RuleXmlDocumentWithNoSecureResolver));
                    }
                }

                foreach (KeyValuePair<ISymbol, XmlTextReaderEnvironment> p in _xmlTextReaderEnvironments)
                {
                    XmlTextReaderEnvironment env = p.Value;
                    if (!(env.IsXmlResolverSet | env.IsSecureResolver) ||
                        !(env.IsDtdProcessingSet | env.IsDtdProcessingDisabled))
                    {
                        context.ReportDiagnostic(env.XmlTextReaderDefinition.CreateDiagnostic(RuleXmlTextReaderConstructedWithNoSecureResolution));
                    }
                }
            }

            public void AnalyzeOperation(OperationAnalysisContext context)
            {
                switch (context.Operation.Kind)
                {
                    case OperationKind.ObjectCreation:
                        AnalyzeObjectCreationOperation(context);
                        break;
                    case OperationKind.SimpleAssignment:
                        AnalyzeAssignment(context);
                        break;
                    case OperationKind.FieldInitializer:
                        var fieldInitializer = (IFieldInitializerOperation)context.Operation;
                        foreach (var field in fieldInitializer.InitializedFields)
                        {
                            AnalyzeObjectCreationInternal(context, field, fieldInitializer.Value);
                        }
                        break;
                    case OperationKind.VariableDeclarator:
                        var declarator = (IVariableDeclaratorOperation)context.Operation;
                        AnalyzeObjectCreationInternal(context, declarator.Symbol, declarator.GetVariableInitializer()?.Value);
                        break;
                    case OperationKind.Invocation:
                        var invocation = (IInvocationOperation)context.Operation;
                        AnalyzeMethodOverloads(context, invocation.TargetMethod, invocation.Arguments, invocation.Syntax);
                        break;
                }
            }

            private void AnalyzeMethodOverloads(OperationAnalysisContext context, IMethodSymbol method, ImmutableArray<IArgumentOperation> arguments, SyntaxNode expressionSyntax)
            {
                if (method.MatchMethodDerivedByName(_xmlTypes.XmlDocument, SecurityMemberNames.Load) ||                                    //FxCop CA3056
                    method.MatchMethodDerivedByName(_xmlTypes.XmlDocument, SecurityMemberNames.LoadXml) ||                                 //FxCop CA3057
                    method.MatchMethodDerivedByName(_xmlTypes.XPathDocument, WellKnownMemberNames.InstanceConstructorName) ||              //FxCop CA3059
                    method.MatchMethodDerivedByName(_xmlTypes.XmlSchema, SecurityMemberNames.Read) ||                                      //FxCop CA3060
                    method.MatchMethodDerivedByName(_xmlTypes.DataSet, SecurityMemberNames.ReadXml) ||                                     //FxCop CA3063
                    method.MatchMethodDerivedByName(_xmlTypes.DataSet, SecurityMemberNames.ReadXmlSchema) ||                               //FxCop CA3064
                    method.MatchMethodDerivedByName(_xmlTypes.XmlSerializer, SecurityMemberNames.Deserialize) ||                           //FxCop CA3070
                    method.MatchMethodDerivedByName(_xmlTypes.DataTable, SecurityMemberNames.ReadXml) ||                                   //FxCop CA3071
                    method.MatchMethodDerivedByName(_xmlTypes.DataTable, SecurityMemberNames.ReadXmlSchema))                               //FxCop CA3072
                {
                    if (SecurityDiagnosticHelpers.HasXmlReaderParameter(method, _xmlTypes) < 0)
                    {
                        context.ReportDiagnostic(expressionSyntax.CreateDiagnostic(RuleDoNotUseDtdProcessingOverloads, method.Name));
                    }
                }
                else if (method.MatchMethodDerivedByName(_xmlTypes.XmlReader, SecurityMemberNames.Create))
                {
                    int xmlReaderSettingsIndex = method.GetXmlReaderSettingsParameterIndex(_xmlTypes);

                    if (xmlReaderSettingsIndex < 0)
                    {
                        // If no XmlReaderSettings are passed, then the default
                        // XmlReaderSettings are used, with DtdProcessing set to Prohibit.
                    }
                    else if (arguments.Length > xmlReaderSettingsIndex)
                    {
                        IArgumentOperation arg = arguments[xmlReaderSettingsIndex];
                        ISymbol settingsSymbol = arg.GetReferencedMemberOrLocalOrParameter();

                        if (settingsSymbol == null)
                        {
                            return;
                        }

                        // If we have no XmlReaderSettingsEnvironment, then we don't know.
                        if (_xmlReaderSettingsEnvironments.TryGetValue(settingsSymbol, out XmlReaderSettingsEnvironment env)
                            && !env.IsDtdProcessingDisabled
                            && !(env.IsSecureResolver && env.IsMaxCharactersFromEntitiesLimited))
                        {
                            var diag = env.IsConstructedInCodeBlock
                                ? expressionSyntax.CreateDiagnostic(RuleXmlReaderCreateInsecureConstructed)
                                : expressionSyntax.CreateDiagnostic(RuleXmlReaderCreateInsecureInput);

                            context.ReportDiagnostic(diag);
                        }
                    }
                }
            }

            private void AnalyzeObjectCreationInternal(OperationAnalysisContext context, ISymbol variable, IOperation valueOpt)
            {
                if (valueOpt is not IObjectCreationOperation objCreation)
                {
                    return;
                }

                if (_objectCreationOperationsAnalyzed.Contains(objCreation))
                {
                    return;
                }
                else
                {
                    _objectCreationOperationsAnalyzed.Add(objCreation);
                }

                if (objCreation.Constructor.IsXmlDocumentCtorDerived(_xmlTypes))
                {
                    AnalyzeObjectCreationForXmlDocument(context, variable, objCreation);
                }
                else if (objCreation.Constructor.IsXmlTextReaderCtorDerived(_xmlTypes))
                {
                    AnalyzeObjectCreationForXmlTextReader(context, variable, objCreation);
                }
                else if (objCreation.Constructor.IsXmlReaderSettingsCtor(_xmlTypes))
                {
                    AnalyzeObjectCreationForXmlReaderSettings(variable, objCreation);
                }
                else
                {
                    AnalyzeMethodOverloads(context, objCreation.Constructor, objCreation.Arguments, objCreation.Syntax);
                }
            }

            private void AnalyzeObjectCreationForXmlDocument(OperationAnalysisContext context, ISymbol variable, IObjectCreationOperation objCreation)
            {
                // create new environment representation if does not already exist
                if (variable == null || !_xmlDocumentEnvironments.TryGetValue(variable, out var xmlDocumentEnvironment))
                {
                    xmlDocumentEnvironment = new XmlDocumentEnvironment(_isFrameworkSecure);
                }

                xmlDocumentEnvironment.XmlDocumentDefinition = objCreation.Syntax;
                SyntaxNode node = objCreation.Syntax;

                // initial XmlResolver secure value dependent on whether framework version secure
                // < .NET 4.5.2 insecure - XmlDocument would set XmlResolver as XmlUrlResolver
                // >= .NET 4.5.2 secure - XmlDocument would set XmlResolver as null
                bool isXmlDocumentSecureResolver = _isFrameworkSecure;

                if (!Equals(objCreation.Constructor.ContainingType, _xmlTypes.XmlDocument))
                {
                    isXmlDocumentSecureResolver = true;
                }

                // propertyInitlizer is not returned any more
                // and no way to get propertysymbol
                if (objCreation.Initializer != null)
                {
                    foreach (IOperation init in objCreation.Initializer.Initializers)
                    {
                        if (init is IAssignmentOperation assign)
                        {
                            var propValue = assign.Value;
                            if (assign.Target is not IPropertyReferenceOperation propertyReference)
                            {
                                continue;
                            }

                            var prop = propertyReference.Property;
                            if (prop.MatchPropertyDerivedByName(_xmlTypes.XmlDocument, "XmlResolver"))
                            {
                                if (propValue is not IConversionOperation operation)
                                {
                                    return;
                                }

                                // if XmlResolver declared as XmlSecureResolver by initializer
                                if (SecurityDiagnosticHelpers.IsXmlSecureResolverType(operation.Operand.Type, _xmlTypes))
                                {
                                    isXmlDocumentSecureResolver = true;
                                }
                                // if XmlResolver declared as null by initializer
                                else if (SecurityDiagnosticHelpers.IsExpressionEqualsNull(operation.Operand))
                                {
                                    isXmlDocumentSecureResolver = true;
                                }
                                // otherwise insecure resolver
                                else
                                {
                                    context.ReportDiagnostic(assign.Syntax.CreateDiagnostic(RuleXmlDocumentWithNoSecureResolver));
                                    return;
                                }
                            }
                            else
                            {
                                AnalyzeNeverSetProperties(context, prop, assign.Syntax.GetLocation());
                            }
                        }
                    }
                }

                xmlDocumentEnvironment.IsSecureResolver = isXmlDocumentSecureResolver;

                // if XmlDocument object not temporary, add environment to dictionary
                if (variable != null)
                {
                    _xmlDocumentEnvironments[variable] = xmlDocumentEnvironment;
                }
                // else is temporary (variable null) and XmlResolver insecure, then report now
                else if (!xmlDocumentEnvironment.IsSecureResolver)
                {
                    context.ReportDiagnostic(node.CreateDiagnostic(RuleXmlDocumentWithNoSecureResolver));
                }

                return;
            }

            private void AnalyzeObjectCreationForXmlTextReader(OperationAnalysisContext context, ISymbol variable, IObjectCreationOperation objCreation)
            {
                if (variable == null || !_xmlTextReaderEnvironments.TryGetValue(variable, out XmlTextReaderEnvironment env))
                {
                    env = new XmlTextReaderEnvironment(_isFrameworkSecure)
                    {
                        XmlTextReaderDefinition = objCreation.Syntax
                    };
                }

                if (!Equals(objCreation.Constructor.ContainingType, _xmlTypes.XmlTextReader))
                {
                    env.IsDtdProcessingDisabled = true;
                    env.IsSecureResolver = true;
                }

                if (objCreation.Initializer != null)
                {
                    foreach (IOperation init in objCreation.Initializer.Initializers)
                    {
                        if (init is IAssignmentOperation assign)
                        {
                            var propValue = assign.Value;
                            if (assign.Target is not IPropertyReferenceOperation propertyReference)
                            {
                                continue;
                            }

                            var prop = propertyReference.Property;
                            if (propValue is IConversionOperation operation
                                && SecurityDiagnosticHelpers.IsXmlTextReaderXmlResolverPropertyDerived(prop, _xmlTypes))
                            {
                                env.IsXmlResolverSet = true;

                                if (SecurityDiagnosticHelpers.IsXmlSecureResolverType(operation.Operand.Type, _xmlTypes))
                                {
                                    env.IsSecureResolver = true;
                                }
                                else if (SecurityDiagnosticHelpers.IsExpressionEqualsNull(operation.Operand))
                                {
                                    env.IsSecureResolver = true;
                                }
                                else
                                {
                                    env.IsSecureResolver = false;
                                }
                            }
                            else if (SecurityDiagnosticHelpers.IsXmlTextReaderDtdProcessingPropertyDerived(prop, _xmlTypes))
                            {
                                env.IsDtdProcessingSet = true;
                                env.IsDtdProcessingDisabled = !SecurityDiagnosticHelpers.IsExpressionEqualsDtdProcessingParse(propValue);
                            }
                        }
                    }
                }

                // if the XmlResolver or Dtdprocessing property is explicitly set when created, and is to an insecure value, generate a warning
                if ((env.IsXmlResolverSet && !env.IsSecureResolver) ||
                    (env.IsDtdProcessingSet && !env.IsDtdProcessingDisabled))
                {
                    context.ReportDiagnostic(env.XmlTextReaderDefinition.CreateDiagnostic(RuleXmlTextReaderSetInsecureResolution));
                }
                // if the XmlResolver or Dtdprocessing property is not explicitly set when constructed for a non-temp XmlTextReader object, add env to the dictionary.
                else if (variable != null && !(env.IsDtdProcessingSet && env.IsXmlResolverSet))
                {
                    _xmlTextReaderEnvironments[variable] = env;
                }
                // if the is not set or set to Parse for a temporary object, report right now.
                else if (variable == null && !(env.IsDtdProcessingSet && env.IsDtdProcessingDisabled))
                {
                    context.ReportDiagnostic(env.XmlTextReaderDefinition.CreateDiagnostic(RuleXmlTextReaderConstructedWithNoSecureResolution));
                }
            }

            private void AnalyzeObjectCreationForXmlReaderSettings(ISymbol variable, IObjectCreationOperation objCreation)
            {
                XmlReaderSettingsEnvironment xmlReaderSettingsEnv = new XmlReaderSettingsEnvironment(_isFrameworkSecure);

                if (variable != null)
                {
                    _xmlReaderSettingsEnvironments[variable] = xmlReaderSettingsEnv;
                }

                xmlReaderSettingsEnv.XmlReaderSettingsDefinition = objCreation.Syntax;

                if (objCreation.Initializer != null)
                {
                    foreach (IOperation init in objCreation.Initializer.Initializers)
                    {
                        if (init is IAssignmentOperation assign)
                        {
                            var propValue = assign.Value;
                            if (assign.Target is not IPropertyReferenceOperation propertyReference)
                            {
                                continue;
                            }

                            var prop = propertyReference.Property;
                            if (SecurityDiagnosticHelpers.IsXmlReaderSettingsXmlResolverProperty(
                                    prop,
                                    _xmlTypes)
                                )
                            {

                                if (propValue is not IConversionOperation operation)
                                {
                                    return;
                                }

                                if (SecurityDiagnosticHelpers.IsXmlSecureResolverType(operation.Operand.Type, _xmlTypes))
                                {
                                    xmlReaderSettingsEnv.IsSecureResolver = true;
                                }
                                else if (SecurityDiagnosticHelpers.IsExpressionEqualsNull(operation.Operand))
                                {
                                    xmlReaderSettingsEnv.IsSecureResolver = true;
                                }
                            }
                            else if (SecurityDiagnosticHelpers.IsXmlReaderSettingsDtdProcessingProperty(prop, _xmlTypes))
                            {
                                xmlReaderSettingsEnv.IsDtdProcessingDisabled = !SecurityDiagnosticHelpers.IsExpressionEqualsDtdProcessingParse(propValue);
                            }
                            else if (SecurityDiagnosticHelpers.IsXmlReaderSettingsMaxCharactersFromEntitiesProperty(prop, _xmlTypes))
                            {
                                xmlReaderSettingsEnv.IsMaxCharactersFromEntitiesLimited = !SecurityDiagnosticHelpers.IsExpressionEqualsIntZero(propValue);
                            }
                        }
                    }
                }
            }

            private void AnalyzeXmlResolverPropertyAssignmentForXmlDocument(OperationAnalysisContext context, ISymbol assignedSymbol, IAssignmentOperation expression)
            {
                bool isSecureResolver = false;

                IConversionOperation conv = expression.Value as IConversionOperation;
                if (conv != null && SecurityDiagnosticHelpers.IsXmlSecureResolverType(conv.Operand.Type, _xmlTypes))
                {
                    isSecureResolver = true;
                }
                else if (conv != null && SecurityDiagnosticHelpers.IsExpressionEqualsNull(conv.Operand))
                {
                    isSecureResolver = true;
                }
                else // Assigning XmlDocument's XmlResolver to an insecure value
                {
                    context.ReportDiagnostic(context.Operation.Syntax.CreateDiagnostic(RuleXmlDocumentWithNoSecureResolver));
                }

                if (_xmlDocumentEnvironments.TryGetValue(assignedSymbol, out XmlDocumentEnvironment xmlDocumentEnv))
                {
                    xmlDocumentEnv.IsXmlResolverSet = true;
                    xmlDocumentEnv.IsSecureResolver = isSecureResolver;
                }
            }

            private void AnalyzeXmlTextReaderProperties(OperationAnalysisContext context, ISymbol assignedSymbol, IAssignmentOperation expression, bool isXmlTextReaderXmlResolverProperty, bool isXmlTextReaderDtdProcessingProperty)
            {
                if (!_xmlTextReaderEnvironments.TryGetValue(assignedSymbol, out XmlTextReaderEnvironment env))
                {
                    env = new XmlTextReaderEnvironment(_isFrameworkSecure);
                }

                if (isXmlTextReaderXmlResolverProperty)
                {
                    env.IsXmlResolverSet = true;
                }
                else
                {
                    env.IsDtdProcessingSet = true;
                }

                IConversionOperation conv = expression.Value as IConversionOperation;

                if (isXmlTextReaderXmlResolverProperty && conv != null && SecurityDiagnosticHelpers.IsXmlSecureResolverType(conv.Operand.Type, _xmlTypes))
                {
                    env.IsSecureResolver = true;
                }
                else if (isXmlTextReaderXmlResolverProperty && conv != null && SecurityDiagnosticHelpers.IsExpressionEqualsNull(conv.Operand))
                {
                    env.IsSecureResolver = true;
                }
                else if (isXmlTextReaderDtdProcessingProperty && conv == null && !SecurityDiagnosticHelpers.IsExpressionEqualsDtdProcessingParse(expression.Value))
                {
                    env.IsDtdProcessingDisabled = !SecurityDiagnosticHelpers.IsExpressionEqualsDtdProcessingParse(expression.Value);
                }
                else if (context.Operation?.Parent?.Kind != OperationKind.ObjectOrCollectionInitializer)
                {
                    // Generate a warning whenever the XmlResolver or DtdProcessing property is set to an insecure value
                    context.ReportDiagnostic(expression.Syntax.CreateDiagnostic(RuleXmlTextReaderSetInsecureResolution));
                }
            }

            private void AnalyzeAssignment(OperationAnalysisContext context)
            {
                var assignment = (IAssignmentOperation)context.Operation;

                if (assignment.Target is not IPropertyReferenceOperation propRef) // A variable/field assignment
                {
                    var symbolAssignedTo = assignment.Target.GetReferencedMemberOrLocalOrParameter();
                    if (symbolAssignedTo != null)
                    {
                        AnalyzeObjectCreationInternal(context, symbolAssignedTo, assignment.Value);
                    }
                }
                else // A property assignment
                {
                    ISymbol assignedSymbol = propRef.Instance?.GetReferencedMemberOrLocalOrParameter();
                    if (assignedSymbol == null)
                    {
                        return;
                    }

                    if (propRef.Property.MatchPropertyByName(_xmlTypes.XmlDocument, "XmlResolver"))
                    {
                        AnalyzeXmlResolverPropertyAssignmentForXmlDocument(context, assignedSymbol, assignment);
                    }
                    else
                    {
                        bool isXmlTextReaderXmlResolverProperty =
                            SecurityDiagnosticHelpers.IsXmlTextReaderXmlResolverPropertyDerived(propRef.Property, _xmlTypes);
                        bool isXmlTextReaderDtdProcessingProperty = !isXmlTextReaderXmlResolverProperty &&
                            SecurityDiagnosticHelpers.IsXmlTextReaderDtdProcessingPropertyDerived(propRef.Property, _xmlTypes);
                        if (isXmlTextReaderXmlResolverProperty || isXmlTextReaderDtdProcessingProperty)
                        {
                            AnalyzeXmlTextReaderProperties(context, assignedSymbol, assignment, isXmlTextReaderXmlResolverProperty,
                                isXmlTextReaderDtdProcessingProperty);
                        }
                        else if (SecurityDiagnosticHelpers.IsXmlReaderSettingsType(propRef.Instance.Type, _xmlTypes))
                        {

                            if (!_xmlReaderSettingsEnvironments.TryGetValue(assignedSymbol, out XmlReaderSettingsEnvironment env))
                            {
                                env = new XmlReaderSettingsEnvironment(_isFrameworkSecure);
                                _xmlReaderSettingsEnvironments[assignedSymbol] = env;
                            }

                            if (assignment.Value is IConversionOperation conv && SecurityDiagnosticHelpers.IsXmlReaderSettingsXmlResolverProperty(propRef.Property, _xmlTypes))
                            {
                                if (SecurityDiagnosticHelpers.IsXmlSecureResolverType(conv.Operand.Type, _xmlTypes))
                                {
                                    env.IsSecureResolver = true;
                                }
                                else if (SecurityDiagnosticHelpers.IsExpressionEqualsNull(conv.Operand))
                                {
                                    env.IsSecureResolver = true;
                                }
                            }
                            else if (SecurityDiagnosticHelpers.IsXmlReaderSettingsDtdProcessingProperty(propRef.Property, _xmlTypes))
                            {
                                env.IsDtdProcessingDisabled =
                                    !SecurityDiagnosticHelpers.IsExpressionEqualsDtdProcessingParse(assignment.Value);
                            }
                            else if (SecurityDiagnosticHelpers.IsXmlReaderSettingsMaxCharactersFromEntitiesProperty(propRef.Property,
                                _xmlTypes))
                            {
                                env.IsMaxCharactersFromEntitiesLimited =
                                    !SecurityDiagnosticHelpers.IsExpressionEqualsIntZero(assignment.Value);
                            }
                        }
                        else
                        {
                            AnalyzeNeverSetProperties(context, propRef.Property, assignment.Syntax.GetLocation());
                        }
                    }
                }
            }

            private void AnalyzeNeverSetProperties(OperationAnalysisContext context, IPropertySymbol property, Location location)
            {
                if (property.MatchPropertyDerivedByName(_xmlTypes.XmlDocument, SecurityMemberNames.InnerXml))
                {
                    context.ReportDiagnostic(Diagnostic.Create(RuleDoNotUseSetInnerXml, location));
                }
                else if (property.MatchPropertyDerivedByName(_xmlTypes.DataViewManager, SecurityMemberNames.DataViewSettingCollectionString))
                {
                    context.ReportDiagnostic(Diagnostic.Create(RuleReviewDtdProcessingProperties, location));
                }
            }

            private void AnalyzeObjectCreationOperation(OperationAnalysisContext context)
            {
                AnalyzeObjectCreationInternal(context, null, context.Operation);
            }
        }

        private static bool ReferencesAnyTargetType(CompilationSecurityTypes types)
        {
            return types.XmlDocument != null
                || types.XmlNode != null
                || types.XmlReader != null
                || types.XmlTextReader != null
                || types.XPathDocument != null
                || types.XmlSchema != null
                || types.DataSet != null
                || types.DataTable != null
                || types.DataViewManager != null
                || types.XmlSerializer != null;
        }

        private static DiagnosticDescriptor CreateDiagnosticDescriptor(LocalizableResourceString messageFormat)
        {
            return DiagnosticDescriptorHelper.Create(RuleId,
                                            SecurityDiagnosticHelpers.GetLocalizableResourceString(nameof(MicrosoftNetFrameworkAnalyzersResources.InsecureXmlDtdProcessing)),
                                            messageFormat,
                                            DiagnosticCategory.Security,
                                            RuleLevel.IdeHidden_BulkConfigurable,
                                            SecurityDiagnosticHelpers.GetLocalizableResourceString(nameof(MicrosoftNetFrameworkAnalyzersResources.DoNotUseInsecureDtdProcessingDescription)),
                                            isPortedFxCopRule: false,
                                            isDataflowRule: false);
        }
    }
}
