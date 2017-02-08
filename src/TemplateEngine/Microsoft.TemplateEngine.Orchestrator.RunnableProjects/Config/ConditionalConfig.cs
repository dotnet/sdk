using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    public class ConditionalConfig : IOperationConfig
    {
        public string Key => Conditional.OperationName;

        public Guid Id => new Guid("3E8BCBF0-D631-45BA-A12D-FBF1DE03AA38");

        // TODO: create handling for the new-style (simplified) setup
        //
        // This is the old-style configuration - before conditional setup was unified
        public IEnumerable<IOperationProvider> ConfigureFromJObject(JObject rawConfiguration, IDirectory templateRoot)
        {
            IReadOnlyList<string> ifToken = rawConfiguration.ArrayAsStrings("if");
            IReadOnlyList<string> elseToken = rawConfiguration.ArrayAsStrings("else");
            IReadOnlyList<string> elseIfToken = rawConfiguration.ArrayAsStrings("elseif");
            IReadOnlyList<string> actionableIfToken = rawConfiguration.ArrayAsStrings("actionableIf");
            IReadOnlyList<string> actionableElseToken = rawConfiguration.ArrayAsStrings("actionableElse");
            IReadOnlyList<string> actionableElseIfToken = rawConfiguration.ArrayAsStrings("actionableElseif");
            IReadOnlyList<string> actionsToken = rawConfiguration.ArrayAsStrings("actions");
            IReadOnlyList<string> endIfToken = rawConfiguration.ArrayAsStrings("endif");
            string id = rawConfiguration.ToString("id");
            bool trim = rawConfiguration.ToBool("trim");
            bool wholeLine = rawConfiguration.ToBool("wholeLine");

            string evaluatorName = rawConfiguration.ToString("evaluator");
            ConditionEvaluator evaluator = EvaluatorSelector.Select(evaluatorName);

            ConditionalTokens tokenVariants = new ConditionalTokens
            {
                IfTokens = ifToken,
                ElseTokens = elseToken,
                ElseIfTokens = elseIfToken,
                EndIfTokens = endIfToken,
                ActionableElseIfTokens = actionableElseIfToken,
                ActionableElseTokens = actionableElseToken,
                ActionableIfTokens = actionableIfToken,
                ActionableOperations = actionsToken
            };

            yield return new Conditional(tokenVariants, wholeLine, trim, evaluator, id);
        }

        public static IReadOnlyList<IOperationProvider> ConditionalSetup(ConditionalType style, string evaluatorType, bool wholeLine, bool trimWhiteSpace, string id)
        {
            List<IOperationProvider> setup;

            switch (style)
            {
                case ConditionalType.MSBuild:
                    setup = MSBuildConditionalSetup(evaluatorType, wholeLine, trimWhiteSpace, id);
                    break;
                case ConditionalType.Xml:
                    setup = ConditionalBlockCommentConfig.GenerateConditionalSetup("<!--", "-->");
                    break;
                case ConditionalType.Razor:
                    setup = ConditionalBlockCommentConfig.GenerateConditionalSetup("@*", "*@");
                    break;
                case ConditionalType.CLineComments:
                    setup = ConditionalLineCommentConfig.GenerateConditionalSetup("//");
                    break;
                case ConditionalType.CNoComments:
                    setup = CStyleNoCommentsConditionalSetup(evaluatorType, wholeLine, trimWhiteSpace, id);
                    break;
                case ConditionalType.CBlockComments:
                    setup = ConditionalBlockCommentConfig.GenerateConditionalSetup("/*", "*/");
                    break;
                case ConditionalType.HashSignLineComment:
                    setup = ConditionalLineCommentConfig.GenerateConditionalSetup("#");
                    break;
                case ConditionalType.RemLineComment:
                    setup = ConditionalLineCommentConfig.GenerateConditionalSetup("rem ");
                    break;
                case ConditionalType.HamlLineComment:
                    setup = ConditionalLineCommentConfig.GenerateConditionalSetup("-#");
                    break;
                case ConditionalType.JsxBlockComment:
                    setup = ConditionalBlockCommentConfig.GenerateConditionalSetup("{/*", "*/}");
                    break;
                default:
                    throw new Exception($"Unrecognized conditional type {style}");
            }

            return setup;
        }

        public static List<IOperationProvider> MSBuildConditionalSetup(string evaluatorType, bool wholeLine, bool trimWhiteSpace, string id)
        {
            ConditionEvaluator evaluator = EvaluatorSelector.Select(evaluatorType);
            IOperationProvider conditional = new InlineMarkupConditional(
                new MarkupTokens("<", "</", ">", "/>", "Condition=\"", "\""),
                wholeLine,
                trimWhiteSpace,
                evaluator,
                "$({0})",
                id
            );

            return new List<IOperationProvider>()
            {
                conditional
            };
        }

        public static List<IOperationProvider> CStyleNoCommentsConditionalSetup(string evaluatorType, bool wholeLine, bool trimWhiteSpace, string id)
        {
            ConditionalTokens tokens = new ConditionalTokens
            {
                IfTokens = new[] { "#if" },
                ElseTokens = new[] { "#else" },
                ElseIfTokens = new[] { "#elseif" },
                EndIfTokens = new[] { "#endif" }
            };

            ConditionEvaluator evaluator = EvaluatorSelector.Select(evaluatorType);
            IOperationProvider conditional = new Conditional(tokens, wholeLine, trimWhiteSpace, evaluator, id);

            return new List<IOperationProvider>()
            {
                conditional
            };
        }
    }
}