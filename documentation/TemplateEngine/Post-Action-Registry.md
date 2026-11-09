# JSON property post action

Adds a property to a JSON file. The action ID is
`695A3659-EB40-4FF5-A6A6-C9C4E629FCB0`.

## Arguments

- `jsonFileName` (string): The file name to search for.
- `newJsonPropertyName` (string): The property name to add.
- `newJsonPropertyValue` (string): The JSON value to assign. A value that is not
  valid JSON is treated as a string.
- `parentPropertyPath` (string, optional): The parent property path, separated
  by colons, such as `test`. If omitted, the property is added at the root.
- `allowFileCreation` (bool, default `false`): Create a file if none is found.
- `allowPathCreation` (bool, default `false`): Create missing parent properties.
- `allowPropertyOverwrite` (bool, default `true`): Allow replacing an existing
  property. When `false`, an equal value is accepted, but a conflicting value
  fails the post action with a diagnostic and leaves the file unchanged.
  Missing properties can still be added.
- `ignorePropertyValueCase` (bool, default `false`): When overwriting is disabled,
  compare scalar strings using `OrdinalIgnoreCase` and retain the stored casing
  of an existing matching value. Object property names, nested strings, arrays,
  and value types still use strict JSON comparison.
- `detectRepositoryRoot` (bool, default `false`): Detect the repository root for
  file creation and stop searching above it.
- `includeAllDirectoriesInSearch` (bool, default `true`): Search subdirectories
  for a matching file.
- `includeAllParentDirectoriesInSearch` (bool, default `false`): Search all
  parent directories, subject to the repository-root boundary.

## Runner compatibility and servicing

The .NET 10 MSTest templates for C#, F#, and Visual Basic, and the Playwright
MSTest template, already use this post action to select
Microsoft.Testing.Platform in `global.json`. They set `allowPropertyOverwrite`
to `false` and `ignorePropertyValueCase` to `true`, matching the
[CLI's case-insensitive runner resolution](../../src/Cli/dotnet/Commands/Test/TestCommandParser.cs).
An explicitly conflicting runner, such as VSTest, is preserved rather than
silently changing the runner for every project in that file's scope.

Project creation still succeeds because these post actions use
[`continueOnError`](../../src/Cli/Microsoft.TemplateEngine.Cli/PostActionDispatcher.cs).
The diagnostic and manual instructions require the user to reconcile the runner
choice. If no runner is configured, the templates still add
Microsoft.Testing.Platform; existing projects in that scope must be compatible.
Plain `dotnet new mstest` continues to default to VSTest. This change does not
add mixed-runner solution support or MTP support to additional templates.

The overwrite and case-comparison arguments are available in .NET 10 servicing
builds that include this backport. Older SDK hosts ignore these arguments, so
servicing must ship both the
[post-action processor](../../src/Cli/Microsoft.TemplateEngine.Cli/PostActionProcessors/AddJsonPropertyPostActionProcessor.cs)
and the template configuration together. Availability must not be inferred from
the .NET 10 major version alone.
