# SDK Breaking Change and Diagnostic Guidelines

This document provides guidelines for introducing new diagnostics or breaking changes to the .NET SDK, which configuration knobs are available, and what criteria should guide the decision around severity, timeline for introduction, and deprecation of diagnostics.

## Overall procedure

In general, we want to make updating the .NET SDK as smooth as possible for developers. This means:

* introducing new changes in a staged/gradual way
* trying to tie opinionated analyzers/diagnostics to a mechanism that requires explicit user opt-in
* providing a way to opt out of a change entirely

## Kinds of changes

There are many kinds of breaking changes that can ship in the .NET SDK, such as:

* New MSBuild warnings and errors (props/targets)
* New NuGet warnings and errors. 
  * For example, NuGet Audit.
* Roslyn Analyzers and CodeFixes
  * This includes trimming/ILLink analyzers and codefixes
* Behavioral/implementation changes
  * MSBuild engine changes like MSBuild Server
  * Implementation changes for MSBuild Tasks
  * NuGet Restore algorithm enhancements
  * Changes to DotNet CLI grammar
  * Changes to defaults in CLI flags that impact behavior
    * For example, `--configuration` flag defaulting to `Release` instead of `Debug`

## Configuration Knobs

The following knobs are available to enable/disable these changes (some may not apply to all kinds of changes):

* [TargetFramework](https://learn.microsoft.com/en-us/dotnet/standard/frameworks)
* [SdkAnalysisLevel](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props#sdkanalysislevel)
* EditorConfig (for Analyzers)
* AnalysisLevel (for Analyzers/CodeFixes)
* WarningLevel

## Deciding how to Surface Changes

### Implement changes in an informational/non-blocking way initially

What this means will vary change-to-change. For example, for a change expressed as an Analyzer or MSBuild diagnostic, consider
Informational level severities initially. For a behavioral change on a CLI, consider an informational message written to the
stderr channel on the console instead of making the stdout output un-parseable by tools.

Concrete example: When the `dotnet new --list` command was changed to `dotnet new list` to adhere to CLI design guidelines,
the old command was still the default supported and a warning was written to the console when the old form was used pointing users to the new form. This allowed users and scripts to continue using the old form and gradually migrate to the new.

### Gradually increase severity over each release

If a change in introduced in an informational/non-blocking way, determine the time frame where it is safe to increase the severity. For Analyzers, this may mean tying it to the next value of AnalysisLevel (which is downstream of TFM). For small MSBuild and NuGet diagnostics, this may mean tying it to the next Warning Level or SdkAnalysisLevel. For CLI changes, this may mean tying it to the next LTS major version of the SDK. Ideally the way you would structure this increase would be automated and documented so that users know what's coming down the pipe.

### Cut-over to new behavior after a long introduction period

After the change has been introduced in a gradual way, cut over to the new behavior. This may mean removing the old behavior entirely, or it may mean making the new behavior the default and providing a way to opt out of it. It is important that you
provide enough time for users to adapt - for example the `dotnet new --list` example above took an entire major release to make the new forms the default.

### Always provide a way to opt out of the change

Have some kind of knob that allows users to opt out of the change entirely. This could be a flag, an environment variable, or a global.json setting. This allows users to continue using the old behavior if they need to in exceptional situations. It is
important to document this knob and its behavior in the SDK documentation. It is also important to define a timeline for when this knob will be removed entirely, forcing users to adopt the new behavior.

For systems like Analyzers that time may be 'never', because the cost of detection is so low. This is a product-level decision that is hard to give universal guidance for.

### Hook into the unified SdkAnalysisLevel knob to allow users to easily opt out of all changes

This knob exists so that users can safely and consistently say "for whatever reason, I just need you to act like SDK version X". This is the one-stop shop - users no longer need to know about all the individual knobs that are available to them.

### Tie potentially impactful changes to the TFM targeted by the application/library.

Changes that are expected to cause significant disruption should only be introduced behind the Target Framework knob. This eliminates business continuity and allows developers to address changes needed as part of scheduled work to migrate a codebase to a new TFM.

Concrete example: NuGet warnings for vulnerable transitive dependencies were introduced only for applications targeting .NET 10 and higher.

## Other recommendations

* If possible, introduce significant breaking changes in a non-LTS release.
* Publish blog posts and update public documentation as appropriate as early as possible.
* Consider creating and pinning an issue in the corresponding GitHub repository where customers can provide feedback.
