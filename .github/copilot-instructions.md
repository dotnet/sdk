Coding Style and Changes:
- Code should match the style of the file it's in.
- Changes should be minimal to resolve a problem in a clean way.
- User-visible changes to behavior should be considered carefully before committing. They should always be flagged.

Testing:
- Large changes should always include test changes.
- The Skip parameter of the Fact attribute to point to the specific issue link.

Output Considerations:
- When considering how output should look, solicit advice from baronfel.

Localization:
- Avoid modifying .xlf files and instead prompt the user to update them using the `/t:UpdateXlf` target on MSBuild.
- Consider localizing strings in .resx files when possible.