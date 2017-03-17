using System.Collections.Generic;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    public static class ConditionalLineCommentConfig
    {
        public static List<IOperationProvider> ConfigureFromJObject(JObject rawConfiguration)
        {
            string token = rawConfiguration.ToString("token");

            if (string.IsNullOrWhiteSpace(token))
            {   // this is the only required data, all the rest is optional
                throw new TemplateAuthoringException("Template authoring error. Token must be defined", "token");
            }

            ConditionalKeywords keywords = ConditionalKeywords.FromJObject(rawConfiguration);
            ConditionalOperationOptions options = ConditionalOperationOptions.FromJObject(rawConfiguration);

            return GenerateConditionalSetup(token, keywords, options);
        }

        public static List<IOperationProvider> GenerateConditionalSetup(string token)
        {
            return GenerateConditionalSetup(token, new ConditionalKeywords(), new ConditionalOperationOptions());
        }

        public static List<IOperationProvider> GenerateConditionalSetup(string token, ConditionalKeywords keywords, ConditionalOperationOptions options)
        {
            string uncommentOperationId = $"Uncomment (line): {token} -> ()";
            string reduceCommentOperationId = $"Reduce comment (line): ({token}{token}) -> ({token})";
            IOperationProvider uncomment = new Replacement(token.TokenConfig(), string.Empty, uncommentOperationId);
            IOperationProvider reduceComment = new Replacement($"{token}{token}".TokenConfig(), token, reduceCommentOperationId);

            ConditionalTokens conditionalTokens = new ConditionalTokens
            {
                IfTokens = new[] { $"{token}{keywords.KeywordPrefix}{keywords.IfKeyword}" }.TokenConfigs(),
                ElseTokens = new[] { $"{token}{keywords.KeywordPrefix}{keywords.ElseKeyword}" }.TokenConfigs(),
                ElseIfTokens = new[] { $"{token}{keywords.KeywordPrefix}{keywords.ElseIfKeyword}" }.TokenConfigs(),
                EndIfTokens = new[] { $"{token}{keywords.KeywordPrefix}{keywords.EndIfKeyword}", $"{token}{token}{keywords.KeywordPrefix}{keywords.EndIfKeyword}" }.TokenConfigs(),
                ActionableIfTokens = new[] { $"{token}{token}{keywords.KeywordPrefix}{keywords.IfKeyword}" }.TokenConfigs(),
                ActionableElseTokens = new[] { $"{token}{token}{keywords.KeywordPrefix}{keywords.ElseKeyword}" }.TokenConfigs(),
                ActionableElseIfTokens = new[] { $"{token}{token}{keywords.KeywordPrefix}{keywords.ElseIfKeyword}" }.TokenConfigs(),
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
