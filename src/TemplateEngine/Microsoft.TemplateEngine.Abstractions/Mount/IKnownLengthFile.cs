// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Abstractions.Mount
{
    internal interface IKnownLengthFile : IFile
    {
        long Length { get; }
    }
}