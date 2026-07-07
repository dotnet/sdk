# Supported .editorconfig options
The dotnet-format global tool supports the core set of [EditorConfig options](https://github.com/editorconfig/editorconfig/wiki/EditorConfig-Properties)* as well as the [.NET coding convention settings for EditorConfig](https://docs.microsoft.com/en-us/visualstudio/ide/editorconfig-code-style-settings-reference?view=vs-2019).

## Core options
- indent_style
- indent_size
- tab_width
- end_of_line
- charset
- insert_final_newline
- root

[*] The options `trim_trailing_whitespace` and `max_line_length` are not supported. Currently insignificant whitespace is **always** removed by the formatter.

## Removing unnecessary imports
In order to remove unnecessary imports the IDE0005 (unnecessary import) diagnostic id must be configured in your .editorconfig. When running dotnet-format specify a severity that includes the configured IDE0005 severity.

*Example:*

.editorconfig
```ini
root = true

[*.{cs,vb}]
dotnet_diagnostic.IDE0005.severity = warning
```

command
```console
dotnet format ./format.sln --severity warn
```