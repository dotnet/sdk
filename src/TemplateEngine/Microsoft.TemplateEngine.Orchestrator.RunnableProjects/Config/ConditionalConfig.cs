using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    internal class ConditionalConfig : IOperationConfig
    {
        public string Key => Conditional.OperationName;

        public Guid Id => new Guid("3E8BCBF0-D631-45BA-A12D-FBF1DE03AA38");

        public IEnumerable<IOperationProvider> ConfigureFromJObject(JObject rawConfiguration, IDirectory templateRoot)
        {
            string commentStyle = rawConfiguration.ToString("style");
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
                throw new TemplateAuthoringException($"Template authoring error. Invalid comment style [{commentStyle}].", "style");
            }

            foreach (IOperationProvider op in operations)
            {
                yield return op;
            }
        }

        internal static IReadOnlyList<IOperationProvider> ConditionalSetup(ConditionalType style, string evaluatorType, bool wholeLine, bool trimWhiteSpace, string id)
        {
            List<IOperationProvider> setup;

            switch (style)
            {
                case ConditionalType.MSBuild:
                    setup = new List<IOperationProvider>();
                    setup.AddRange(MSBuildConditionalSetup(evaluatorType, wholeLine, trimWhiteSpace, "msbuild-conditional"));
                    setup.AddRange(ConditionalBlockCommentConfig.GenerateConditionalSetup("<!--", "-->"));
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
                case ConditionalType.VB:
                    setup = ConditionalLineCommentConfig.GenerateConditionalSetup("", new ConditionalKeywords
                    {
                        IfKeywords = new[] { "If" },
                        ElseIfKeywords = new[] { "ElseIf" },
                        ElseKeywords = new[] { "Else" },
                        EndIfKeywords = new[] { "End If" },
                        KeywordPrefix = "#"
                    }, new ConditionalOperationOptions
                    {
                        EvaluatorType = "VB",
                        WholeLine = true
                    });
                    break;
                default:
                    throw new Exception($"Unrecognized conditional type {style}");
            }

            return setup;
        }

        // Nice to have: Generalize this type of setup similarly to Line, Block, & Custom
        internal static List<IOperationProvider> MSBuildConditionalSetup(string evaluatorType, bool wholeLine, bool trimWhiteSpace, string id)
        {
            ConditionEvaluator evaluator = EvaluatorSelector.Select(evaluatorType);
            IOperationProvider conditional = new InlineMarkupConditional(
                new MarkupTokens("<".TokenConfig(), "</".TokenConfig(), ">".TokenConfig(), "/>".TokenConfig(), "Condition=\"".TokenConfig(), "\"".TokenConfig()),
                wholeLine,
                trimWhiteSpace,
                evaluator,
                "$({0})",
                id,
                true
            );

            return new List<IOperationProvider>()
            {
                conditional
            };
        }

        // Nice to have: Generalize this type of setup similarly to Line, Block, & Custom
        internal static List<IOperationProvider> CStyleNoCommentsConditionalSetup(string evaluatorType, bool wholeLine, bool trimWhiteSpace, string id)
        {
            ConditionalKeywords defaultKeywords = new ConditionalKeywords();

            List<ITokenConfig> ifTokens = new List<ITokenConfig>();
            List<ITokenConfig> elseifTokens = new List<ITokenConfig>();
            List<ITokenConfig> elseTokens = new List<ITokenConfig>();
            List<ITokenConfig> endifTokens = new List<ITokenConfig>();

            foreach (string ifKeyword in defaultKeywords.IfKeywords)
            {
                ifTokens.Add($"{defaultKeywords.KeywordPrefix}{ifKeyword}".TokenConfig());
            }

            foreach (string elseifKeyword in defaultKeywords.ElseIfKeywords)
            {
                elseifTokens.Add($"{defaultKeywords.KeywordPrefix}{elseifKeyword}".TokenConfig());
            }

            foreach (string elseKeyword in defaultKeywords.ElseKeywords)
            {
                elseTokens.Add($"{defaultKeywords.KeywordPrefix}{elseKeyword}".TokenConfig());
            }

            foreach (string endifKeyword in defaultKeywords.EndIfKeywords)
            {
                endifTokens.Add($"{defaultKeywords.KeywordPrefix}{endifKeyword}".TokenConfig());
            }

            ConditionalTokens tokens = new ConditionalTokens
            {
                IfTokens = ifTokens,
                ElseTokens = elseTokens,
                ElseIfTokens = elseifTokens,
                EndIfTokens = endifTokens
            };

            ConditionEvaluator evaluator = EvaluatorSelector.Select(evaluatorType);
            IOperationProvider conditional = new Conditional(tokens, wholeLine, trimWhiteSpace, evaluator, id, true);

            return new List<IOperationProvider>()
            {
                conditional
            };
        }
    }
}
