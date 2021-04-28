// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class PostActionModel : ConditionedConfigurationElementBase, IPostActionModel
    {
        public string Description { get; private set; }

        public Guid ActionId { get; private set; }

        public bool ContinueOnError { get; private set; }

        public IReadOnlyDictionary<string, string> Args { get; private set; }

        public IReadOnlyList<ManualInstructionModel> ManualInstructionInfo { get; private set; }

        public string ConfigFile { get; private set; }

        internal static IReadOnlyList<IPostActionModel> ListFromJArray(JArray jObject, IReadOnlyList<IPostActionLocalizationModel> localizations)
        {
            List<IPostActionModel> modelList = new List<IPostActionModel>();

            if (jObject == null)
            {
                return modelList;
            }

            int localizationIndex = 0;
            foreach (JToken action in jObject)
            {
                IPostActionLocalizationModel actionLocalizations = localizationIndex < localizations?.Count ? localizations[localizationIndex] : null;
                localizationIndex++;

                Dictionary<string, string> args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (JProperty argInfo in action.PropertiesOf("Args"))
                {
                    args.Add(argInfo.Name, argInfo.Value.ToString());
                }

                List<ManualInstructionModel> instructionOptions = new ();

                JArray manualInstructions = action.Get<JArray>("ManualInstructions");

                if (manualInstructions != null)
                {
                    for (int i = 0; i < manualInstructions.Count; i++)
                    {
                        string text;
                        if (actionLocalizations?.Instructions.Count > i && !string.IsNullOrEmpty(actionLocalizations.Instructions[i]))
                        {
                            text = actionLocalizations.Instructions[i];
                        }
                        else
                        {
                            text = manualInstructions[i].ToString("text");
                        }

                        instructionOptions.Add(new ManualInstructionModel(text, manualInstructions[i].ToString("condition")));
                    }
                }

                PostActionModel model = new PostActionModel()
                {
                    Condition = action.ToString(nameof(model.Condition)),
                    Description = actionLocalizations?.Description ?? action.ToString(nameof(model.Description)),
                    ActionId = action.ToGuid(nameof(ActionId)),
                    ContinueOnError = action.ToBool(nameof(model.ContinueOnError)),
                    Args = args,
                    ManualInstructionInfo = instructionOptions,
                    ConfigFile = action.ToString(nameof(model.ConfigFile))
                };

                modelList.Add(model);
            }

            return modelList;
        }
    }
}
