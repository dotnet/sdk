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

        public IEnumerable<IOperationProvider> ConfigureFromJObject(JObject rawConfiguration, IDirectory templateRoot)
        {
            string commentStyle = rawConfiguration.ToString("commentStyle");
            IEnumerable<IOperationProvider> operations = null;

            if (string.IsNullOrEmpty(commentStyle) || string.Equals(commentStyle, "custom", StringComparison.OrdinalIgnoreCase))
            {
                operations = ConditionalCustomConfig.ConfigureFromJObject(rawConfiguration);
            }
            else if (string.Equals(commentStyle, "line", StringComparison.OrdinalIgnoreCase))
            {
                operations = ConditionalLineCommentConfig.ConfigureFromJObject(rawConfiguration);
            }
            else if (string.Equals(commentStyle, "block", StringComparison.OrdinalIgnoreCase))
            {
                operations = ConditionalBlockCommentConfig.ConfigureFromJObject(rawConfiguration);
            }
            else
            {
                throw new Exception($"Template authoring error. Invalid comment style [{commentStyle}].");
            }

            foreach (IOperationProvider op in operations)
            {
                yield return op;
            }
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
                    // Most line comment conditional tags use: <comment symbol><keyword prefix><keyword>
                    // But for this one, the '#' comment symbol is all that's needed, so it uses an empty keyword prefix.
                    // So we end up with regular conditionals suchs as '#if', '#else'
                    // and actionables such as '##if'
                    ConditionalKeywords keywords = new ConditionalKeywords()
                    {
                        KeywordPrefix = string.Empty
                    };
                    setup = ConditionalLineCommentConfig.GenerateConditionalSetup("#", keywords, new ConditionalOperationOptions());
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

        // Nice to have: Generalize this type of setup similarly to Line, Block, & Custom
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

        // Nice to have: Generalize this type of setup similarly to Line, Block, & Custom
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