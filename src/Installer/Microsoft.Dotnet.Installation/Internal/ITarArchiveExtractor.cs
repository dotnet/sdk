// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal;

internal interface ITarArchiveExtractor
{
    void Extract(TarExtractionContext context);
}
