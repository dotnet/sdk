There are 3 sections of editorconfig configuration options: Formatting, Code Style, and Naming. For the most part the Formatting options are covered by the Roslyn Formatter, which leaves the Code Style and Naming options to be implemented. Below is a list of Code Style options. Naming options will be handled separately.

# .NET code style settings

## "This." and "Me." qualifiers
Name | Base Class | Option | Diagnostic 
-|-|-|-
dotnet_style_qualification_for_field | AbstractQualifyMemberAccessDiagnosticAnalyzer | CodeStyleOptions.QualifyFieldAccess | AddQualificationDiagnosticId = "IDE0009"
dotnet_style_qualification_for_property | AbstractQualifyMemberAccessDiagnosticAnalyzer | CodeStyleOptions.QualifyPropertyAccess | AddQualificationDiagnosticId = "IDE0009"
dotnet_style_qualification_for_method | AbstractQualifyMemberAccessDiagnosticAnalyzer | CodeStyleOptions.QualifyMethodAccess | AddQualificationDiagnosticId = "IDE0009"
dotnet_style_qualification_for_event | AbstractQualifyMemberAccessDiagnosticAnalyzer | CodeStyleOptions.QualifyEventAccess | AddQualificationDiagnosticId = "IDE0009"

## Language keywords instead of framework type names for type references
Name | Base Class | Option |  Diagnostic 
-|-|-|-
dotnet_style_predefined_type_for_locals_parameters_members |  PreferFrameworkTypeDiagnosticAnalyzerBase | CodeStyleOptions.PreferIntrinsicPredefinedTypeKeywordInDeclaration | PreferBuiltInOrFrameworkTypeDiagnosticId = "IDE0049"
dotnet_style_predefined_type_for_member_access | PreferFrameworkTypeDiagnosticAnalyzerBase | CodeStyleOptions.PreferIntrinsicPredefinedTypeKeywordInMemberAccess | PreferBuiltInOrFrameworkTypeDiagnosticId = "IDE0049"

## Modifier preferences
Name | Base Class | Option | Diagnostic 
-|-|-|-
dotnet_style_require_accessibility_modifiers | AbstractAddAccessibilityModifiersDiagnosticAnalyzer | CodeStyleOptions.RequireAccessibilityModifiers | AddAccessibilityModifiersDiagnosticId = "IDE0040"
csharp_preferred_modifier_order | AbstractOrderModifiersDiagnosticAnalyzer | CSharpCodeStyleOptions.PreferredModifierOrder | OrderModifiersDiagnosticId = "IDE0036"
visual_basic_preferred_modifier_order | AbstractOrderModifiersDiagnosticAnalyzer | VisualBasicCodeStyleOptions.PreferredModifierOrder | OrderModifiersDiagnosticId = "IDE0036"
dotnet_style_readonly_field | MakeFieldReadonlyDiagnosticAnalyzer | CodeStyleOption.PreferReadonly | MakeFieldReadonlyDiagnosticId = "IDE0044"

## Parentheses preferences
Name | Base Class | Option | Diagnostic 
-|-|-|-
dotnet_style_parentheses_in_arithmetic_binary_operators | AbstractAddRequiredParenthesesDiagnosticAnalyzer<br>AbstractRemoveUnnecessaryParenthesesDiagnosticAnalyzer | CodeStyleOption.ArithmeticBinaryParentheses | AddRequiredParenthesesDiagnosticId = "IDE0048"<br>RemoveUnnecessaryParenthesesDiagnosticId = "IDE0047"
dotnet_style_parentheses_in_other_binary_operators | AbstractAddRequiredParenthesesDiagnosticAnalyzer<br>AbstractRemoveUnnecessaryParenthesesDiagnosticAnalyzer | CodeStyleOptions.OtherBinaryParentheses | AddRequiredParenthesesDiagnosticId = "IDE0048"<br>RemoveUnnecessaryParenthesesDiagnosticId = "IDE0047"
dotnet_style_parentheses_in_other_operators | AbstractAddRequiredParenthesesDiagnosticAnalyzer<br>AbstractRemoveUnnecessaryParenthesesDiagnosticAnalyzer | CodeStyleOptions.OtherParentheses | AddRequiredParenthesesDiagnosticId = "IDE0048"<br>RemoveUnnecessaryParenthesesDiagnosticId = "IDE0047"
dotnet_style_parentheses_in_relational_binary_operators | AbstractAddRequiredParenthesesDiagnosticAnalyzer<br>AbstractRemoveUnnecessaryParenthesesDiagnosticAnalyzer | CodeStyleOptions.RelationalBinaryParentheses | AddRequiredParenthesesDiagnosticId = "IDE0048"<br>RemoveUnnecessaryParenthesesDiagnosticId = "IDE0047"

