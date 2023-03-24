// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class PostAction : IPostAction
    {
        public PostAction(string? description, string? manualInstructions, Guid actionId, bool continueOnError, IReadOnlyDictionary<string, string> args)
        {
            Description = description;
            ManualInstructions = manualInstructions;
            ActionId = actionId;
            ContinueOnError = continueOnError;
            Args = args;
        }

        public string? Description { get; }

        public string? ManualInstructions { get; }

        public Guid ActionId { get; }

        public bool ContinueOnError { get; private set; }

        public IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>();

        internal static List<IPostAction> Evaluate(
            IEngineEnvironmentSettings environmentSettings,
            IReadOnlyList<PostActionModel> modelList,
            IVariableCollection rootVariableCollection,
            FileRenameGenerator renameGenerator)
        {
            ILogger logger = environmentSettings.Host.Logger;
            List<IPostAction> actionList = new();

            rootVariableCollection ??= new VariableCollection();

            foreach (PostActionModel model in modelList)
            {
                model.EvaluateCondition(logger, rootVariableCollection);

                if (!model.ConditionResult)
                {
                    // Condition on the post action is blank, or not true. Don't include this post action.
                    continue;
                }

                string chosenInstruction = string.Empty;

                if (model.ManualInstructionInfo != null)
                {
                    foreach (ManualInstructionModel modelInstruction in model.ManualInstructionInfo)
                    {
                        if (string.IsNullOrEmpty(modelInstruction.Condition))
                        {
                            // no condition
                            if (string.IsNullOrEmpty(chosenInstruction))
                            {
                                // No condition, and no instruction previously chosen. Take this one.
                                // We don't want a default instruction to override a conditional one.
                                chosenInstruction =
                                    model.ApplyFileRenamesToManualInstructions
                                        ? renameGenerator.ApplyRenameToString(modelInstruction.Text)
                                        : modelInstruction.Text;
                            }
                        }
                        else if (modelInstruction.EvaluateCondition(logger, rootVariableCollection))
                        {
                            // condition is not blank and true, take this one. This results in a last-in-wins behavior for conditions that are true.
                            chosenInstruction =
                                model.ApplyFileRenamesToManualInstructions
                                    ? renameGenerator.ApplyRenameToString(modelInstruction.Text)
                                    : modelInstruction.Text;
                        }
                    }
                }

                Dictionary<string, string> processedArgs = new();

                foreach (KeyValuePair<string, string> arg in model.Args)
                {
                    processedArgs[arg.Key] = model.ApplyFileRenamesToArgs.Contains(arg.Key, StringComparer.OrdinalIgnoreCase)
                        ? renameGenerator.ApplyRenameToString(arg.Value)
                        : arg.Value;
                }
                IPostAction postAction = new PostAction(
                    model.Description,
                    chosenInstruction,
                    model.ActionId,
                    model.ContinueOnError,
                    processedArgs);

                actionList.Add(postAction);
            }

            return actionList;
        }
    }
}
