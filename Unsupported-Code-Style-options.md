There are 3 sections of editorconfig configuration options: Formatting, Code Style, and Naming. For the most part the Formatting options are covered by the Roslyn Formatter, which leaves the Code Style and Naming options to be implemented. Below is a *mostly* complete list of Code Style options. Naming options will be handled separately.

# .NET code style settings

## "This." and "Me." qualifiers
Name | Base Class | Diagnostic 
-|-|-
dotnet_style_qualification_for_field | AbstractQualifyMemberAccessDiagnosticAnalyzer | AddQualificationDiagnosticId = "IDE0009"
dotnet_style_qualification_for_property |  AbstractQualifyMemberAccessDiagnosticAnalyzer | AddQualificationDiagnosticId = "IDE0009"
dotnet_style_qualification_for_method | AbstractQualifyMemberAccessDiagnosticAnalyzer | AddQualificationDiagnosticId = "IDE0009"
dotnet_style_qualification_for_event | AbstractQualifyMemberAccessDiagnosticAnalyzer | AddQualificationDiagnosticId = "IDE0009"

## Language keywords instead of framework type names for type references
Name | Base Class | Diagnostic 
-|-|-
dotnet_style_predefined_type_for_locals_parameters_members |  PreferFrameworkTypeDiagnosticAnalyzerBase | PreferBuiltInOrFrameworkTypeDiagnosticId = "IDE0049"
dotnet_style_predefined_type_for_member_access | PreferFrameworkTypeDiagnosticAnalyzerBase | PreferBuiltInOrFrameworkTypeDiagnosticId = "IDE0049"

## Modifier preferences
Name | Base Class | Diagnostic 
-|-|-
dotnet_style_require_accessibility_modifiers | AbstractAddAccessibilityModifiersDiagnosticAnalyzer | AddAccessibilityModifiersDiagnosticId = "IDE0040"
csharp_preferred_modifier_order | AbstractOrderModifiersDiagnosticAnalyzer | OrderModifiersDiagnosticId = "IDE0036"
visual_basic_preferred_modifier_order | AbstractOrderModifiersDiagnosticAnalyzer | OrderModifiersDiagnosticId = "IDE0036"
dotnet_style_readonly_field | MakeFieldReadonlyDiagnosticAnalyzer | MakeFieldReadonlyDiagnosticId = "IDE0044"

## Parentheses preferences
Name | Base Class | Diagnostic 
-|-|-
dotnet_style_parentheses_in_arithmetic_binary_operators | AbstractAddRequiredParenthesesDiagnosticAnalyzer<br>AbstractRemoveUnnecessaryParenthesesDiagnosticAnalyzer | AddRequiredParenthesesDiagnosticId = "IDE0048"<br>RemoveUnnecessaryParenthesesDiagnosticId = "IDE0047"
dotnet_style_parentheses_in_other_binary_operators | AbstractAddRequiredParenthesesDiagnosticAnalyzer<br>AbstractRemoveUnnecessaryParenthesesDiagnosticAnalyzer | AddRequiredParenthesesDiagnosticId = "IDE0048"<br>RemoveUnnecessaryParenthesesDiagnosticId = "IDE0047"
dotnet_style_parentheses_in_other_operators | AbstractAddRequiredParenthesesDiagnosticAnalyzer<br>AbstractRemoveUnnecessaryParenthesesDiagnosticAnalyzer | AddRequiredParenthesesDiagnosticId = "IDE0048"<br>RemoveUnnecessaryParenthesesDiagnosticId = "IDE0047"
dotnet_style_parentheses_in_relational_binary_operators | AbstractAddRequiredParenthesesDiagnosticAnalyzer<br>AbstractRemoveUnnecessaryParenthesesDiagnosticAnalyzer | AddRequiredParenthesesDiagnosticId = "IDE0048"<br>RemoveUnnecessaryParenthesesDiagnosticId = "IDE0047"

## Expression-level preferences
Name | Base Class | Diagnostic 
-|-|-
dotnet_style_object_initializer | AbstractUseObjectInitializerDiagnosticAnalyzer | UseObjectInitializerDiagnosticId = "IDE0017"
dotnet_style_collection_initializer | AbstractUseCollectionInitializerDiagnosticAnalyzer | UseCollectionInitializerDiagnosticId = "IDE0028"
dotnet_style_explicit_tuple_names | UseExplicitTupleNameDiagnosticAnalyzer | UseExplicitTupleNameDiagnosticId = "IDE0033"
dotnet_style_prefer_inferred_tuple_names | AbstractUseInferredMemberNameDiagnosticAnalyzer | UseInferredMemberNameDiagnosticId = "IDE0037"
dotnet_style_prefer_inferred_anonymous_type_member_names | AbstractUseInferredMemberNameDiagnosticAnalyzer | UseInferredMemberNameDiagnosticId = "IDE0037"
dotnet_style_prefer_auto_properties | AbstractUseAutoPropertyAnalyzer | UseAutoPropertyDiagnosticId = "IDE0032"
dotnet_style_prefer_is_null_check_over_reference_equality_method | AbstractUseIsNullCheckForReferenceEqualsDiagnosticAnalyzer | UseIsNullCheckDiagnosticId = "IDE0041"
dotnet_style_prefer_conditional_expression_over_assignment | AbstractUseConditionalExpressionForAssignmentDiagnosticAnalyzer | UseConditionalExpressionForAssignmentDiagnosticId = "IDE0045"
dotnet_style_prefer_conditional_expression_over_return | AbstractUseConditionalExpressionForReturnDiagnosticAnalyzer | UseConditionalExpressionForReturnDiagnosticId = "IDE0046"

