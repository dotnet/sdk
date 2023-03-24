// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Localization;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Validation;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel
{
    public sealed class PostActionModel : ConditionedConfigurationElement
    {
        /// <summary>
        /// Default id to be used when post action contains only one manual instruction
        /// and the author has not explicitly specified an id.
        /// </summary>
        internal const string DefaultIdForSingleManualInstruction = "default";

        private string? _description;

        internal PostActionModel()
            : this(new Dictionary<string, string>(), new List<ManualInstructionModel>()) { }

        internal PostActionModel(IReadOnlyDictionary<string, string> args, IReadOnlyList<ManualInstructionModel> manualInstructions)
        {
            Args = args;
            ManualInstructionInfo = manualInstructions;
        }

        /// <summary>
        /// Gets a string that uniquely identifies this post action within a template.
        /// </summary>
        public string? Id { get; internal init; }

        /// <summary>
        /// Gets the description of the post action.
        /// </summary>
        public string? Description
        {
            get => _description;

            internal init => _description = value;
        }

        /// <summary>
        /// Gets the identifier of the action that will be performed.
        /// Note that this is not an identifier for the post action itself.
        /// </summary>
        public Guid ActionId { get; internal init; }

        /// <summary>
        /// Gets a value indicating whether the template instantiation should continue
        /// in case of an error with this post action.
        /// </summary>
        public bool ContinueOnError { get; internal init; }

        /// <summary>
        /// Gets the arguments for this post action.
        /// </summary>
        public IReadOnlyDictionary<string, string> Args { get; internal init; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets the list of arguments to which file renames should be applied.
        /// </summary>
        public IReadOnlyList<string> ApplyFileRenamesToArgs { get; internal init; } = Array.Empty<string>();

        /// <summary>
        /// Gets a value indicating whether the file renames should be applied to manual instructions.
        /// </summary>
        public bool ApplyFileRenamesToManualInstructions { get; internal init; }

        /// <summary>
        /// Gets the list of instructions that should be manually performed by the user.
        /// "instruction" contains the text that explains the steps to be taken by the user.
        /// An instruction is only considered if the "condition" evaluates to true.
        /// </summary>
        public IReadOnlyList<ManualInstructionModel> ManualInstructionInfo { get; internal init; } = new List<ManualInstructionModel>();

        internal static IReadOnlyList<PostActionModel> LoadListFromJArray(JArray? jArray, List<IValidationEntry> validationEntries)
        {
            List<PostActionModel> localizedPostActions = new();
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
                bool applyFileRenamesToManualInstructions = action.ToBool(nameof(ApplyFileRenamesToManualInstructions));
                IReadOnlyList<string> applyFileRenamesToArgs = action.ArrayAsStrings(nameof(ApplyFileRenamesToArgs)) ?? Array.Empty<string>();

                if (postActionId != null && !postActionIds.Add(postActionId))
                {
                    // There is already a post action with the same id. Localization won't work properly. Let user know.
                    validationEntries.Add(
                        new ValidationEntry(
                            IValidationEntry.SeverityLevel.Warning,
                            "CONFIG0201",
                            string.Format(LocalizableStrings.Authoring_CONFIG0201_PostActionIdIsNotUnique, postActionId, postActionIndex)));
                    postActionId = null;
                }

                if (actionId == default)
                {
                    validationEntries.Add(
                    new ValidationEntry(
                        IValidationEntry.SeverityLevel.Error,
                        "CONFIG0202",
                        string.Format(LocalizableStrings.Authoring_CONFIG0202_PostActionMustHaveActionId, postActionIndex)));
                    continue;
                }

                Dictionary<string, string> args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (JProperty argInfo in action.PropertiesOf("Args"))
                {
                    args.Add(argInfo.Name, argInfo.Value.ToString());
                }

                List<string> verifiedApplyFileRenamesToArgs = new();

                foreach (string arg in applyFileRenamesToArgs)
                {
                    if (!args.ContainsKey(arg))
                    {
                        validationEntries.Add(
                         new ValidationEntry(
                             IValidationEntry.SeverityLevel.Warning,
                             "CONFIG0204",
                             string.Format(LocalizableStrings.Authoring_CONFIG0204_UnknownArgumentForReplace, arg, nameof(ApplyFileRenamesToArgs).ToCamelCase(), nameof(Args).ToCamelCase())));
                    }
                    else
                    {
                        verifiedApplyFileRenamesToArgs.Add(arg);
                    }
                }

                IReadOnlyList<ManualInstructionModel> manualInstructions = LoadManualInstructionsFromJArray(action.Get<JArray>("ManualInstructions"), validationEntries);

                PostActionModel model = new(args, manualInstructions)
                {
                    Id = postActionId,
                    Description = description,
                    ActionId = actionId,
                    ContinueOnError = continueOnError,
                    Condition = postActionCondition,
                    ApplyFileRenamesToArgs = verifiedApplyFileRenamesToArgs,
                    ApplyFileRenamesToManualInstructions = applyFileRenamesToManualInstructions,
                };

                localizedPostActions.Add(model);
            }

            return localizedPostActions;
        }

        internal void Localize(PostActionLocalizationModel locModel)
        {
            _description = locModel.Description ?? Description;

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

        private static IReadOnlyList<ManualInstructionModel> LoadManualInstructionsFromJArray(JArray? jArray, List<IValidationEntry> validationEntries)
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
                    validationEntries.Add(
                    new ValidationEntry(
                        IValidationEntry.SeverityLevel.Warning,
                        "CONFIG0203",
                        string.Format(LocalizableStrings.Authoring_CONFIG0203_ManualInstructionIdIsNotUnique, id, i)));
                    id = null;
                }

                results.Add(new ManualInstructionModel(id, text, condition));
            }

            return results;
        }
    }
}
