using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Expressions.Cpp;
using Microsoft.TemplateEngine.Core.Expressions.Cpp2;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class PostAction : IPostAction
    {
        public string Description { get; private set; }

        public Guid ActionId { get; private set; }

        public bool ContinueOnError { get; private set; }

        public IReadOnlyDictionary<string, string> Args { get; private set; }

        public string ManualInstructions { get; private set; }

        public string ConfigFile { get; private set; }

        string IPostAction.Description => Description;

        Guid IPostAction.ActionId => ActionId;

        bool IPostAction.ContinueOnError => ContinueOnError;

        IReadOnlyDictionary<string, string> IPostAction.Args => Args;

        string IPostAction.ManualInstructions => ManualInstructions;

        string IPostAction.ConfigFile => ConfigFile;

        public static List<IPostAction> ListFromModel(IEngineEnvironmentSettings environmentSettings, IReadOnlyList<IPostActionModel> modelList, IVariableCollection rootVariableCollection)
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
                {   // Condition on the post action is blank, or not true. Don't include this post action.
                    continue;
                }

                string chosenInstruction = string.Empty;

                foreach (KeyValuePair<string, string> modelInstruction in model.ManualInstructionInfo)
                {
                    if (string.IsNullOrEmpty(modelInstruction.Value))
                    {   // no condition
                        if (string.IsNullOrEmpty(chosenInstruction))
                        {   // No condition, and no instruction previously chosen. Take this one.
                            // We don't want a default instruction to override a conditional one.
                            chosenInstruction = modelInstruction.Key;
                        }
                    }
                    else if (Cpp2StyleEvaluatorDefinition.EvaluateFromString(environmentSettings, modelInstruction.Value, rootVariableCollection))
                    {   // condition is not blank and true, take this one. This results in a last-in-wins behaviour for conditions that are true.
                        chosenInstruction = modelInstruction.Key;
                    }
                }

                IPostAction postAction = new PostAction()
                {
                    Description = model.Description,
                    ActionId = model.ActionId,
                    ContinueOnError = model.ContinueOnError,
                    Args = model.Args,
                    ManualInstructions = chosenInstruction,
                    ConfigFile = model.ConfigFile,
                };

                actionList.Add(postAction);
            }

            return actionList;
        }
    }
}
