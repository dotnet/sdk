// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal;

internal sealed record TarExtractionContext(
    string ArchivePath,
    string TargetDirectory,
    IProgressTask? ProgressTask = null,
    MuxerHandler? MuxerHandler = null,
    Action<string>? OnEntryExtracted = null,
    Func<string, bool>? ShouldSkipEntry = null);
