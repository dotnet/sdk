Use the instructions from the main branch if available: @dotnet/sdk/files/.github/copilot-instructions.md

If the instructions from main are not available, use the following as a fallback:

Coding Style and Changes:
- Code should match the style of the file it's in.
- Changes should be minimal to resolve a problem in a clean way.
- User-visible changes to behavior should be considered carefully before committing. They should always be flagged.
- When generating code, run `dotnet format` to ensure uniform formatting.
- Prefer using file-based namespaces for new code.
- Do not allow unused `using` directives to be committed.
- Commit your changes, and then format them.
- Add the format commit SHA to the .git-blame-ignore-revs file so that the commit doesn't dirty git blame in the future
- Use `#if NET` blocks for .NET Core specific code, and `#if NETFRAMEWORK` for .NET Framework specific code.

Testing:
- Large changes should always include test changes.
- The Skip parameter of the Fact attribute to point to the specific issue link.
- To run tests in this repo:
  - Use the repo-local dotnet instance: `./.dotnet/dotnet`
  - For MSTest-style projects: `dotnet test path/to/project.csproj --filter "FullyQualifiedName~TestName"`
  - For XUnit test assemblies: `dotnet exec artifacts/bin/redist/Debug/TestAssembly.dll -method "*TestMethodName*"`
  - Examples:
    - `dotnet test test/dotnet.Tests/dotnet.Tests.csproj --filter "Name~ItShowsTheAppropriateMessageToTheUser"`
    - `dotnet exec artifacts/bin/redist/Debug/dotnet.Tests.dll -method "*ItShowsTheAppropriateMessageToTheUser*"`
- To test CLI command changes:
  - Build the redist SDK: `./build.sh` from repo root
  - Create a dogfood environment: `source eng/dogfood.sh`
  - Test commands in the dogfood shell (e.g., `dnx --help`, `dotnet tool install --help`)
  - The dogfood script sets up PATH and environment to use the newly built SDK

dotnetup:
- When building or testing dotnetup, always use the full path to the csproj file:
  - `dotnet build d:\sdk\src\Installer\dotnetup\dotnetup.csproj`
  - `dotnet test d:\sdk\test\dotnetup.Tests\dotnetup.Tests.csproj`
- Do not run `dotnet build` from within the dotnetup directory as restore may fail.
- When running dotnetup directly (e.g. `dotnet run`), use the repo-local dogfood dotnet instance:
  - `d:\sdk\.dotnet\dotnet run --project d:\sdk\src\Installer\dotnetup\dotnetup.csproj -- <args>`

dotnetup Code Style:
- dotnetup has an `.editorconfig` at `src/Installer/.editorconfig` that enforces strict style rules (file-scoped namespaces, `s_` prefix for static fields, `_` prefix for instance fields, CA analyzers as warnings, etc.).
- All three projects (dotnetup, Microsoft.Dotnet.Installation, dotnetup.Tests) build with `TreatWarningsAsErrors`, so style violations break the build.
- When writing new code, follow the `.editorconfig` conventions. Key rules:
  - File-scoped namespaces (`namespace Foo;`)
  - File header: `// Licensed to the .NET Foundation under one or more agreements.` / `// The .NET Foundation licenses this file to you under the MIT license.`
  - Static fields: `s_camelCase` prefix. Instance fields: `_camelCase` prefix.
  - Use `.ConfigureAwait(false)` on awaited tasks (CA2007).
  - Use `CultureInfo.InvariantCulture` for string formatting (CA1305), or suppress with pragma if the API doesn't support it.
  - Mark methods `static` if they don't access instance data (CA1822), unless they implement an interface.
- When fixing bugs or iterating quickly, it is acceptable to skip formatting and add `// TODO: fix style` comments. Style can be cleaned up in a follow-up commit. Do not let formatting slow down urgent fixes.
- To auto-format: `d:\sdk\.dotnet\dotnet format <project.csproj> --no-restore`
- A pre-commit hook is available at `src/Installer/hooks/`. Install with `powershell -File src/Installer/hooks/install.ps1` (Windows) or `sh src/Installer/hooks/install.sh` (Unix).

Output Considerations:
- When considering how output should look, solicit advice from baronfel.

Localization:
- Avoid modifying .xlf files and instead prompt the user to update them using the `/t:UpdateXlf` target on MSBuild. Correctly automatically modified .xlf files have elements with state `needs-review-translation` or `new`.
- Consider localizing strings in .resx files when possible.

Documentation:
- Do not manually edit files under documentation/manpages/sdk as these are generated based on documentation and should not be manually modified.

External Dependencies:
- Changes that require modifications to the dotnet/templating repository (Microsoft.TemplateEngine packages) should be made directly in that repository, not worked around in this repo.
- The dotnet/templating repository owns the TemplateEngine.Edge, TemplateEngine.Abstractions, and related packages.
- If a change requires updates to template engine behavior or formatting (e.g., DisplayName properties), file an issue in dotnet/templating and make the changes there rather than adding workarounds in this SDK repository.
