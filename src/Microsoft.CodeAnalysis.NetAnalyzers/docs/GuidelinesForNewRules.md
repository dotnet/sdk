Guidelines for contributing a new Code Analysis (CA) rule to the repo
=================================================================

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

4. Follow the below guidelines to choose the appropriate **analyzer package** for the new rule:

   1. Read the README section [here](https://github.com/dotnet/roslyn-analyzers#the-following-are-subpackages-or-nuget-dependencies-that-are-automatically-installed-when-you-install-the-microsoftcodeanalysisfxcopanalyzers-package) to get an idea of the content of the analyzer packages in the repo.
   2. For majority of cases, you would be contributing to either [Microsoft.CodeQuality.Analyzers](https://github.com/dotnet/roslyn-analyzers#microsoftcodequalityanalyzers) or [Microsoft.NetCoreAnalyzers](https://github.com/dotnet/roslyn-analyzers#microsoftnetcoreanalyzers). Analyzers related to pure code quality improvements, which are not specific to any API should go into `Microsoft.CodeQuality.Analyzers`. Analyzers specific to usage of a specific .NetCore/.NetStandard API should go into `Microsoft.NetCore.Analyzers` package.
   3. A good rule of thumb is that if your analyzer needs to invoke `GetTypeByMetadataName`, then most likely it is an API specific analyzer and belongs to `Microsoft.NetCore.Analyzers`.

5. Documentation requirements:
   1. **New CA rule must be documented**: Each rule ID `CAxxxx` is automatically assigned the help link https://docs.microsoft.com/visualstudio/code-quality/caxxxx. The documentation for this page is populated from `caxxxx.md` file at https://github.com/MicrosoftDocs/visualstudio-docs/tree/master/docs/code-quality. For example, `CA1000` is documented at [ca1000.md](https://github.com/MicrosoftDocs/visualstudio-docs/blob/master/docs/code-quality/ca1000.md) file. Documenting a new rule is primarily ensuring a PR is sent to `MicrosoftDocs` repo to add `caxxxx.md` file for the new rule. Detailed steps are given below.
   2. **Documentation PR must be submitted within ONE WEEK of the rule implementation being merged**. Note that we will communicate this requirement on each PR contributing a new CA rule. We reserve the right to revert the rule implementation PR if this documentation requirement is not met.
   
   Steps for creating documentation PR:
   
   1. Documentation PR must be submitted to the following repo:
      1. _External contributors_: https://github.com/MicrosoftDocs/visualstudio-docs
      2. _Internal contributors_: https://github.com/MicrosoftDocs/visualstudio-docs-pr
      
      Please review [CONTRIBUTING.md](https://github.com/MicrosoftDocs/visualstudio-docs/blob/master/CONTRIBUTING.md) for guidelines.
   2. Documentation PR for a new CA rule must have following changes:
      1. New `caxxxx.md` file under `/docs/code-quality` sub-folder with rule documentation.
         
         `TIP:` Clone an existing `caxxxx.md` file inside `/docs/code-quality` sub-folder in the repo, rename it and update the contents for the new rule.
      2. Update the following tables in the repo for supported CA rule IDs:
         1. Add entry in `/docs/code-quality/toc.yml` under appropriate category.
         2. Add entry in `/docs/code-quality/code-analysis-warnings-for-managed-code-by-checkid.md`
         3. Add entry in the documentation file `/docs/code-quality/<%category%>-warnings.md` for rule's `Category`. For example:
            1. For a new rule with category `Design`, add an entry to `/docs/code-quality/design-warnings.md`.
            2. For a new rule with category `Performance`, add an entry to `/docs/code-quality/performance-warnings.md`, and so on.
    
   If for some exceptional reason you are unable to submit a PR, please [file a documentation issue](https://github.com/MicrosoftDocs/visualstudio-docs/issues) to add documentation for the rule in future. Please include all relevant information in the issue to allow the documentation experts to easily author the documentation. For example, see [this issue](https://github.com/MicrosoftDocs/visualstudio-docs/issues/3454).
