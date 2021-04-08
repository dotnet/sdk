using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    internal static class ConditionalBlockCommentConfig
    {
        internal static List<IOperationProvider> ConfigureFromJObject(JObject rawConfiguration)
        {
            string startToken = rawConfiguration.ToString("startToken");
            string endToken = rawConfiguration.ToString("endToken");

            if (string.IsNullOrWhiteSpace(startToken))
            {
                throw new TemplateAuthoringException($"Template authoring error. StartToken must be defined", "StartToken");
            }
            else if (string.IsNullOrWhiteSpace(endToken))
            {
                throw new TemplateAuthoringException($"Template authoring error. EndToken must be defined", "EndToken");
            }

            string pseudoEndToken = rawConfiguration.ToString("pseudoEndToken");

            ConditionalKeywords keywords = ConditionalKeywords.FromJObject(rawConfiguration);
            ConditionalOperationOptions options = ConditionalOperationOptions.FromJObject(rawConfiguration);

            if (string.IsNullOrWhiteSpace(pseudoEndToken))
            {
                return GenerateConditionalSetup(startToken, endToken, keywords, options);
            }
            else
            {
                return GenerateConditionalSetup(startToken, endToken, pseudoEndToken, keywords, options);
            }
        }

        internal static List<IOperationProvider> GenerateConditionalSetup(string startToken, string endToken)
        {
            return GenerateConditionalSetup(startToken, endToken, new ConditionalKeywords(), new ConditionalOperationOptions());
        }

        internal static List<IOperationProvider> GenerateConditionalSetup(string startToken, string endToken, ConditionalKeywords keywords, ConditionalOperationOptions options)
        {
            string pseudoEndComment;

            if (endToken.Length < 2)
            {   // end comment must be at least two characters to have a programmatically determined pseudo-comment
                pseudoEndComment = null;
            }
            else
            {
                // add a space just before the final character of the end comment
                pseudoEndComment = endToken.Substring(0, endToken.Length - 1) + " " + endToken.Substring(endToken.Length - 1);
            }

            return GenerateConditionalSetup(startToken, endToken, pseudoEndComment, keywords, options);
        }

        internal static List<IOperationProvider> GenerateConditionalSetup(string startToken, string endToken, string pseudoEndToken)
        {
            return GenerateConditionalSetup(startToken, endToken, pseudoEndToken, new ConditionalKeywords(), new ConditionalOperationOptions());
        }

        internal static List<IOperationProvider> GenerateConditionalSetup(string startToken, string endToken, string pseudoEndToken, ConditionalKeywords keywords, ConditionalOperationOptions options)
        {
            ConditionEvaluator evaluator = EvaluatorSelector.Select(options.EvaluatorType);

            List<ITokenConfig> endIfTokens = new List<ITokenConfig>();
            foreach (string endIfKeyword in keywords.EndIfKeywords)
            {
                endIfTokens.Add($"{keywords.KeywordPrefix}{endIfKeyword}".TokenConfig());
                endIfTokens.Add($"{startToken}{keywords.KeywordPrefix}{endIfKeyword}".TokenConfig());
            }

            List<ITokenConfig> actionableIfTokens = new List<ITokenConfig>();
            foreach (string ifKeyword in keywords.IfKeywords)
            {
                actionableIfTokens.Add($"{startToken}{keywords.KeywordPrefix}{ifKeyword}".TokenConfig());
            }

            List<ITokenConfig> actionableElseTokens = new List<ITokenConfig>();
            foreach (string elseKeyword in keywords.ElseKeywords)
            {
                actionableElseTokens.Add($"{keywords.KeywordPrefix}{elseKeyword}".TokenConfig());
                actionableElseTokens.Add($"{startToken}{keywords.KeywordPrefix}{elseKeyword}".TokenConfig());
            }

            List<ITokenConfig> actionableElseIfTokens = new List<ITokenConfig>();
            foreach (string elseIfKeyword in keywords.ElseIfKeywords)
            {
                actionableElseIfTokens.Add($"{keywords.KeywordPrefix}{elseIfKeyword}".TokenConfig());
                actionableElseIfTokens.Add($"{startToken}{keywords.KeywordPrefix}{elseIfKeyword}".TokenConfig());
            }

            ConditionalTokens tokens = new ConditionalTokens
            {
                EndIfTokens = endIfTokens,
                ActionableIfTokens = actionableIfTokens,
                ActionableElseTokens = actionableElseTokens,
                ActionableElseIfTokens = actionableElseIfTokens
            };

            if (!string.IsNullOrWhiteSpace(pseudoEndToken))
            {
                Guid operationIdGuid = new Guid();
                string commentFixOperationId = $"Fix pseudo tokens ({pseudoEndToken} {operationIdGuid})";
                string commentFixResetId = $"Reset pseudo token fixer ({pseudoEndToken} {operationIdGuid})";

                tokens.ActionableOperations = new[] { commentFixOperationId, commentFixResetId };

                IOperationProvider balancedComments = new BalancedNesting(startToken.TokenConfig(), endToken.TokenConfig(), pseudoEndToken.TokenConfig(), commentFixOperationId, commentFixResetId, options.OnByDefault);
                IOperationProvider conditional = new Conditional(tokens, options.WholeLine, options.TrimWhitespace, evaluator, options.Id, options.OnByDefault);

                return new List<IOperationProvider>()
                {
                    conditional,
                    balancedComments
                };
            }
            else
            {
                IOperationProvider conditional = new Conditional(tokens, options.WholeLine, options.TrimWhitespace, evaluator, options.Id, options.OnByDefault);
                return new List<IOperationProvider>()
                {
                    conditional
                };
            }
        }
    }
}
