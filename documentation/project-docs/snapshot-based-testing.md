# Guide to snapshot-based testing in the .NET SDK

Snapshot-based testing is a technique used in the .NET SDK to ensure that the command-line interface (CLI) behaves as expected. This document provides an overview of how snapshot-based testing works, particularly for CLI completions, and how to manage and update snapshots.

## CLI Completions Snapshot Testing

The point of the tests is to keep an eye on the CLI, since it is our user interface. When the CLI changes in a way that impacts completions (new commands, options, defaults) we need to inspect and reconcile the baselines. Most of this happens in the [dotnet.Tests][dotnet.Tests] project, in the [Microsoft.DotNet.Cli.Completions.Tests.DotnetCliSnapshotTests][snapshot-tests] tests.

These tests use [Verify][Verify] to perform snapshot testing - storing the results of a known good 'baseline' as a snapshot and comparing the output of the same action performed with changes in your PR. Verify calls these baselines 'verified' files. When the test computes a new snapshot for comparison, this is called a 'received' file. Verify compares the 'verified' file to the 'received' file for each test, and if they are not the same provides a git-diff in the console output for the test.

To fix these tests, you need to diff the two files and visually inspect the changes. If the changes to the 'received' file are what you want to see (new commands, new options, renames, etc) then you rename the 'received' file to 'verified' and commit that change to the 'verified' file. There are two MSBuild Targets on the dotnet.Tests project that you can use to help you do this, both of which are intended to be run after you run the snapshot tests locally:

* [CompareCliSnapshots][compare] - this Target copies the .received. files from the artifacts directory, where they are created due to the way we run tests, to the [snapshots][snapshots] directory in the dotnet.Tests project. This makes it much easier to diff the two.
* [UpdateCliSnapshots][update] - this Target renames the .received. files to .verified. in the local [snapshots][snapshots] directory, and so acts as a giant 'I accept these changes' button. Only use this if you've diffed the snapshots and are sure they match your expectations.

[dotnet.Tests]: ../../test/dotnet.Tests/
[snapshot-tests]: ../../test/dotnet.Tests/CompletionTests/DotnetCliSnapshotTests.cs
[snapshots]: ../../test/dotnet.Tests/CompletionTests/snapshots/
[Verify]: https://github.com/VerifyTests/Verify
[compare]: ../../test/dotnet.Tests/dotnet.Tests.csproj#L100
[update]: ../../test/dotnet.Tests/dotnet.Tests.csproj#L107
