# dotnetup documentation

This directory contains the in-repository documentation for `dotnetup`.
The public-facing articles use the structure and front matter expected by
the dotnet/docs repository.

## Public documentation

- [dotnetup overview](index.md)
- [Table of contents](toc.yml)
- [Core concepts](concepts/how-dotnetup-works.md)
- [Release channels](channels/preview.md)
- [CLI reference](reference/dotnetup.md)
- [Scenarios](usecases/install-with-global-json.md)

## Maintainer documentation

- [Design notes](designs/)
- [How dotnetup is included in the SDK](dotnetup_in_sdk.md)
- [Release engineering](releasing.md)
- [Signature verification](signature-verification.md)

The command reference follows the generated runtime help. Command handlers
and tests verify product behavior. Hidden compatibility and elevation
commands are implementation details. They are not part of the public command
reference.