## Expression-level preferences
Name | Base Class | Option | Diagnostic 
-|-|-|-
dotnet_style_object_initializer | AbstractUseObjectInitializerDiagnosticAnalyzer | CodeStyleOptions.PreferObjectInitializer | UseObjectInitializerDiagnosticId = "IDE0017"
dotnet_style_collection_initializer | AbstractUseCollectionInitializerDiagnosticAnalyzer | CodeStyleOptions.PreferCollectionInitializer | UseCollectionInitializerDiagnosticId = "IDE0028"
dotnet_style_explicit_tuple_names | UseExplicitTupleNameDiagnosticAnalyzer | CodeStyleOptions.PreferExplicitTupleNames | UseExplicitTupleNameDiagnosticId = "IDE0033"
dotnet_style_prefer_inferred_tuple_names | AbstractUseInferredMemberNameDiagnosticAnalyzer | CodeStyleOptions.PreferInferredTupleNames | UseInferredMemberNameDiagnosticId = "IDE0037"
dotnet_style_prefer_inferred_anonymous_type_member_names | AbstractUseInferredMemberNameDiagnosticAnalyzer | CodeStyleOptions.PreferInferredAnonymousTypeMemberNames | UseInferredMemberNameDiagnosticId = "IDE0037"
dotnet_style_prefer_auto_properties | AbstractUseAutoPropertyAnalyzer | CodeStyleOptions.PreferAutoProperties | UseAutoPropertyDiagnosticId = "IDE0032"
dotnet_style_prefer_is_null_check_over_reference_equality_method | AbstractUseIsNullCheckForReferenceEqualsDiagnosticAnalyzer | CodeStyleOptions.PreferIsNullCheckOverReferenceEqualityMethod | UseIsNullCheckDiagnosticId = "IDE0041"
dotnet_style_prefer_conditional_expression_over_assignment | AbstractUseConditionalExpressionForAssignmentDiagnosticAnalyzer | CodeStyleOptions.PreferConditionalExpressionOverAssignment | UseConditionalExpressionForAssignmentDiagnosticId = "IDE0045"
dotnet_style_prefer_conditional_expression_over_return | AbstractUseConditionalExpressionForReturnDiagnosticAnalyzer | CodeStyleOptions.PreferConditionalExpressionOverReturn | UseConditionalExpressionForReturnDiagnosticId = "IDE0046"
dotnet_style_prefer_compound_assignment | AbstractUseCompoundAssignmentDiagnosticAnalyzer | CodeStyleOptions.PreferCompoundAssignment | UseCompoundAssignmentDiagnosticId = "IDE0054"

## "Null" checking preferences
Name | Base Class | Option | Diagnostic 
-|-|-|-
dotnet_style_coalesce_expression | AbstractUseCoalesceExpressionDiagnosticAnalyzer | CodeStyleOptions.PreferCoalesceExpression | UseCoalesceExpressionDiagnosticId = "IDE0029"
dotnet_style_null_propagation | AbstractUseNullPropagationDiagnosticAnalyzer | CodeStyleOptions.PreferNullPropagation | UseNullPropagationDiagnosticId = "IDE0031"

# C# code style settings

## Implicit and explicit types
Name | Base Class | Option | Diagnostic 
-|-|-|-
csharp_style_var_for_built_in_types | CSharpUseExplicitTypeDiagnosticAnalyzer<br>CSharpUseImplicitTypeDiagnosticAnalyzer | CSharpCodeStyleOptions.VarForBuiltInTypes | UseExplicitTypeDiagnosticId = "IDE0008"<br>UseImplicitTypeDiagnosticId = "IDE0007"
csharp_style_var_when_type_is_apparent | CSharpUseExplicitTypeDiagnosticAnalyzer<br>CSharpUseImplicitTypeDiagnosticAnalyzer | CSharpCodeStyleOptions.VarWhenTypeIsApparent | UseExplicitTypeDiagnosticId = "IDE0008"<br>UseImplicitTypeDiagnosticId = "IDE0007"
csharp_style_var_elsewhere | CSharpUseExplicitTypeDiagnosticAnalyzer<br>CSharpUseImplicitTypeDiagnosticAnalyzer | CSharpCodeStyleOptions.VarElsewhere | UseExplicitTypeDiagnosticId = "IDE0008"<br>UseImplicitTypeDiagnosticId = "IDE0007"

