// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IFileChange
    {
        string TargetRelativePath { get; }

        ChangeKind ChangeKind { get; }

        byte[] Contents { get; }
    }

    public interface IFileChange2 : IFileChange
    {
        string SourceRelativePath { get; }
    }
}
