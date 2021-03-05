# Contribution Guidelines

## Guidelines for contributing a new Code Analysis (CA) rule to the repo

1. File an issue describing your proposed rule prior to working on a PR. This will ensure that the rule gets triaged and there is no duplicate work involved from an existing rule OR another contributor working on a similar rule.
   1. For .NET API related analyzer suggestions, please open an issue at [dotnet/runtime/issues](https://github.com/dotnet/runtime/issues?q=is%3Aissue+is%3Aopen+sort%3Aupdated-desc) with [code-analyzer](https://github.com/dotnet/runtime/issues?q=is%3Aopen+is%3Aissue+label%3Acode-analyzer+sort%3Aupdated-desc) label.
   2. For non-API related analyzer suggestions, please open an issue in this repo over [here](https://github.com/dotnet/roslyn-analyzers/issues/new?template=suggest-a-new-rule.md).

2. Newly proposed rule would be tagged with [Needs-Review](https://github.com/dotnet/roslyn-analyzers/labels/Needs-Review) label. An [Approved-Rule](https://github.com/dotnet/roslyn-analyzers/labels/Approved-Rule) label indicates that the proposal has been reviewed and a PR to implement the rule would be accepted.

3. Follow the below steps to choose the appropriate **rule ID** for the new rule:

   1. Choose the **applicable 'category'** for the new rule. See [DiagnosticCategoryAndIdRanges.txt](.//src//Utilities//Compiler//DiagnosticCategoryAndIdRanges.txt) for current diagnostic categories, and the CA IDs currently in use for each category.
   2. Choose the **next available CA ID** for the chosen 'category' from [DiagnosticCategoryAndIdRanges.txt](.//src//Utilities//Compiler//DiagnosticCategoryAndIdRanges.txt).
      For example, while adding a new rule in the `Performance` category, if `CA1800-CA1829` represents the current CA ID range in `DiagnosticCategoryAndIdRanges.txt`, then:
      1. Choose `CA1830` as the rule ID for your rule.
      2. Update the range for `Performance` in [DiagnosticCategoryAndIdRanges.txt](.//src//Utilities//Compiler//DiagnosticCategoryAndIdRanges.txt) to `CA1800-CA1830`

   You can refer to the [official documentation](https://docs.microsoft.com/visualstudio/code-quality/code-analysis-for-managed-code-warnings) for all released CA rules by rule category.

4. Documentation requirements:
   1. **New CA rule must be documented**: Each rule ID `CAxxxx` is automatically assigned the help link `https://docs.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/caxxxx`. The documentation for this page is populated from `caxxxx.md` file at [dotnet/docs quality-rules folder](https://github.com/dotnet/docs/tree/main/docs/fundamentals/code-analysis/quality-rules). For example, `CA1000` is documented at [ca1000.md](https://github.com/dotnet/docs/tree/main/docs/fundamentals/code-analysis/quality-rules/ca1000.md) file. Documenting a new rule is primarily ensuring a PR is sent to `dotnet/docs` repo to add `caxxxx.md` file for the new rule. Detailed steps are given below.
   2. **Documentation PR must be submitted within ONE WEEK of the rule implementation being merged**. Note that we will communicate this requirement on each PR contributing a new CA rule. We reserve the right to revert the rule implementation PR if this documentation requirement is not met.

## Guidelines for creating documentation PR

1. Documentation PR must be submitted to the [dotnet/docs](https://github.com/dotnet/docs) repo:

   Please review [Contribute docs for .NET code analysis rules to the .NET docs repository](https://docs.microsoft.com/contribute/dotnet/dotnet-contribute-code-analysis) for guidelines.

If for some exceptional reason you are unable to submit a PR, please [file a documentation issue](https://github.com/dotnet/docs/issues) to add documentation for the rule in future. Please include all relevant information in the issue to allow the documentation experts to easily author the documentation.
