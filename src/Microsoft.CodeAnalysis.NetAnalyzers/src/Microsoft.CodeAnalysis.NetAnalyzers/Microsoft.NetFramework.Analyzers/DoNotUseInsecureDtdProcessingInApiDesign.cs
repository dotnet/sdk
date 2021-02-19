// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetFramework.Analyzers.Helpers;

namespace Microsoft.NetFramework.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotUseInsecureDtdProcessingInApiDesignAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA3077";

        internal static DiagnosticDescriptor RuleDoNotUseInsecureDtdProcessingInApiDesign =
            DiagnosticDescriptorHelper.Create(
                RuleId,
                SecurityDiagnosticHelpers.GetLocalizableResourceString(nameof(MicrosoftNetFrameworkAnalyzersResources.InsecureDtdProcessingInApiDesign)),
                nameof(MicrosoftNetFrameworkAnalyzersResources.DoNotUseInsecureDtdProcessingGenericMessage),
                DiagnosticCategory.Security,
                RuleLevel.IdeHidden_BulkConfigurable,
                SecurityDiagnosticHelpers.GetLocalizableResourceString(nameof(MicrosoftNetFrameworkAnalyzersResources.DoNotUseInsecureDtdProcessingInApiDesignDescription)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(RuleDoNotUseInsecureDtdProcessingInApiDesign);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics in generated code.
            context.ConfigureGeneratedCodeAnalysis(
                GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(context =>
            {
                Compilation compilation = context.Compilation;
                var xmlTypes = new CompilationSecurityTypes(compilation);
                if (ReferencesAnyTargetType(xmlTypes))
                {
                    var version = SecurityDiagnosticHelpers.GetDotNetFrameworkVersion(compilation);

                    // bail if we are not analyzing project targeting .NET Framework
                    // TODO: should we throw an exception to notify user?
                    if (version != null)
                    {
                        SymbolAndNodeAnalyzer analyzer = new SymbolAndNodeAnalyzer(xmlTypes, version);
                        context.RegisterSymbolAction(analyzer.AnalyzeSymbol, SymbolKind.NamedType);
                        context.RegisterOperationBlockStartAction(analyzer.AnalyzeOperationBlock);
                    }
                }
            });
        }

        private static bool ReferencesAnyTargetType(CompilationSecurityTypes types)
            => types.XmlDocument != null || types.XmlTextReader != null;

        private sealed class SymbolAndNodeAnalyzer
        {
            // .NET frameworks >= 4.5.2 have secure default settings for XmlTextReader:
            //      DtdProcessing is enabled with null resolver
            private static readonly Version s_minSecureFxVersion = new(4, 5, 2);

            private readonly CompilationSecurityTypes _xmlTypes;
            private readonly bool _isFrameworkSecure;

            // key: symbol for type derived from XmlDocument/XmlTextReader (exclude base type itself)
            // value: if it has explicitly defined constructor
            private readonly ConcurrentDictionary<INamedTypeSymbol, bool> _xmlDocumentDerivedTypes = new();
            private readonly ConcurrentDictionary<INamedTypeSymbol, bool> _xmlTextReaderDerivedTypes = new();

            public SymbolAndNodeAnalyzer(CompilationSecurityTypes xmlTypes,
                Version targetFrameworkVersion)
            {
                _xmlTypes = xmlTypes;
                _isFrameworkSecure = targetFrameworkVersion != null
                    && targetFrameworkVersion >= s_minSecureFxVersion;
            }

            public void AnalyzeOperationBlock(OperationBlockStartAnalysisContext context)
            {
                AnalyzeBlockForXmlDocumentDerivedTypeConstructorDecl(context);
                AnalyzeBlockForXmlDocumentDerivedTypeMethodDecl(context);
                if (!_isFrameworkSecure)
                {
                    AnalyzeBlockForXmlTextReaderDerivedTypeConstructorDecl(context);
                }
                AnalyzeBlockForXmlTextReaderDerivedTypeMethodDecl(context);
            }

            public void AnalyzeSymbol(SymbolAnalysisContext context)
            {
                AnalyzeSymbolForXmlDocumentDerivedType(context);
                AnalyzeSymbolForXmlTextReaderDerivedType(context);
            }

            private void AnalyzeBlockForXmlDocumentDerivedTypeConstructorDecl(
                OperationBlockStartAnalysisContext context)
            {
                if (context.OwningSymbol is not IMethodSymbol methodSymbol
                    || methodSymbol.MethodKind != MethodKind.Constructor
                    || Equals(methodSymbol.ContainingType, _xmlTypes.XmlDocument)
                    || !methodSymbol.ContainingType.DerivesFrom(_xmlTypes.XmlDocument, baseTypesOnly: true))
                {
                    return;
                }

                var hasSetSecureXmlResolver = new ThreadSafeBoolean(false);
                context.RegisterOperationAction(context =>
                {
                    var assignment = (IAssignmentOperation)context.Operation;

                    if (hasSetSecureXmlResolver.Value)
                    {
                        return;
                    }

                    var result = IsAssigningIntendedValueToPropertyDerivedFromType(
                        assignment,
                        property =>
                            SecurityDiagnosticHelpers.IsXmlDocumentXmlResolverProperty(property, _xmlTypes),
                        operation =>
                            operation.HasNullConstantValue()
                            || SecurityDiagnosticHelpers.IsXmlSecureResolverType(operation.Type, _xmlTypes),
                        out _);

                    if (result)
                    {
                        hasSetSecureXmlResolver.Value = true;
                    }
                }, OperationKind.SimpleAssignment);

                context.RegisterOperationBlockEndAction(context =>
                {
                    if (hasSetSecureXmlResolver.Value)
                    {
                        return;
                    }

                    context.ReportDiagnostic(
                        methodSymbol.CreateDiagnostic(
                            RuleDoNotUseInsecureDtdProcessingInApiDesign,
                            SecurityDiagnosticHelpers.GetLocalizableResourceString(
                                nameof(MicrosoftNetFrameworkAnalyzersResources.XmlDocumentDerivedClassConstructorNoSecureXmlResolverMessage),
                                SecurityDiagnosticHelpers.GetNonEmptyParentName(methodSymbol)
                            )
                        )
                    );
                });
            }

            // Trying to find every "this.XmlResolver = [Insecure Resolve];" in methods of
            // types derived from XmlDocment and generate a warning for each
            private void AnalyzeBlockForXmlDocumentDerivedTypeMethodDecl(OperationBlockStartAnalysisContext context)
            {
                if (context.OwningSymbol is not IMethodSymbol methodSymbol
                    // skip constructors since we report on the absence of secure assignment in AnalyzeBlockForXmlDocumentDerivedTypeConstructorDecl
                    || methodSymbol.MethodKind == MethodKind.Constructor
                    || Equals(methodSymbol.ContainingType, _xmlTypes.XmlDocument)
                    || !methodSymbol.ContainingType.DerivesFrom(_xmlTypes.XmlDocument, baseTypesOnly: true))
                {
                    return;
                }

                context.RegisterOperationAction(context =>
                {
                    var assignment = (IAssignmentOperation)context.Operation;

                    var isAssigned = IsAssigningIntendedValueToPropertyDerivedFromType(
                        assignment,
                        property => SecurityDiagnosticHelpers.IsXmlDocumentXmlResolverProperty(property, _xmlTypes),
                        operation =>
                            !operation.HasNullConstantValue()
                            || SecurityDiagnosticHelpers.IsXmlSecureResolverType(operation.Type, _xmlTypes),
                        out _);

                    if (isAssigned)
                    {
                        context.ReportDiagnostic(
                            assignment.CreateDiagnostic(
                                RuleDoNotUseInsecureDtdProcessingInApiDesign,
                                SecurityDiagnosticHelpers.GetLocalizableResourceString(
                                    nameof(MicrosoftNetFrameworkAnalyzersResources.XmlDocumentDerivedClassSetInsecureXmlResolverInMethodMessage),
                                    methodSymbol.Name
                                )
                            )
                        );
                    }
                }, OperationKind.SimpleAssignment);
            }

            private void AnalyzeBlockForXmlTextReaderDerivedTypeConstructorDecl(
                OperationBlockStartAnalysisContext context)
            {
                if (context.OwningSymbol is not IMethodSymbol methodSymbol
                    || methodSymbol.MethodKind != MethodKind.Constructor
                    || Equals(methodSymbol.ContainingType, _xmlTypes.XmlTextReader)
                    || !methodSymbol.ContainingType.DerivesFrom(_xmlTypes.XmlTextReader, baseTypesOnly: true))
                {
                    return;
                }

                var hasSetSecureXmlResolver = new ThreadSafeBoolean(false);
                var isDtdProcessingDisabled = new ThreadSafeBoolean(false);

                context.RegisterOperationAction(context =>
                {
                    var assignment = (IAssignmentOperation)context.Operation;
                    if (hasSetSecureXmlResolver.Value && isDtdProcessingDisabled.Value)
                    {
                        return;
                    }

                    bool isTargetProperty = false;

                    if (!hasSetSecureXmlResolver.Value)
                    {
                        var result = IsAssigningIntendedValueToPropertyDerivedFromType(
                            assignment,
                            property =>
                                SecurityDiagnosticHelpers.IsXmlTextReaderXmlResolverProperty(property, _xmlTypes),
                            operation =>
                                operation.HasNullConstantValue()
                                || SecurityDiagnosticHelpers.IsXmlSecureResolverType(operation.Type, _xmlTypes),
                            out isTargetProperty);

                        if (result)
                        {
                            hasSetSecureXmlResolver.Value = true;
                        }
                    }

                    if (!isTargetProperty && !isDtdProcessingDisabled.Value)
                    {
                        var result = IsAssigningIntendedValueToPropertyDerivedFromType(
                            assignment,
                            property =>
                                SecurityDiagnosticHelpers.IsXmlTextReaderDtdProcessingProperty(property, _xmlTypes),
                            operation =>
                                operation is not IFieldReferenceOperation fieldReference
                                || _xmlTypes.DtdProcessing == null
                                || !fieldReference.Field.MatchMemberByName(_xmlTypes.DtdProcessing, SecurityMemberNames.Parse),
                            out _);

                        if (result)
                        {
                            isDtdProcessingDisabled.Value = true;
                        }
                    }
                }, OperationKind.SimpleAssignment);

                context.RegisterOperationBlockEndAction(context =>
                {
                    if (!hasSetSecureXmlResolver.Value || !isDtdProcessingDisabled.Value)
                    {
                        context.ReportDiagnostic(
                            methodSymbol.CreateDiagnostic(
                                RuleDoNotUseInsecureDtdProcessingInApiDesign,
                                SecurityDiagnosticHelpers.GetLocalizableResourceString(
                                    nameof(MicrosoftNetFrameworkAnalyzersResources.XmlTextReaderDerivedClassConstructorNoSecureSettingsMessage),
                                    SecurityDiagnosticHelpers.GetNonEmptyParentName(methodSymbol)
                                )
                            )
                        );
                    }
                });
            }

            private void AnalyzeBlockForXmlTextReaderDerivedTypeMethodDecl(
                OperationBlockStartAnalysisContext context)
            {
                if (context.OwningSymbol is not IMethodSymbol methodSymbol
                    || Equals(methodSymbol.ContainingType, _xmlTypes.XmlTextReader)
                    || !methodSymbol.ContainingType.DerivesFrom(_xmlTypes.XmlTextReader, baseTypesOnly: true))
                {
                    return;
                }

                // If the default value are not secure, the AnalyzeNodeForXmlTextReaderDerivedTypeConstructorDecl
                // would be skipped, therefore we need to check constructor for any insecure settings.
                // Otherwise, we skip checking constructors
                if (_isFrameworkSecure && methodSymbol.MethodKind == MethodKind.Constructor)
                {
                    return;
                }

                var hasSetXmlResolver = new ThreadSafeBoolean(false);
                var hasSetInsecureXmlResolver = new ThreadSafeBoolean(true);
                var isDtdProcessingSet = new ThreadSafeBoolean(false);
                var isDtdProcessingEnabled = new ThreadSafeBoolean(true);

                var locations = new ConcurrentQueue<Location>();

                context.RegisterOperationAction(context =>
                {
                    var assignment = (IAssignmentOperation)context.Operation;

                    var result = IsAssigningIntendedValueToPropertyDerivedFromType(
                        assignment,
                        property =>
                            SecurityDiagnosticHelpers.IsXmlTextReaderXmlResolverProperty(property, _xmlTypes),
                        operation =>
                            !operation.HasNullConstantValue()
                            && !SecurityDiagnosticHelpers.IsXmlSecureResolverType(operation.Type, _xmlTypes),
                        out var isTargetProperty);

                    if (isTargetProperty)
                    {
                        hasSetXmlResolver.Value = true;
                        // use 'AND' to avoid false positives (but increase false negative rate)
                        if (!result)
                        {
                            hasSetInsecureXmlResolver.Value = false;
                        }
                        else
                        {
                            locations.Enqueue(assignment.Syntax.GetLocation());
                        }
                        return;
                    }

                    result = IsAssigningIntendedValueToPropertyDerivedFromType(
                        assignment,
                        property =>
                            SecurityDiagnosticHelpers.IsXmlTextReaderDtdProcessingProperty(property, _xmlTypes),
                        operation =>
                            operation is IFieldReferenceOperation fieldReference
                            && _xmlTypes.DtdProcessing != null
                            && fieldReference.Field.MatchMemberByName(_xmlTypes.DtdProcessing, SecurityMemberNames.Parse),
                        out isTargetProperty);

                    if (isTargetProperty)
                    {
                        isDtdProcessingSet.Value = true;
                        // use 'AND' to avoid false positives (but increase false negative rate)
                        if (!result)
                        {
                            isDtdProcessingEnabled.Value = false;
                        }
                        else
                        {
                            locations.Enqueue(assignment.Syntax.GetLocation());
                        }
                    }
                }, OperationKind.SimpleAssignment);

                context.RegisterOperationBlockEndAction(context =>
                {
                    // neither XmlResolver nor DtdProcessing is explicitly set
                    if (!(hasSetXmlResolver.Value || isDtdProcessingSet.Value))
                    {
                        return;
                    }
                    // explicitly set XmlResolver and/or DtdProcessing to secure value
                    else if (!hasSetInsecureXmlResolver.Value || !isDtdProcessingEnabled.Value)
                    {
                        return;
                    }
                    // didn't explicitly set either one of XmlResolver and DtdProcessing to secure value
                    // but explicitly set XmlResolver and/or DtdProcessing to insecure value
                    else
                    {
                        // TODO: Only first location is shown in error, maybe we want to report on method instead?
                        //       Or on each insecure assignment?
                        context.ReportDiagnostic(
                            locations.OrderBy(l => l.SourceSpan.Start).CreateDiagnostic(
                                RuleDoNotUseInsecureDtdProcessingInApiDesign,
                                SecurityDiagnosticHelpers.GetLocalizableResourceString(
                                    nameof(MicrosoftNetFrameworkAnalyzersResources.XmlTextReaderDerivedClassSetInsecureSettingsInMethodMessage),
                                    methodSymbol.Name
                                )
                            )
                        );
                    }
                });
            }

            // report warning if no explicit definition of constructor in XmlDocument derived types
            private void AnalyzeSymbolForXmlDocumentDerivedType(SymbolAnalysisContext context)
            {
                ISymbol symbol = context.Symbol;
                if (symbol.Kind != SymbolKind.NamedType)
                {
                    return;
                }

                var typeSymbol = (INamedTypeSymbol)symbol;
                if (Equals(typeSymbol, _xmlTypes.XmlDocument) ||
                    !typeSymbol.DerivesFrom(_xmlTypes.XmlDocument, baseTypesOnly: true))
                {
                    return;
                }

                bool explicitlyDeclared = true;

                if (typeSymbol.Constructors.Length == 1)
                {
                    IMethodSymbol constructor = typeSymbol.Constructors[0];
                    explicitlyDeclared = !constructor.IsImplicitlyDeclared;

                    if (!explicitlyDeclared)
                    {
                        context.ReportDiagnostic(
                            typeSymbol.CreateDiagnostic(
                                RuleDoNotUseInsecureDtdProcessingInApiDesign,
                                SecurityDiagnosticHelpers.GetLocalizableResourceString(
                                    nameof(MicrosoftNetFrameworkAnalyzersResources.XmlDocumentDerivedClassNoConstructorMessage),
                                    typeSymbol.Name
                                )
                            )
                        );
                    }
                }

                _xmlDocumentDerivedTypes.AddOrUpdate(typeSymbol, explicitlyDeclared, (k, v) => explicitlyDeclared);
            }

            // report warning if no explicit definition of constructor in XmlTextReader derived types
            private void AnalyzeSymbolForXmlTextReaderDerivedType(SymbolAnalysisContext context)
            {
                ISymbol symbol = context.Symbol;
                if (symbol.Kind != SymbolKind.NamedType)
                {
                    return;
                }

                var typeSymbol = (INamedTypeSymbol)symbol;
                if (Equals(typeSymbol, _xmlTypes.XmlTextReader) ||
                    !typeSymbol.DerivesFrom(_xmlTypes.XmlTextReader, baseTypesOnly: true))
                {
                    return;
                }

                bool explicitlyDeclared = true;

                if (typeSymbol.Constructors.Length == 1)
                {
                    IMethodSymbol constructor = typeSymbol.Constructors[0];
                    explicitlyDeclared = !constructor.IsImplicitlyDeclared;

                    if (!explicitlyDeclared && !_isFrameworkSecure)
                    {
                        context.ReportDiagnostic(
                            typeSymbol.CreateDiagnostic(
                                RuleDoNotUseInsecureDtdProcessingInApiDesign,
                                SecurityDiagnosticHelpers.GetLocalizableResourceString(
                                    nameof(MicrosoftNetFrameworkAnalyzersResources.XmlTextReaderDerivedClassNoConstructorMessage),
                                    symbol.Name
                                )
                            )
                        );
                    }
                }

                _xmlTextReaderDerivedTypes.AddOrUpdate(typeSymbol, explicitlyDeclared,
                    (k, v) => explicitlyDeclared);
            }

            private static bool IsAssigningIntendedValueToPropertyDerivedFromType(
                IAssignmentOperation assignmentOperation,
                Func<IPropertySymbol?, bool> isTargetPropertyFunc,
                Func<IOperation, bool> isIntendedValueFunc,
                out bool isTargetProperty)
            {
                var propertyReference = assignmentOperation.Target as IPropertyReferenceOperation;
                isTargetProperty = isTargetPropertyFunc(propertyReference?.Property);
                if (!isTargetProperty)
                {
                    return false;
                }

                RoslynDebug.Assert(propertyReference != null);
                // call to isIntendedValueFunc must be after checking isTargetProperty
                // since the logic of isIntendedValueFunc relies on corresponding SyntaxNode
                bool isIntendedValue = isIntendedValueFunc(assignmentOperation.Value);

                // Here's an example that needs some extra check:
                //
                //    class TestClass : XmlDocument
                //    {
                //        private XmlDocument doc = new XmlDocument();
                //        public TestClass(XmlDocument doc)
                //        {
                //            this.doc.XmlResolver = null;
                //        }
                //    }
                //
                // Even though the assignment would return true for both isTargetPropertyFunc and isIntendedValueFunc,
                // it is not setting the actual property for this class.

                // The goal is to find all assignment like in the example above, "this.xxx.xxx.Property = ...;".
                if (propertyReference.Instance == null
                    || propertyReference.Instance.IsImplicit)
                {
                    //stop here, to avoid false positive, as long as there's one setting <Property> to secure value, we are happy
                    return isIntendedValue;
                }

                if (propertyReference.Instance.Kind != OperationKind.InstanceReference)
                {
                    return false;
                }

                // stop here, same reason as stated above
                return isTargetProperty && isIntendedValue;
            }
        }

        private class ThreadSafeBoolean
        {
            private const int TRUE_VALUE = 1;
            private const int FALSE_VALUE = 0;
            private int zeroOrOne = FALSE_VALUE;

            public ThreadSafeBoolean(bool initialValue = false)
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
