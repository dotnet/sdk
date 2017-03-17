using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    public static class ConditionalBlockCommentConfig
    {
        public static List<IOperationProvider> ConfigureFromJObject(JObject rawConfiguration)
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

        public static List<IOperationProvider> GenerateConditionalSetup(string startToken, string endToken)
        {
            return GenerateConditionalSetup(startToken, endToken, new ConditionalKeywords(), new ConditionalOperationOptions());
        }

        public static List<IOperationProvider> GenerateConditionalSetup(string startToken, string endToken, ConditionalKeywords keywords, ConditionalOperationOptions options)
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

        public static List<IOperationProvider> GenerateConditionalSetup(string startToken, string endToken, string pseudoEndToken)
        {
            return GenerateConditionalSetup(startToken, endToken, pseudoEndToken, new ConditionalKeywords(), new ConditionalOperationOptions());
        }

        public static List<IOperationProvider> GenerateConditionalSetup(string startToken, string endToken, string pseudoEndToken, ConditionalKeywords keywords, ConditionalOperationOptions options)
        {
            ConditionEvaluator evaluator = EvaluatorSelector.Select(options.EvaluatorType);

            ConditionalTokens tokens = new ConditionalTokens
            {
                EndIfTokens = new[] { $"{keywords.KeywordPrefix}{keywords.EndIfKeyword}".TokenConfig(), $"{startToken}{keywords.KeywordPrefix}{keywords.EndIfKeyword}".TokenConfig() },
                ActionableIfTokens = new[] { $"{startToken}{keywords.KeywordPrefix}{keywords.IfKeyword}".TokenConfig() },
                ActionableElseTokens = new[] { $"{keywords.KeywordPrefix}{keywords.ElseKeyword}".TokenConfig(), $"{startToken}{keywords.KeywordPrefix}{keywords.ElseKeyword}".TokenConfig() },
                ActionableElseIfTokens = new[] { $"{keywords.KeywordPrefix}{keywords.ElseIfKeyword}".TokenConfig(), $"{startToken}{keywords.KeywordPrefix}{keywords.ElseIfKeyword}".TokenConfig() },
            };

            if (!string.IsNullOrWhiteSpace(pseudoEndToken))
            {
                Guid operationIdGuid = new Guid();
                string commentFixOperationId = $"Fix pseudo tokens ({pseudoEndToken} {operationIdGuid})";
                string commentFixResetId = $"Reset pseudo token fixer ({pseudoEndToken} {operationIdGuid})";

                tokens.ActionableOperations = new[] { commentFixOperationId, commentFixResetId };

                IOperationProvider balancedComments = new BalancedNesting(startToken.TokenConfig(), endToken.TokenConfig(), pseudoEndToken.TokenConfig(), commentFixOperationId, commentFixResetId);
                IOperationProvider conditional = new Conditional(tokens, options.WholeLine, options.TrimWhitespace, evaluator, options.Id);

                return new List<IOperationProvider>()
                {
                    conditional,
                    balancedComments
                };
            }
            else
            {
                IOperationProvider conditional = new Conditional(tokens, options.WholeLine, options.TrimWhitespace, evaluator, options.Id);
                return new List<IOperationProvider>()
                {
                    conditional
                };
            }
        }
    }
}
