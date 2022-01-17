## CodeAnalysisTreatWarningsAsErrors - Treat code analysis warnings as errors

If you use the `/warnaserror` flag when you build your projects, all code analysis warnings are also treated as errors. If you do not want code quality warnings (CAxxxx) to be treated as errors in presence of `/warnaserror`, you can set the `CodeAnalysisTreatWarningsAsErrors` MSBuild property to `false` in your project file. Similarly, if want only code analysis warnings to be treated as errors, you can set `CodeAnalysisTreatWarningsAsErrors` MSBuild property to `true` in your project file.

### Semantics for CodeAnalysisTreatWarningsAsErrors

We have following important MSBuild properties that determine whether or not CAxxxx rules are bulk escalated (or not escalated) from warnings to errors:

- `CodeAnalysisTreatWarningsAsErrors`
- `TreatWarningsAsErrors`
- `WarningsAsErrors`
- `WarningsNotAsErrors`

Following are the precedence rules as per the values of these properties:

1. For non-CAxxxx rules

   1. `CodeAnalysisTreatWarningsAsErrors` has no relevance and is ignored.
   2. `TreatWarningsAsErrors`, if not set, defaults to false. If true, this property translates to `/warnaserror` command line switch.
   3. Compiler bumps all warnings to errors iff `TreatWarningsAsErrors` is true. Users can prevent escalation or force escalation of individual warnings to errors by appending the IDs to `WarningsNotAsErrors` or `WarningsAsErrors`, which just translate to `/warnaserror[+|-]:<%rule ids%>` on the command line.

2. For CAxxxx rules:

   1. If `CodeAnalysisTreatWarningsAsErrors` and `TreatWarningsAsErrors` both are not set, no bulk settings to escalate or de-escalate warnings to errors is done.
   2. If `CodeAnalysisTreatWarningsAsErrors` is set, it overrides `TreatWarningsAsErrors` to determine if CA warnings are bulk escalated to errors or not.
   3. If `CodeAnalysisTreatWarningsAsErrors` is not set, it defaults to `TreatWarningsAsErrors`.
   4. If final value of `CodeAnalysisTreatWarningsAsErrors = false`, we append all CA rule IDs to `WarningsNotAsErrors` to ensure they are not escalated to errors. Users can still bump individual rule IDs to errors by editorconfig/ruleset entry, etc.
   5. If final value of `CodeAnalysisTreatWarningsAsErrors = true`, we append all CA rule IDs to `WarningsAsErrors` to ensure they are escalated to errors. We optimize it a bit more by avoiding this append if `TreatWarningsAsErrors` is also true, because then the compiler itself will take care of bumping all warnings to errors, and we don't need to pollute the command line with large number of CA rules IDs in a `/warnaserror+` switch. We expect this to be the most common case as well (`TreatWarningsAsErrors` is set by user to true, `CodeAnalysisTreatWarningsAsErrors` is never set and hence defaults to `true`), and we want to ensure we don't end up polluting the entire command line with CA rules IDs unless `TreatWarningsAsErrors` and `CodeAnalysisTreatWarningsAsErrors` have different settings.