## "Null" checking preferences
Name | Base Class | Diagnostic 
-|-|-
dotnet_style_coalesce_expression | AbstractUseCoalesceExpressionDiagnosticAnalyzer | UseCoalesceExpressionDiagnosticId = "IDE0029"
dotnet_style_null_propagation | AbstractUseNullPropagationDiagnosticAnalyzer | UseNullPropagationDiagnosticId = "IDE0031"

# C# code style settings

## Implicit and explicit types
Name | Base Class | Diagnostic 
-|-|-
csharp_style_var_for_built_in_types | CSharpUseExplicitTypeDiagnosticAnalyzer<br>CSharpUseImplicitTypeDiagnosticAnalyzer | UseExplicitTypeDiagnosticId = "IDE0008"<br>UseImplicitTypeDiagnosticId = "IDE0007"
csharp_style_var_when_type_is_apparent | CSharpUseExplicitTypeDiagnosticAnalyzer<br>CSharpUseImplicitTypeDiagnosticAnalyzer | UseExplicitTypeDiagnosticId = "IDE0008"<br>UseImplicitTypeDiagnosticId = "IDE0007"
csharp_style_var_elsewhere | CSharpUseExplicitTypeDiagnosticAnalyzer<br>CSharpUseImplicitTypeDiagnosticAnalyzer | UseExplicitTypeDiagnosticId = "IDE0008"<br>UseImplicitTypeDiagnosticId = "IDE0007"

## Expression-bodied members
Name | Base Class | Diagnostic 
-|-|-
csharp_style_expression_bodied_methods | UseExpressionBodyForMethodsHelper | UseExpressionBodyForMethodsDiagnosticId = "IDE0022"
csharp_style_expression_bodied_constructors | UseExpressionBodyForConstructorsHelper | UseExpressionBodyForConstructorsDiagnosticId = "IDE0021"
csharp_style_expression_bodied_operators | UseExpressionBodyForOperatorsHelper | UseExpressionBodyForOperatorsDiagnosticId = "IDE0024"
csharp_style_expression_bodied_properties | UseExpressionBodyForPropertiesHelper | UseExpressionBodyForPropertiesDiagnosticId = "IDE0025"
csharp_style_expression_bodied_indexers | UseExpressionBodyForIndexersHelper | UseExpressionBodyForIndexersDiagnosticId = "IDE0026"
csharp_style_expression_bodied_accessors | UseExpressionBodyForAccessorsHelper | UseExpressionBodyForAccessorsDiagnosticId = "IDE0027"

## Pattern matching
Name | Base Class | Diagnostic 
-|-|-
csharp_style_pattern_matching_over_is_with_cast_check | CSharpIsAndCastCheckDiagnosticAnalyzer | InlineIsTypeCheckId = "IDE0020"
csharp_style_pattern_matching_over_as_with_null_check | CSharpAsAndNullCheckDiagnosticAnalyzer | InlineAsTypeCheckId = "IDE0019"

## Inlined variable declarations
Name | Base Class | Diagnostic 
-|-|-
csharp_style_inlined_variable_declaration | CSharpInlineDeclarationDiagnosticAnalyzer | InlineDeclarationDiagnosticId = "IDE0018"

## Expression-level preferences
Name | Base Class | Diagnostic 
-|-|-
csharp_prefer_simple_default_expression | CSharpUseDefaultLiteralDiagnosticAnalyzer | UseDefaultLiteralDiagnosticId = "IDE0034"
csharp_style_deconstructed_variable_declaration | CSharpUseDeconstructionDiagnosticAnalyzer | UseDeconstructionDiagnosticId = "IDE0042"
csharp_style_pattern_local_over_anonymous_function | CSharpUseLocalFunctionDiagnosticAnalyzer | UseLocalFunctionDiagnosticId = "IDE0039"

## "Null" checking preferences
Name | Base Class | Diagnostic 
-|-|-
csharp_style_throw_expression | AbstractUseThrowExpressionDiagnosticAnalyzer | UseThrowExpressionDiagnosticId = "IDE0016"
csharp_style_conditional_delegate_call | InvokeDelegateWithConditionalAccessAnalyzer | InvokeDelegateWithConditionalAccessId = "IDE1005"

## Code block preferences
Name | Base Class | Diagnostic 
-|-|-
csharp_prefer_braces | CSharpAddBracesDiagnosticAnalyzer | AddBracesDiagnosticId = "IDE0011"