## Expression-bodied members
Name | Base Class | Option | Diagnostic 
-|-|-|-
csharp_style_expression_bodied_methods | UseExpressionBodyForMethodsHelper | CSharpCodeStyleOptions.PreferExpressionBodiedMethods | UseExpressionBodyForMethodsDiagnosticId = "IDE0022"
csharp_style_expression_bodied_constructors | UseExpressionBodyForConstructorsHelper | CSharpCodeStyleOptions.PreferExpressionBodiedConstructors | UseExpressionBodyForConstructorsDiagnosticId = "IDE0021"
csharp_style_expression_bodied_operators | UseExpressionBodyForOperatorsHelper | CSharpCodeStyleOptions.PreferExpressionBodiedOperators | UseExpressionBodyForOperatorsDiagnosticId = "IDE0024"
csharp_style_expression_bodied_properties | UseExpressionBodyForPropertiesHelper | CSharpCodeStyleOptions.PreferExpressionBodiedProperties | UseExpressionBodyForPropertiesDiagnosticId = "IDE0025"
csharp_style_expression_bodied_indexers | UseExpressionBodyForIndexersHelper | CSharpCodeStyleOptions.PreferExpressionBodiedIndexers | UseExpressionBodyForIndexersDiagnosticId = "IDE0026"
csharp_style_expression_bodied_accessors | UseExpressionBodyForAccessorsHelper | CSharpCodeStyleOptions.PreferExpressionBodiedAccessors | UseExpressionBodyForAccessorsDiagnosticId = "IDE0027"
csharp_style_expression_bodied_lambdas | UseExpressionBodyForLambdaDiagnosticAnalyzer | CSharpCodeStyleOptions.PreferExpressionBodiedLambdas | UseExpressionBodyForLambdaExpressionsDiagnosticId = "IDE0053"
csharp_style_expression_bodied_local_functions | UseExpressionBodyForLocalFunctionHelper | CSharpCodeStyleOptions.PreferExpressionBodiedLocalFunctions | UseExpressionBodyForLocalFunctionsDiagnosticId = "IDE0061"

## Pattern matching
Name | Base Class | Option | Diagnostic 
-|-|-|-
csharp_style_pattern_matching_over_is_with_cast_check | CSharpIsAndCastCheckDiagnosticAnalyzer | CSharpCodeStyleOptions.PreferPatternMatchingOverIsWithCastCheck | InlineIsTypeCheckId = "IDE0020"
csharp_style_pattern_matching_over_as_with_null_check | CSharpAsAndNullCheckDiagnosticAnalyzer | CSharpCodeStyleOptions.PreferPatternMatchingOverAsWithNullCheck | InlineAsTypeCheckId = "IDE0019"
csharp_style_prefer_switch_expression | ConvertSwitchStatementToExpressionDiagnosticAnalyzer | CSharpCodeStyleOptions.PreferSwitchExpression | ConvertSwitchStatementToExpressionDiagnosticId = "IDE0066"

## Inlined variable declarations
Name | Base Class | Option | Diagnostic 
-|-|-|-
csharp_style_inlined_variable_declaration | CSharpInlineDeclarationDiagnosticAnalyzer | CodeStyleOptions.PreferInlinedVariableDeclaration | InlineDeclarationDiagnosticId = "IDE0018"

## Expression-level preferences
Name | Base Class | Option | Diagnostic 
-|-|-|-
csharp_prefer_simple_default_expression | CSharpUseDefaultLiteralDiagnosticAnalyzer | CSharpCodeStyleOptions.UseDefaultLiteralDiagnosticId | UseDefaultLiteralDiagnosticId = "IDE0034"
csharp_style_deconstructed_variable_declaration | CSharpUseDeconstructionDiagnosticAnalyzer | CodeStyleOptions.PreferDeconstructedVariableDeclaration | UseDeconstructionDiagnosticId = "IDE0042"
csharp_style_pattern_local_over_anonymous_function | CSharpUseLocalFunctionDiagnosticAnalyzer | CSharpCodeStyleOptions.PreferLocalOverAnonymousFunction | UseLocalFunctionDiagnosticId = "IDE0039"
csharp_style_prefer_index_operator | CSharpUseIndexOperatorDiagnosticAnalyzer | CSharpCodeStyleOptions.PreferIndexOperator | UseIndexOperatorDiagnosticId = "IDE0056"
csharp_style_prefer_range_operator | CSharpUseRangeOperatorDiagnosticAnalyzer | CSharpCodeStyleOptions.PreferRangeOperator | UseRangeOperatorDiagnosticId = "IDE0057"
csharp_prefer_static_local_function | MakeLocalFunctionStaticDiagnosticAnalyzer | CSharpCodeStyleOptions.PreferStaticLocalFunction | MakeLocalFunctionStaticDiagnosticId = "IDE0062"
csharp_prefer_simple_using_statement | UseSimpleUsingStatementDiagnosticAnalyzer | CSharpCodeStyleOptions.PreferSimpleUsingStatement | UseSimpleUsingStatementDiagnosticId = "IDE0063"

## "Null" checking preferences
Name | Base Class | Option | Diagnostic 
-|-|-|-
csharp_style_throw_expression | AbstractUseThrowExpressionDiagnosticAnalyzer | CodeStyleOptions.PreferThrowExpression | UseThrowExpressionDiagnosticId = "IDE0016"
csharp_style_conditional_delegate_call | InvokeDelegateWithConditionalAccessAnalyzer | CSharpCodeStyleOptions.PreferConditionalDelegateCall | InvokeDelegateWithConditionalAccessId = "IDE1005"

## Code block preferences
Name | Base Class | Option | Diagnostic 
-|-|-|-
csharp_prefer_braces | CSharpAddBracesDiagnosticAnalyzer | CSharpCodeStyleOptions.PreferBraces | AddBracesDiagnosticId = "IDE0011"