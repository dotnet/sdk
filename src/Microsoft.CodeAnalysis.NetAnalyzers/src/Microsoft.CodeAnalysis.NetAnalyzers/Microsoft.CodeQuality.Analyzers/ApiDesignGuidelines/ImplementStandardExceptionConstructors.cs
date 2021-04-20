// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1032 - redefined: Implement standard exception constructors
    /// Cause: A type extends System.Exception and does not declare all the required constructors.
    /// Description: Exception types must implement the following constructors. Failure to provide the full set of constructors can make it difficult to correctly handle exceptions
    /// For CSharp, example when type name is GoodException
    ///     public GoodException()
    ///     public GoodException(string)
    ///     public GoodException(string, Exception)
    /// For Basic, example
    ///     Sub New()
    ///     Sub New(message As String)
    ///     Sub New(message As String, innerException As Exception)
    /// Redefined - because, in the original FxCop rule, it was also checking for a 4th constructor, as listed below, which after discussion with Sri was decided as covered by other analyzers
    ///     protected or private NewException(SerializationInfo, StreamingContext)
    /// </summary>

    public abstract class ImplementStandardExceptionConstructorsAnalyzer : DiagnosticAnalyzer
    {
        internal enum MissingCtorSignature { CtorWithNoParameter, CtorWithStringParameter, CtorWithStringAndExceptionParameters }

        internal const string RuleId = "CA1032";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ImplementStandardExceptionConstructorsTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageMissingConstructor = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ImplementStandardExceptionConstructorsMessageMissingConstructor), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ImplementStandardExceptionConstructorsDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor MissingConstructorRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageMissingConstructor,
                                                                             DiagnosticCategory.Design,
                                                                             RuleLevel.Disabled,    // Need to validate with other parameter requirements and consider code coverage scenarios.
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(MissingConstructorRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(AnalyzeCompilationSymbol);
        }

        //abstract methods, which the language specific analyzers implements - these will return the required constructor method signatures for CSharp/Basic
        protected abstract string GetConstructorSignatureNoParameter(ISymbol symbol);
        protected abstract string GetConstructorSignatureStringTypeParameter(ISymbol symbol);
        protected abstract string GetConstructorSignatureStringAndExceptionTypeParameter(ISymbol symbol);

        private void AnalyzeCompilationSymbol(CompilationStartAnalysisContext context)
        {
            var exceptionType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemException);
            if (exceptionType == null)
            {
                return;
            }

            // Analyze named types
            context.RegisterSymbolAction(symbolContext =>
            {
                var namedTypeSymbol = (INamedTypeSymbol)symbolContext.Symbol;

                //Check if type derives from Exception type
                if (namedTypeSymbol.DerivesFrom(exceptionType))
                {
                    //Set flags for the 3 different constructos, that is being searched for
                    var defaultConstructorFound = false; //flag for default constructor
                    var secondConstructorFound = false; //flag for constructor with string type parameter
                    var thirdConstructorFound = false; //flag for constructor with string and exception type parameter

                    foreach (IMethodSymbol ctor in namedTypeSymbol.Constructors)
                    {
                        var parameters = ctor.GetParameters();

                        //case 1: Default constructor - no parameters
                        if (parameters.IsEmpty)
                        {
                            defaultConstructorFound = true;
                        }
                        //case 2: Constructor with string type parameter
                        else if (parameters.Length == 1 && parameters[0].Type.SpecialType == SpecialType.System_String)
                        {
                            secondConstructorFound = true;
                        }
                        //case 3: Constructor with string type and exception type parameter
                        else if (parameters.Length == 2 && parameters[0].Type.SpecialType == SpecialType.System_String &&
                                parameters[1].Type.Equals(exceptionType))
                        {
                            thirdConstructorFound = true;
                        }

                        if (defaultConstructorFound && secondConstructorFound && thirdConstructorFound)
                        {
                            //reaches here only when all 3 constructors are found - no diagnostic needed
                            return;
                        }
                    } //end of for loop

                    if (!defaultConstructorFound) //missing default constructor
                    {
                        ReportDiagnostic(symbolContext, namedTypeSymbol, MissingCtorSignature.CtorWithNoParameter, GetConstructorSignatureNoParameter(namedTypeSymbol));
                    }

                    if (!secondConstructorFound) //missing constructor with string parameter
                    {
                        ReportDiagnostic(symbolContext, namedTypeSymbol, MissingCtorSignature.CtorWithStringParameter, GetConstructorSignatureStringTypeParameter(namedTypeSymbol));
                    }

                    if (!thirdConstructorFound) //missing constructor with string and exception type parameter - report diagnostic
                    {
                        ReportDiagnostic(symbolContext, namedTypeSymbol, MissingCtorSignature.CtorWithStringAndExceptionParameters, GetConstructorSignatureStringAndExceptionTypeParameter(namedTypeSymbol));
                    }
                }
            }, SymbolKind.NamedType);
        }

        private static void ReportDiagnostic(SymbolAnalysisContext context, INamedTypeSymbol namedTypeSymbol, MissingCtorSignature missingCtorSignature, string constructorSignature)
        {
            //store MissingCtorSignature enum type into dictionary, to set diagnostic property. This is needed because Diagnostic is immutable
            ImmutableDictionary<string, string?>.Builder builder = ImmutableDictionary.CreateBuilder<string, string?>();
            builder.Add("Signature", missingCtorSignature.ToString());

            //create dignostic and store signature into diagnostic property for fixer
            Diagnostic diagnostic = namedTypeSymbol.Locations.CreateDiagnostic(MissingConstructorRule, builder.ToImmutableDictionary(), namedTypeSymbol.Name, constructorSignature);

            //report diagnostic
            context.ReportDiagnostic(diagnostic);
        }
    }
}