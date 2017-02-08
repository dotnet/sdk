using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    public static class ConditionalLineCommentConfig
    {
        public static List<IOperationProvider> ConfigureFromJObject(JObject rawConfiguration)
        {
            string commentToken = rawConfiguration.ToString("comment");

            if (string.IsNullOrWhiteSpace(commentToken))
            {   // this is the only required data, all the rest is optional
                throw new Exception("Template authoring error. Comment must be defined");
            }

            ConditionalKeywords keywords = ConditionalKeywords.FromJObject(rawConfiguration);
            ConditionalOperationOptions options = ConditionalOperationOptions.FromJObject(rawConfiguration);

            return GenerateConditionalSetup(commentToken, keywords, options);
        }

        public static List<IOperationProvider> GenerateConditionalSetup(string commentToken)
        {
            return GenerateConditionalSetup(commentToken, new ConditionalKeywords(), new ConditionalOperationOptions());
        }

        public static List<IOperationProvider> GenerateConditionalSetup(string commentToken, ConditionalKeywords keywords, ConditionalOperationOptions options)
        {
            string uncommentOperationId = $"Uncomment (line): {commentToken} -> ()";
            string reduceCommentOperationId = $"Reduce comment (line): ({commentToken}{commentToken}) -> ({commentToken})";
            IOperationProvider uncomment = new Replacement(commentToken, string.Empty, uncommentOperationId);
            IOperationProvider reduceComment = new Replacement($"{commentToken}{commentToken}", commentToken, reduceCommentOperationId);

            ConditionalTokens conditionalTokens = new ConditionalTokens
            {
                IfTokens = new[] { $"{commentToken}{keywords.KeywordPrefix}{keywords.IfKeyword}" },
                ElseTokens = new[] { $"{commentToken}{keywords.KeywordPrefix}{keywords.ElseKeyword}" },
                ElseIfTokens = new[] { $"{commentToken}{keywords.KeywordPrefix}{keywords.ElseIfKeyword}" },
                EndIfTokens = new[] { $"{commentToken}{keywords.KeywordPrefix}{keywords.EndIfKeyword}", $"{commentToken}{commentToken}{keywords.KeywordPrefix}{keywords.EndIfKeyword}" },
                ActionableIfTokens = new[] { $"{commentToken}{commentToken}{keywords.KeywordPrefix}{keywords.IfKeyword}" },
                ActionableElseTokens = new[] { $"{commentToken}{commentToken}{keywords.KeywordPrefix}{keywords.ElseKeyword}" },
                ActionableElseIfTokens = new[] { $"{commentToken}{commentToken}{keywords.KeywordPrefix}{keywords.ElseIfKeyword}" },
                ActionableOperations = new[] { uncommentOperationId, reduceCommentOperationId }
            };

            ConditionEvaluator evaluator = EvaluatorSelector.Select(options.EvaluatorType);
            IOperationProvider conditional = new Conditional(conditionalTokens, options.WholeLine, options.TrimWhitespace, evaluator, options.Id);

            return new List<IOperationProvider>()
            {
                conditional,
                reduceComment,
                uncomment
            };
        }
    }
}
