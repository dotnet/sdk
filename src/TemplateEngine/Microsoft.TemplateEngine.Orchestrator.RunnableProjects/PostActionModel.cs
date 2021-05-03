// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class PostActionModel : ConditionedConfigurationElementBase, IPostActionModel
    {
        public PostActionModel(
            string? description,
            Guid actionId,
            bool continueOnError,
            IReadOnlyDictionary<string, string> args,
            IReadOnlyList<ManualInstructionModel> manualInstructionInfo,
            string? configFile,
            string? condition)
        {
            Description = description;
            ActionId = actionId;
            ContinueOnError = continueOnError;
            Args = args;
            ManualInstructionInfo = manualInstructionInfo;
            ConfigFile = configFile;
            Condition = condition;
        }

        public string? Description { get; private set; }

        public Guid ActionId { get; private set; }

        public bool ContinueOnError { get; private set; }

        public IReadOnlyDictionary<string, string> Args { get; private set; }

        public IReadOnlyList<ManualInstructionModel> ManualInstructionInfo { get; private set; }

        public string? ConfigFile { get; private set; }

        internal static IReadOnlyList<IPostActionModel> ListFromJArray(JArray jObject, IReadOnlyDictionary<int, IPostActionLocalizationModel>? localizations)
        {
            List<IPostActionModel> modelList = new List<IPostActionModel>();

            if (jObject == null)
            {
                return modelList;
            }

            int postActionIndex = 0;
            int localizedPostActions = 0;
            foreach (JToken action in jObject)
            {
                IPostActionLocalizationModel? actionLocalizations = null;
                localizations?.TryGetValue(postActionIndex++, out actionLocalizations);
                localizedPostActions += actionLocalizations != null ? 1 : 0;

                Dictionary<string, string> args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (JProperty argInfo in action.PropertiesOf("Args"))
                {
                    args.Add(argInfo.Name, argInfo.Value.ToString());
                }

                List<ManualInstructionModel> instructionOptions = new ();

                JArray? manualInstructions = action.Get<JArray>("ManualInstructions");

                if (manualInstructions != null)
                {
                    for (int i = 0; i < manualInstructions.Count; i++)
                    {
                        string? text = string.Empty;
                        if (!actionLocalizations?.Instructions.TryGetValue(i, out text) ?? true || string.IsNullOrEmpty(text))
                        {
                            text = manualInstructions[i].ToString("text");
                        }

                        instructionOptions.Add(new ManualInstructionModel(text ?? string.Empty, manualInstructions[i].ToString("condition")));
                    }
                }

                PostActionModel model = new PostActionModel(
                    actionLocalizations?.Description ?? action.ToString(nameof(model.Description)),
                    action.ToGuid(nameof(ActionId)),
                    action.ToBool(nameof(model.ContinueOnError)),
                    args,
                    instructionOptions,
                    action.ToString(nameof(model.ConfigFile)),
                    action.ToString(nameof(model.Condition))
                );

                modelList.Add(model);
            }

            return modelList;
        }
    }
}
