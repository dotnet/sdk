// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface IOrchestrator
    {
        void Run(string runSpecPath, IDirectory sourceDir, string targetDir);

        void Run(IGlobalRunSpec spec, IDirectory sourceDir, string targetDir);

        IReadOnlyList<IFileChange> GetFileChanges(string runSpecPath, IDirectory sourceDir, string targetDir);

        IReadOnlyList<IFileChange> GetFileChanges(IGlobalRunSpec spec, IDirectory sourceDir, string targetDir);
    }

    public interface IOrchestrator2
    {
        void Run(string runSpecPath, IDirectory sourceDir, string targetDir);

        void Run(IGlobalRunSpec spec, IDirectory sourceDir, string targetDir);

        IReadOnlyList<IFileChange2> GetFileChanges(string runSpecPath, IDirectory sourceDir, string targetDir);

        IReadOnlyList<IFileChange2> GetFileChanges(IGlobalRunSpec spec, IDirectory sourceDir, string targetDir);
    }
}
