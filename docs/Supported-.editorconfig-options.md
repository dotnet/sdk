The dotnet-format global tool supports the core set of EditorConfig options*:

- indent_style
- indent_size
- tab_width
- end_of_line
- charset
- insert_final_newline
- root

[*] The option trim_trailing_whitespace is not supported. Currently insignificant whitespace is **always** removed by the formatter.

In addition dotnet-format supports a subset of the [.NET coding convention settings for EditorConfig](https://docs.microsoft.com/en-us/visualstudio/ide/editorconfig-code-style-settings-reference?view=vs-2017).

# Formatting conventions
Most of the rules for formatting conventions have the following format:

`rule_name = false|true`

You specify either `true` (prefer this style) or `false` (do not prefer this style). You do not specify a severity. For a few rules, instead of `true` or `false`, you specify other values to describe when and where to apply the rule.

## C# formatting settings
The formatting rules in this section apply only to C# code.

**Newline options**
These formatting rules concern the use of new lines to format code.

- csharp_new_line_before_open_brace (default value: `all`)
- csharp_new_line_before_else (default value: `true`)
- csharp_new_line_before_catch (default value: `true`)
- csharp_new_line_before_finally (default value: `true`)
- csharp_new_line_before_members_in_object_initializers (default value: `true`)
- csharp_new_line_before_members_in_anonymous_types (default value: `true`)
- csharp_new_line_between_query_expression_clauses (default value: `true`)

**Indentation options**
These formatting rules concern the use of indentation to format code.

- csharp_indent_case_contents (default value: `true`)
- csharp_indent_switch_labels (default value: `true`)
- csharp_indent_labels (default value: `no_change`)

**Spacing options**
These formatting rules concern the use of space characters to format code.

- csharp_space_after_cast (default value: `false`)
- csharp_space_after_keywords_in_control_flow_statements (default value: `true`)
- csharp_space_between_method_declaration_parameter_list_parentheses (default value: `false`)
- csharp_space_between_method_call_parameter_list_parentheses (default value: `false`)
- csharp_space_between_parentheses (default value: `false`)
- csharp_space_before_colon_in_inheritance_clause (default value: `true`)
- csharp_space_after_colon_in_inheritance_clause (default value: `true`)
- csharp_space_around_binary_operators (default value: `before_and_after`)
- csharp_space_between_method_declaration_empty_parameter_list_parentheses (default value: `false`)
- csharp_space_between_method_call_name_and_opening_parenthesis (default value: `false`)
- csharp_space_between_method_call_empty_parameter_list_parentheses (default value: `false`)

**Wrapping options**
These formatting rules concern the use of single lines versus separate lines for statements and code blocks.

- csharp_preserve_single_line_statements (default value: `true`)
- csharp_preserve_single_line_blocks (default value: `true`)