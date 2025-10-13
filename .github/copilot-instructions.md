Coding Style and Changes:
- Code should match the style of the file it's in.
- Changes should be minimal to resolve a problem in a clean way.
- User-visible changes to behavior should be considered carefully before committing. They should always be flagged.
- When generating code, run `dotnet format` to ensure uniform formatting.
- Prefer using file-based namespaces for new code.
- Do not allow unused `using` directives to be committed.

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
