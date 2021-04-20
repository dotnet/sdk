// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    internal static class ConditionalLineCommentConfig
    {
        internal static List<IOperationProvider> ConfigureFromJObject(JObject rawConfiguration)
        {
            string token = rawConfiguration.ToString("token");

            if (string.IsNullOrWhiteSpace(token))
            {
                // this is the only required data, all the rest is optional
                throw new TemplateAuthoringException("Template authoring error. Token must be defined", "token");
            }

            ConditionalKeywords keywords = ConditionalKeywords.FromJObject(rawConfiguration);
            ConditionalOperationOptions options = ConditionalOperationOptions.FromJObject(rawConfiguration);

            return GenerateConditionalSetup(token, keywords, options);
        }

        internal static List<IOperationProvider> GenerateConditionalSetup(string token)
        {
            return GenerateConditionalSetup(token, new ConditionalKeywords(), new ConditionalOperationOptions());
        }

        internal static List<IOperationProvider> GenerateConditionalSetup(string token, ConditionalKeywords keywords, ConditionalOperationOptions options)
        {
            string uncommentOperationId = $"Uncomment (line): {token} -> ()";
            string reduceCommentOperationId = $"Reduce comment (line): ({token}{token}) -> ({token})";
            IOperationProvider uncomment = new Replacement(token.TokenConfig(), string.Empty, uncommentOperationId, options.OnByDefault);
            IOperationProvider reduceComment = new Replacement($"{token}{token}".TokenConfig(), token, reduceCommentOperationId, options.OnByDefault);

            List<ITokenConfig> ifTokens = new List<ITokenConfig>();
            List<ITokenConfig> actionableIfTokens = new List<ITokenConfig>();
            foreach (string ifKeyword in keywords.IfKeywords)
            {
                ifTokens.Add($"{token}{keywords.KeywordPrefix}{ifKeyword}".TokenConfig());
                actionableIfTokens.Add($"{token}{token}{keywords.KeywordPrefix}{ifKeyword}".TokenConfig());
            }

            List<ITokenConfig> elseTokens = new List<ITokenConfig>();
            List<ITokenConfig> actionableElseTokens = new List<ITokenConfig>();
            foreach (string elseKeyword in keywords.ElseKeywords)
            {
                elseTokens.Add($"{token}{keywords.KeywordPrefix}{elseKeyword}".TokenConfig());
                actionableElseTokens.Add($"{token}{token}{keywords.KeywordPrefix}{elseKeyword}".TokenConfig());
            }

            List<ITokenConfig> elseIfTokens = new List<ITokenConfig>();
            List<ITokenConfig> actionalElseIfTokens = new List<ITokenConfig>();
            foreach (string elseIfKeyword in keywords.ElseIfKeywords)
            {
                elseIfTokens.Add($"{token}{keywords.KeywordPrefix}{elseIfKeyword}".TokenConfig());
                actionalElseIfTokens.Add($"{token}{token}{keywords.KeywordPrefix}{elseIfKeyword}".TokenConfig());
            }

            List<ITokenConfig> endIfTokens = new List<ITokenConfig>();
            foreach (string endIfKeyword in keywords.EndIfKeywords)
            {
                endIfTokens.Add($"{token}{keywords.KeywordPrefix}{endIfKeyword}".TokenConfig());
                endIfTokens.Add($"{token}{token}{keywords.KeywordPrefix}{endIfKeyword}".TokenConfig());
            }

            ConditionalTokens conditionalTokens = new ConditionalTokens
            {
                IfTokens = ifTokens,
                ElseTokens = elseTokens,
                ElseIfTokens = elseIfTokens,
                EndIfTokens = endIfTokens,
                ActionableIfTokens = actionableIfTokens,
                ActionableElseTokens = actionableElseTokens,
                ActionableElseIfTokens = actionalElseIfTokens,
                ActionableOperations = new[] { uncommentOperationId, reduceCommentOperationId }
            };

            ConditionEvaluator evaluator = EvaluatorSelector.Select(options.EvaluatorType);
            IOperationProvider conditional = new Conditional(conditionalTokens, options.WholeLine, options.TrimWhitespace, evaluator, options.Id, options.OnByDefault);

            return new List<IOperationProvider>()
            {
                conditional,
                reduceComment,
                uncomment
            };
        }
    }
}
