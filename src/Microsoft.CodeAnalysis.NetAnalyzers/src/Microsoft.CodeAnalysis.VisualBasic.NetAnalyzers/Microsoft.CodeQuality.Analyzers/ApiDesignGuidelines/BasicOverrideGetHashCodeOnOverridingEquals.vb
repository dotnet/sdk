' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports Analyzer.Utilities
Imports Analyzer.Utilities.Extensions
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeQuality.Analyzers

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines
    ''' <summary>
    ''' CA2218: Override GetHashCode on overriding Equals
    ''' </summary>
    ''' <remarks>
    ''' CA2218 is not applied to C# since it already reports CS0569.
    ''' </remarks>
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicOverrideGetHashCodeOnOverridingEqualsAnalyzer
        Inherits DiagnosticAnalyzer

        Friend Const RuleId As String = "CA2218"

        Private Shared ReadOnly s_localizableTitle As LocalizableString = New LocalizableResourceString(NameOf(MicrosoftCodeQualityAnalyzersResources.OverrideGetHashCodeOnOverridingEqualsTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, GetType(MicrosoftCodeQualityAnalyzersResources))

        Private Shared ReadOnly s_localizableMessage As LocalizableString = New LocalizableResourceString(NameOf(MicrosoftCodeQualityAnalyzersResources.OverrideGetHashCodeOnOverridingEqualsMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, GetType(MicrosoftCodeQualityAnalyzersResources))
        Private Shared ReadOnly s_localizableDescription As LocalizableString = New LocalizableResourceString(NameOf(MicrosoftCodeQualityAnalyzersResources.OverrideGetHashCodeOnOverridingEqualsDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, GetType(MicrosoftCodeQualityAnalyzersResources))

        Friend Shared Rule As DiagnosticDescriptor = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableMessage,
            DiagnosticCategory.Usage,
            RuleLevel.IdeSuggestion,
            s_localizableDescription,
            isPortedFxCopRule:=True,
            isDataflowRule:=False)

        Public Overrides ReadOnly Property SupportedDiagnostics As ImmutableArray(Of DiagnosticDescriptor) = ImmutableArray.Create(Rule)

        Public Overrides Sub Initialize(context As AnalysisContext)
            context.EnableConcurrentExecution()
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)

            context.RegisterSymbolAction(
                Sub(symbolContext)
                    Dim type = DirectCast(symbolContext.Symbol, INamedTypeSymbol)
                    Debug.Assert(type.IsDefinition)

                    If type.TypeKind = TypeKind.Interface OrElse type.IsImplicitClass OrElse type.SpecialType = SpecialType.System_Object Then
                        ' Don't apply this rule to interfaces, the implicit class (i.e. error case), or System.Object.
                        Return
                    End If

                    If type.OverridesEquals() AndAlso Not type.OverridesGetHashCode() Then
                        symbolContext.ReportDiagnostic(type.CreateDiagnostic(Rule))
                    End If
                End Sub,
                SymbolKind.NamedType)
        End Sub

    End Class
End Namespace