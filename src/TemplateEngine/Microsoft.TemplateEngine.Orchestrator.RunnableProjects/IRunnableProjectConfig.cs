// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal interface IRunnableProjectConfig
    {
        IReadOnlyDictionary<string, Parameter> Parameters { get; }

        IReadOnlyList<KeyValuePair<string, IGlobalRunConfig>> SpecialOperationConfig { get; }

        IGlobalRunConfig OperationConfig { get; }

        IReadOnlyList<FileSourceMatchInfo> Sources { get; }

        string DefaultName { get; }

        string Description { get; }

        string Name { get; }

        IReadOnlyList<string> ShortNameList { get; }

        string Author { get; }

        IReadOnlyDictionary<string, string> Tags { get; }

        IReadOnlyList<string> Classifications { get; }

        string GroupIdentity { get; }

        int Precedence { get; }

        IFile SourceFile { set; }

        string Identity { get; }

        IReadOnlyList<string> IgnoreFileNames { get; }

        IReadOnlyList<IPostActionModel> PostActionModel { get; }

        IReadOnlyList<ICreationPathModel> PrimaryOutputs { get; }

        string GeneratorVersions { get; }

        IReadOnlyDictionary<string, IBaselineInfo> BaselineInfo { get; }

        void Evaluate(IParameterSet parameters, IVariableCollection rootVariableCollection, IFileSystemInfo configFile);
    }
}
