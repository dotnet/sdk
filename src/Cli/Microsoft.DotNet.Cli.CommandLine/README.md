# Microsoft.Dotnet.Cli.CommandLine

This project contains extensions and utilities for building command line applications.

These extensions are layered on top of core System.CommandLine concepts and types, and
do not directly reference concepts that are specific to the `dotnet` CLI. We hope that
these would be published separately as a NuGet package for use by other command line
applications in the future.

From a layering perspective, everything that is specific to the `dotnet` CLI should
be in the `src/Cli/dotnet` or `src/Cli/Microsoft.DotNet.Cli.Utils` projects, which
reference this project. Keep this one generally-speaking clean.
