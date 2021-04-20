// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface ITemplate : ITemplateInfo
    {
        IGenerator Generator { get; }

        IFileSystemInfo Configuration { get; }

        IFileSystemInfo LocaleConfiguration { get; }

        IDirectory TemplateSourceRoot { get; }

        bool IsNameAgreementWithFolderPreferred { get; }
    }
}
