// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Expressions.Cpp2;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.OperationConfig;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class GlobalRunSpec : IGlobalRunSpec
    {
        private readonly IReadOnlyDictionary<string, IOperationConfig> _operationConfigLookup;

        internal GlobalRunSpec(
            IDirectory templateRoot,
            IComponentManager componentManager,
            IVariableCollection variables,
            IRunnableProjectConfig configuration)
        {
            List<IOperationConfig> operationConfigReaders = new List<IOperationConfig>(componentManager.OfType<IOperationConfig>());
            Dictionary<string, IOperationConfig> operationConfigLookup = new Dictionary<string, IOperationConfig>();

            foreach (IOperationConfig opConfig in operationConfigReaders)
            {
                operationConfigLookup[opConfig.Key] = opConfig;
            }

            _operationConfigLookup = operationConfigLookup;

            RootVariableCollection = variables;
            IgnoreFileNames = configuration.IgnoreFileNames;
            Operations = ResolveOperations(configuration.GlobalOperationConfig, templateRoot, variables);
            List<KeyValuePair<IPathMatcher, IRunSpec>> specials = new List<KeyValuePair<IPathMatcher, IRunSpec>>();

            if (configuration.SpecialOperationConfig != null)
            {
                foreach ((string glob, GlobalRunConfig runConfig) in configuration.SpecialOperationConfig)
                {
                    IReadOnlyList<IOperationProvider> specialOps = Array.Empty<IOperationProvider>();

                    if (runConfig != null)
                    {
                        specialOps = ResolveOperations(runConfig, templateRoot, variables);
                    }

                    RunSpec spec = new(specialOps, runConfig?.VariableSetup.FallbackFormat);
                    specials.Add(new KeyValuePair<IPathMatcher, IRunSpec>(new GlobbingPatternMatcher(glob), spec));
                }
            }

            Special = specials;
        }

        public IReadOnlyList<IPathMatcher> Include { get; private set; } = Array.Empty<IPathMatcher>();

        public IReadOnlyList<IPathMatcher> Exclude { get; private set; } = Array.Empty<IPathMatcher>();

        public IReadOnlyList<IPathMatcher> CopyOnly { get; private set; } = Array.Empty<IPathMatcher>();

        public IReadOnlyList<IOperationProvider> Operations { get; }

        public IVariableCollection RootVariableCollection { get; }

        public IReadOnlyList<KeyValuePair<IPathMatcher, IRunSpec>> Special { get; }

        public IReadOnlyList<string> IgnoreFileNames { get; }

        internal IReadOnlyDictionary<string, string> Rename { get; private set; } = new Dictionary<string, string>();

        public bool TryGetTargetRelPath(string sourceRelPath, out string targetRelPath)
        {
            return Rename.TryGetValue(sourceRelPath, out targetRelPath);
        }

        internal void SetupFileSource(FileSourceMatchInfo source)
        {
            FileSourceHierarchicalPathMatcher matcher = new FileSourceHierarchicalPathMatcher(source);
            Include = new List<IPathMatcher>() { new FileSourceStateMatcher(FileDispositionStates.Include, matcher) };
            Exclude = new List<IPathMatcher>() { new FileSourceStateMatcher(FileDispositionStates.Exclude, matcher) };
            CopyOnly = new List<IPathMatcher>() { new FileSourceStateMatcher(FileDispositionStates.CopyOnly, matcher) };
            Rename = source.Renames ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }

        // Returns a list of operations which contains the custom operations and the default operations.
        // If there are custom Conditional operations, don't include the default Conditionals.
        //
        // Note: we may need a more robust filtering mechanism in the future.
        private IReadOnlyList<IOperationProvider> ResolveOperations(GlobalRunConfig runConfig, IDirectory templateRoot, IVariableCollection variables)
        {
            IReadOnlyList<IOperationProvider> customOperations = SetupCustomOperations(runConfig.CustomOperations, templateRoot, variables);
            IReadOnlyList<IOperationProvider> defaultOperations = SetupOperations(templateRoot.MountPoint.EnvironmentSettings, variables, runConfig);

            List<IOperationProvider> operations = new List<IOperationProvider>(customOperations);

            if (customOperations.Any(x => x is Conditional))
            {
                operations.AddRange(defaultOperations.Where(op => op is not Conditional));
            }
            else
            {
                operations.AddRange(defaultOperations);
            }

            return operations;
        }

        private IReadOnlyList<IOperationProvider> SetupOperations(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables, GlobalRunConfig runConfig)
        {
            // default operations
            List<IOperationProvider> operations = new List<IOperationProvider>();
            operations.AddRange(runConfig.Operations);

            // replacements
            if (runConfig.Replacements != null)
            {
                foreach (IReplacementTokens replaceSetup in runConfig.Replacements)
                {
                    IOperationProvider? replacement = ReplacementConfig.Setup(environmentSettings, replaceSetup, variables);
                    if (replacement != null)
                    {
                        operations.Add(replacement);
                    }
                }
            }

            if (runConfig.VariableSetup.Expand)
            {
                operations.Add(new ExpandVariables(null, true));
            }

            return operations;
        }

        private IReadOnlyList<IOperationProvider> SetupCustomOperations(IReadOnlyList<CustomOperationModel> customModel, IDirectory templateRoot, IVariableCollection variables)
        {
            ITemplateEngineHost host = templateRoot.MountPoint.EnvironmentSettings.Host;
            List<IOperationProvider> customOperations = new List<IOperationProvider>();

            foreach (CustomOperationModel opModel in customModel)
            {
                string? opType = opModel.Type;
                string? condition = opModel.Condition;

                if (string.IsNullOrEmpty(condition)
                    || Cpp2StyleEvaluatorDefinition.EvaluateFromString(host.Logger, condition!, variables))
                {
                    if (opType == null)
                    {
                        continue;
                    }
                    if (opModel.Configuration == null)
                    {
                        continue;
                    }
                    if (_operationConfigLookup.TryGetValue(opType, out IOperationConfig realConfigObject))
                    {
                        customOperations.AddRange(
                            realConfigObject.ConfigureFromJson(opModel.Configuration, templateRoot));
                    }
                    else
                    {
                        host.Logger.LogWarning($"Operation type = [{opType}] from configuration is unknown.");
                    }
                }
            }

            return customOperations;
        }
    }
}
