using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    public static class ConditionalBlockCommentConfig
    {
        public static List<IOperationProvider> ConfigureFromJObject(JObject rawConfiguration)
        {
            string startComment = rawConfiguration.ToString("startComment");
            string endComment = rawConfiguration.ToString("endComment");

            if (string.IsNullOrWhiteSpace(startComment) || string.IsNullOrWhiteSpace(endComment))
            {
                throw new Exception($"Template authoring error. StartComment and EndComment must be defined");
            }

            string pseudoEndComment = rawConfiguration.ToString("pseudoEndComment");

            ConditionalKeywords keywords = ConditionalKeywords.FromJObject(rawConfiguration);
            ConditionalOperationOptions options = ConditionalOperationOptions.FromJObject(rawConfiguration);

            if (string.IsNullOrWhiteSpace(pseudoEndComment))
            {
                return GenerateConditionalSetup(startComment, endComment, keywords, options);
            }
            else
            {
                return GenerateConditionalSetup(startComment, endComment, pseudoEndComment, keywords, options);
            }
        }

        public static List<IOperationProvider> GenerateConditionalSetup(string startComment, string endComment)
        {
            return GenerateConditionalSetup(startComment, endComment, new ConditionalKeywords(), new ConditionalOperationOptions());
        }

        public static List<IOperationProvider> GenerateConditionalSetup(string startComment, string endComment, ConditionalKeywords keywords, ConditionalOperationOptions options)
        {
            string pseudoEndComment;

            if (endComment.Length < 2)
            {   // end comment must be at least two characters to have a programmatically determined pseudo-comment
                pseudoEndComment = null;
            }
            else
            {
                // add a space just before the final character of the end comment
                pseudoEndComment = endComment.Substring(0, endComment.Length - 1) + " " + endComment.Substring(endComment.Length - 1);
            }

            return GenerateConditionalSetup(startComment, endComment, pseudoEndComment, keywords, options);
        }

        public static List<IOperationProvider> GenerateConditionalSetup(string startComment, string endComment, string pseudoEndComment)
        {
            return GenerateConditionalSetup(startComment, endComment, pseudoEndComment, new ConditionalKeywords(), new ConditionalOperationOptions());
        }

        public static List<IOperationProvider> GenerateConditionalSetup(string startComment, string endComment, string pseudoEndComment, ConditionalKeywords keywords, ConditionalOperationOptions options)
        {
            ConditionEvaluator evaluator = EvaluatorSelector.Select(options.EvaluatorType);

            ConditionalTokens tokens = new ConditionalTokens
            {
                EndIfTokens = new[] { $"{keywords.KeywordPrefix}{keywords.EndIfKeyword}", $"{startComment}{keywords.KeywordPrefix}{keywords.EndIfKeyword}" },
                ActionableIfTokens = new[] { $"{startComment}{keywords.KeywordPrefix}{keywords.IfKeyword}" },
                ActionableElseTokens = new[] { $"{keywords.KeywordPrefix}{keywords.ElseKeyword}", $"{startComment}{keywords.KeywordPrefix}{keywords.ElseKeyword}" },
                ActionableElseIfTokens = new[] { $"{keywords.KeywordPrefix}{keywords.ElseIfKeyword}", $"{startComment}{keywords.KeywordPrefix}{keywords.ElseIfKeyword}" },
            };

            if (!string.IsNullOrWhiteSpace(pseudoEndComment))
            {
                Guid operationIdGuid = new Guid();
                string commentFixOperationId = $"Fix pseudo comments ({pseudoEndComment} {operationIdGuid.ToString()})";
                string commentFixResetId = $"Reset pseudo comment fixer ({pseudoEndComment} {operationIdGuid.ToString()})";

                tokens.ActionableOperations = new[] { commentFixOperationId, commentFixResetId };

                IOperationProvider balancedComments = new BalancedNesting(startComment, endComment, pseudoEndComment, commentFixOperationId, commentFixResetId);
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
