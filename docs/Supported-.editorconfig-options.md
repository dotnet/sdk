# Supported .editorconfig options
The dotnet-format global tool supports the core set of [EditorConfig options](https://github.com/editorconfig/editorconfig/wiki/EditorConfig-Properties)* as well as the [.NET coding convention settings for EditorConfig](https://docs.microsoft.com/en-us/visualstudio/ide/editorconfig-code-style-settings-reference?view=vs-2019)**.

## Core options
- indent_style
- indent_size
- tab_width
- end_of_line
- charset
- insert_final_newline
- root

[*] The options `trim_trailing_whitespace` and `max_line_length` are not supported. Currently insignificant whitespace is **always** removed by the formatter.

[**] [Formatting conventions](https://docs.microsoft.com/en-us/visualstudio/ide/editorconfig-formatting-conventions?view=vs-2019) are enforced by default. Use the `--fix-style` option to enforce [Language conventions](https://docs.microsoft.com/en-us/visualstudio/ide/editorconfig-language-conventions?view=vs-2019) and [Naming conventions](https://docs.microsoft.com/en-us/visualstudio/ide/editorconfig-naming-conventions?view=vs-2019).
