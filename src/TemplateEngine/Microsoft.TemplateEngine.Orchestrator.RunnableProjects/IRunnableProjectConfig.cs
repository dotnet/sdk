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

        IReadOnlyDictionary<string, IReadOnlyList<IOperationProvider>> LocalizationOperations { get; }

        IGlobalRunConfig OperationConfig { get; }

        IReadOnlyList<FileSourceMatchInfo> Sources { get; }

        string DefaultName { get; }

        string Description { get; }

        string Name { get; }

        IReadOnlyList<string> ShortNameList { get; }

        string Author { get; }

        IReadOnlyDictionary<string, ICacheTag> Tags { get; }

        IReadOnlyDictionary<string, ICacheParameter> CacheParameters { get; }

        IReadOnlyList<string> Classifications { get; }

        string GroupIdentity { get; }

        int Precedence { get; }

        IFile SourceFile { set; }

        string Identity { get; }

        IReadOnlyList<string> IgnoreFileNames { get; }

        IReadOnlyList<IPostActionModel> PostActionModel { get; }

        IReadOnlyList<ICreationPathModel> PrimaryOutputs { get; }

        string GeneratorVersions { get; }

        void Evaluate(IParameterSet parameters, IVariableCollection rootVariableCollection, IFileSystemInfo configFile);

        IReadOnlyDictionary<string, IBaselineInfo> BaselineInfo { get; }

        bool HasScriptRunningPostActions { get; set; }
    }
}
