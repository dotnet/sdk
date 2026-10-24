using namespace System.Management.Automation
using namespace System.Management.Automation.Language

Register-ArgumentCompleter -Native -CommandName 'mycommand' -ScriptBlock {
    param($wordToComplete, $commandAst, $cursorPosition)

    $commandElements = $commandAst.CommandElements
    $command = @(
        'mycommand'
        for ($i = 1; $i -lt $commandElements.Count; $i++) {
            $element = $commandElements[$i]
            if ($element -isnot [StringConstantExpressionAst] -or
                $element.StringConstantType -ne [StringConstantType]::BareWord -or
                $element.Value.StartsWith('-') -or
                $element.Value -eq $wordToComplete) {
                break
            }
            $element.Value
        }) -join ';'

    $completions = @()
    switch ($command) {
        'mycommand' {
            $staticCompletions = @(
                [CompletionResult]::new('--curly', '--curly', [CompletionResultType]::ParameterName, "Text with `“curly`” and `„low`“ quotes")
                [CompletionResult]::new('--dollar', '--dollar', [CompletionResultType]::ParameterName, "Text with `$dollar sign")
                [CompletionResult]::new('--backtick', '--backtick', [CompletionResultType]::ParameterName, "Text with ``backtick`` char")
                [CompletionResult]::new('--double', '--double', [CompletionResultType]::ParameterName, "Text with `"double`" quote")
                [CompletionResult]::new('--single', '--single', [CompletionResultType]::ParameterName, "Text with 'single' quotes")
                [CompletionResult]::new('subcmd', 'subcmd', [CompletionResultType]::ParameterValue, "Subcmd: `“curly`” `„low`“ `$dollar ``backtick`` `"double`" and 'single'")
            )
            $completions += $staticCompletions
            break
        }
        'mycommand;subcmd' {
            $staticCompletions = @(
            )
            $completions += $staticCompletions
            break
        }
    }
    $completions | Where-Object -FilterScript { $_.CompletionText -like "$wordToComplete*" } | Sort-Object -Property ListItemText
}
