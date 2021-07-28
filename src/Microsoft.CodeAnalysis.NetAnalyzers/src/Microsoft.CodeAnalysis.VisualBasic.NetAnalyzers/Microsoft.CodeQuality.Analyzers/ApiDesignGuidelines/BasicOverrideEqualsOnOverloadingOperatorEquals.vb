' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports Analyzer.Utilities
Imports Analyzer.Utilities.Extensions
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeQuality.Analyzers

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines
    ''' <summary>
    ''' CA2224: Override Equals on overloading operator equals
    ''' </summary>
    ''' <remarks>
    ''' CA2224 is not applied to C# since it already reports CS0660.
    ''' </remarks>
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicOverrideEqualsOnOverloadingOperatorEqualsAnalyzer
        Inherits DiagnosticAnalyzer

        Friend Const RuleId As String = "CA2224"

        Private Shared ReadOnly s_localizableTitle As LocalizableString = New LocalizableResourceString(NameOf(MicrosoftCodeQualityAnalyzersResources.OverrideEqualsOnOverloadingOperatorEqualsTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, GetType(MicrosoftCodeQualityAnalyzersResources))

        Private Shared ReadOnly s_localizableMessage As LocalizableString = New LocalizableResourceString(NameOf(MicrosoftCodeQualityAnalyzersResources.OverrideEqualsOnOverloadingOperatorEqualsMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, GetType(MicrosoftCodeQualityAnalyzersResources))
        Private Shared ReadOnly s_localizableDescription As LocalizableString = New LocalizableResourceString(NameOf(MicrosoftCodeQualityAnalyzersResources.OverrideEqualsOnOverloadingOperatorEqualsDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, GetType(MicrosoftCodeQualityAnalyzersResources))

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
                    Dim method = DirectCast(symbolContext.Symbol, IMethodSymbol)
                    Debug.Assert(method.IsDefinition)

                    Dim type = method.ContainingType
                    If type.TypeKind = TypeKind.Interface OrElse type.IsImplicitClass OrElse type.SpecialType = SpecialType.System_Object Then
                        ' Don't apply this rule to interfaces, the implicit class (i.e. error case), or System.Object.
                        Return
                    End If

                    ' If there's a = operator...
                    If method.MethodKind <> MethodKind.UserDefinedOperator OrElse
                        Not CaseInsensitiveComparison.Equals(method.Name, WellKnownMemberNames.EqualityOperatorName) Then

                        Return
                    End If

                    ' ...search for a corresponding Equals override.
                    If type.OverridesEquals() Then
                        Return
                    End If

                    symbolContext.ReportDiagnostic(type.CreateDiagnostic(Rule))
                End Sub,
                SymbolKind.Method)
        End Sub
    End Class
End Namespace