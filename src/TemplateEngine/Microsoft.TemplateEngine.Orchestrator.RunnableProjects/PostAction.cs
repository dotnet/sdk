// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;

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

        internal static List<IPostAction> ListFromModel(IEngineEnvironmentSettings environmentSettings, IReadOnlyList<IPostActionModel> modelList, IVariableCollection rootVariableCollection)
        {
            List<IPostAction> actionList = new List<IPostAction>();

            if (rootVariableCollection == null)
            {
                rootVariableCollection = new VariableCollection();
            }

            foreach (IPostActionModel model in modelList)
            {
                model.EvaluateCondition(environmentSettings, rootVariableCollection);

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
                                chosenInstruction = modelInstruction.Text;
                            }
                        }
                        else if (modelInstruction.EvaluateCondition(environmentSettings, rootVariableCollection))
                        {
                            // condition is not blank and true, take this one. This results in a last-in-wins behaviour for conditions that are true.
                            chosenInstruction = modelInstruction.Text;
                        }
                    }
                }

                IPostAction postAction = new PostAction(
                    model.Description,
                    chosenInstruction,
                    model.ActionId,
                    model.ContinueOnError,
                    model.Args);

                actionList.Add(postAction);
            }

            return actionList;
        }
    }
}
