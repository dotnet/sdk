Guidelines for contributing a new Code Analysis (CA) rule to the repo
=================================================================

1. File an [issue](https://github.com/dotnet/roslyn-analyzers/issues/new) describing your proposed rule prior to working on a PR. This will ensure that the rule gets triaged and there is no duplicate work involved from an existing rule OR another contributor working on a similar rule.

2. Newly proposed rule would be tagged with [Needs-Review](https://github.com/dotnet/roslyn-analyzers/labels/Needs-Review) label. An [Approved-Rule](https://github.com/dotnet/roslyn-analyzers/labels/Approved-Rule) label indicates that the proposal has been reviewed and a PR to implement the rule would be accepted.

3. Follow the below steps to choose the appropriate **rule ID** for the new rule:

   1. Choose the **applicable 'category'** for the new rule. See [DiagnosticCategoryAndIdRanges.txt](.//src//Utilities//Compiler//DiagnosticCategoryAndIdRanges.txt) for current diagnostic categories, and the CA IDs currently in use for each category.
   2. Choose the **next available CA ID** for the chosen 'category' from [DiagnosticCategoryAndIdRanges.txt](.//src//Utilities//Compiler//DiagnosticCategoryAndIdRanges.txt).
      For example, while adding a new rule in the `Performance` category, if `CA1800-CA1829` represents the current CA ID range in `DiagnosticCategoryAndIdRanges.txt`, then:
      1. Choose `CA1830` as the rule ID for your rule.
      2. Update the range for `Performance` in [DiagnosticCategoryAndIdRanges.txt](.//src//Utilities//Compiler//DiagnosticCategoryAndIdRanges.txt) to `CA1800-CA1830`

   You can refer to the [official documentation](https://docs.microsoft.com/visualstudio/code-quality/code-analysis-for-managed-code-warnings) for all released CA rules by rule category.

4. Follow the below guidelines to choose the appropriate **analyzer package** for the new rule:

   1. Read the README section [here](https://github.com/dotnet/roslyn-analyzers#the-following-are-subpackages-or-nuget-dependencies-that-are-automatically-installed-when-you-install-the-microsoftcodeanalysisfxcopanalyzers-package) to get an idea of the content of the analyzer packages in the repo.
   2. For majority of cases, you would be contributing to either [Microsoft.CodeQuality.Analyzers](https://github.com/dotnet/roslyn-analyzers#microsoftcodequalityanalyzers) or [Microsoft.NetCoreAnalyzers](https://github.com/dotnet/roslyn-analyzers#microsoftnetcoreanalyzers). Analyzers related to pure code quality improvements, which are not specific to any API should go into `Microsoft.CodeQuality.Analyzers`. Analyzers specific to usage of a specific .NetCore/.NetStandard API should go into `Microsoft.NetCore.Analyzers` package.
   3. A good rule of thumb is that if your analyzer needs to invoke `GetTypeByMetadataName`, then most likely it is an API specific analyzer and belongs to `Microsoft.NetCore.Analyzers`.

5. **NOTE:** Once the new rule is merged, it needs to be documented. Either submit a PR on the [official documentation page](https://docs.microsoft.com/visualstudio/code-quality/code-analysis-for-managed-code-warnings) for the rule's category (preferred) or [file an issue](https://github.com/MicrosoftDocs/visualstudio-docs/issues). If filing an issue, please include all relevant information in the issue to allow the documentation experts to easily author the documentation. For example, see [this issue](https://github.com/MicrosoftDocs/visualstudio-docs/issues/3454).
