# macOS Roslyn apphost signing

`Directory.Build.props` assigns stable `com.microsoft.dotnet.<tool-name>` signing
identifiers to the `csc`, `vbc`, and `VBCSCompiler` apphosts created by
`PublishRoslyn` in `redist/targets/GenerateLayout.targets`. Identifiers are
lowercase, including `com.microsoft.dotnet.vbcscompiler`.

The signing step applies `roslyn-entitlements.plist` and preserves existing
requirements, signing flags, hardened runtime version, and launch and library
constraints while explicitly setting the identifier. Reusable apphost templates
remain application-neutral.
