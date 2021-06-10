// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class PostActionModel : ConditionedConfigurationElementBase, IPostActionModel
    {
        /// <summary>
        /// Default id to be used when post action contains only one manual instruction
        /// and the author has not explicitly specified an id.
        /// </summary>
        public const string DefaultIdForSingleManualInstruction = "default";

        public PostActionModel()
            : this(new Dictionary<string, string>(), new List<ManualInstructionModel>()) { }

        public PostActionModel(IReadOnlyDictionary<string, string> args, IReadOnlyList<ManualInstructionModel> manualInstructions)
        {
            Args = args;
            ManualInstructionInfo = manualInstructions;
        }

        public string? Id { get; init; }

        public string? Description { get; private set; }

        public Guid ActionId { get; init; }

        public bool ContinueOnError { get; init; }

        public IReadOnlyDictionary<string, string> Args { get; init; } = new Dictionary<string, string>();

        public IReadOnlyList<ManualInstructionModel> ManualInstructionInfo { get; init; } = new List<ManualInstructionModel>();

        public void Localize(IPostActionLocalizationModel locModel, ILogger logger)
        {
            Description = locModel.Description ?? Description;

            foreach (var manualInstruction in ManualInstructionInfo)
            {
                string localizedInstruction = string.Empty;
                bool exactIdMatch = manualInstruction.Id != null && locModel.Instructions.TryGetValue(manualInstruction.Id, out localizedInstruction);
                bool defaultIdMatch = manualInstruction.Id == null && ManualInstructionInfo.Count == 1 && locModel.Instructions.TryGetValue(DefaultIdForSingleManualInstruction, out localizedInstruction);

                if (exactIdMatch || defaultIdMatch)
                {
                    manualInstruction.Localize(localizedInstruction);
                }
            }
        }

        internal static IReadOnlyList<PostActionModel> LoadListFromJArray(JArray jArray, ILogger logger, string filename)
        {
            List<PostActionModel> localizedPostActions = new List<PostActionModel>();
            if (jArray == null)
            {
                return localizedPostActions;
            }

            HashSet<string> postActionIds = new();
            for (int postActionIndex = 0; postActionIndex < jArray.Count; postActionIndex++)
            {
                JToken action = jArray[postActionIndex];
                string? postActionId = action.ToString(nameof(Id));
                string? description = action.ToString(nameof(Description));
                Guid actionId = action.ToGuid(nameof(ActionId));
                bool continueOnError = action.ToBool(nameof(ContinueOnError));
                string? postActionCondition = action.ToString(nameof(Condition));

                if (postActionId != null && !postActionIds.Add(postActionId))
                {
                    // There is already a post action with the same id. Localization won't work properly. Let user know.
                    logger.LogWarning(LocalizableStrings.Authoring_PostActionIdIsNotUnique, filename, postActionId, postActionIndex);
                    postActionId = null;
                }

                if (actionId == default)
                {
                    logger.LogError(LocalizableStrings.Authoring_PostActionMustHaveActionId, filename, postActionIndex);
                    continue;
                }

                Dictionary<string, string> args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (JProperty argInfo in action.PropertiesOf("Args"))
                {
                    args.Add(argInfo.Name, argInfo.Value.ToString());
                }

                using var postActionLoggerScope = logger.BeginScope("PostAction " + (postActionId ?? postActionIndex.ToString()));
                IReadOnlyList<ManualInstructionModel> manualInstructions = LoadManualInstructionsFromJArray(action.Get<JArray>("ManualInstructions"), logger);

                PostActionModel model = new PostActionModel(args, manualInstructions)
                {
                    Id = postActionId,
                    Description = description,
                    ActionId = actionId,
                    ContinueOnError = continueOnError,
                    Condition = postActionCondition
                };

                localizedPostActions.Add(model);
            }

            return localizedPostActions;
        }

        private static IReadOnlyList<ManualInstructionModel> LoadManualInstructionsFromJArray(JArray? jArray, ILogger logger)
        {
            var results = new List<ManualInstructionModel>();
            if (jArray == null)
            {
                return results;
            }

            HashSet<string> manualInstructionIds = new HashSet<string>();
            for (int i = 0; i < jArray.Count; i++)
            {
                JToken jToken = jArray[i];
                string? id = jToken.ToString("id");
                string text = jToken.ToString("text") ?? string.Empty;
                string? condition = jToken.ToString("condition");

                if (id != null && !manualInstructionIds.Add(id))
                {
                    // There is already an instruction with the same id. We won't be able to localize this. Let user know.
                    logger.LogWarning(LocalizableStrings.Authoring_ManualInstructionIdIsNotUnique, id, i);
                    id = null;
                }

                results.Add(new ManualInstructionModel(id, text, condition));
            }

            return results;
        }
    }
}
