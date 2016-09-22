using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    public class ConditionalConfig : IOperationConfig
    {
        public int Order => -7000;

        public string Key => "conditionals";

        public Guid Id => new Guid("3E8BCBF0-D631-45BA-A12D-FBF1DE03AA38");

        public IEnumerable<IOperationProvider> ConfigureFromJObject(IComponentManager componentManager, JObject rawConfiguration, IDirectory templateRoot, IVariableCollection variables, IParameterSet parameters)
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
                case ConditionalType.Xml:
                    setup = XmlConditionalSetup(evaluatorType, wholeLine, trimWhiteSpace, id);
                    break;
                case ConditionalType.Razor:
                    setup = RazorConditionalSetup(evaluatorType, wholeLine, trimWhiteSpace, id);
                    break;
                case ConditionalType.CLineComments:
                    setup = CStyleLineCommentsConditionalSetup(evaluatorType, wholeLine, trimWhiteSpace, id);
                    break;
                case ConditionalType.CNoComments:
                    setup = CStyleNoCommentsConditionalSetup(evaluatorType, wholeLine, trimWhiteSpace, id);
                    break;
                case ConditionalType.CBlockComments:
                    setup = CStyleBlockCommentConditionalSetup(evaluatorType, wholeLine, trimWhiteSpace, id);
                    break;
                case ConditionalType.HashSignLineComment:
                    setup = HashSignLineCommentConditionalSetup(evaluatorType, wholeLine, trimWhiteSpace, id);
                    break;
                default:
                    throw new Exception($"Unrecognized conditional type {style}");
            }

            return setup;
        }

        public static List<IOperationProvider> XmlConditionalSetup(string evaluatorType, bool wholeLine, bool trimWhiteSpace, string id)
        {
            // This is the operationId (flag) for the balanced nesting.
            string commentFixingOperationId = "Fix pseudo comments (XML)";

            // This is not an operationId (flag), it does not toggle the operation.
            // But conditional doesn't care, it takes the flags its given and sets them as appropriate.
            // It lets BalancedNesting know it's been reset.
            string commentFixingResetId = "Reset pseudo comment fixer (XML)";

            IOperationProvider balancedComments = new BalancedNesting("<!--", "-->", "-- >", commentFixingOperationId, commentFixingResetId);

            ConditionalTokens tokens = new ConditionalTokens
            {
                EndIfTokens = new[] { "#endif", "<!--#endif" },
                ActionableIfTokens = new[] { "<!--#if" },
                ActionableElseTokens = new[] { "#else", "<!--#else" },
                ActionableElseIfTokens = new[] { "#elseif", "<!--#elseif" },
                ActionableOperations = new[] { commentFixingOperationId, commentFixingResetId }
            };

            ConditionEvaluator evaluator = EvaluatorSelector.Select(evaluatorType);
            IOperationProvider conditional = new Conditional(tokens, wholeLine, trimWhiteSpace, evaluator, id);

            return new List<IOperationProvider>()
            {
                conditional,
                balancedComments
            };
        }

        public static List<IOperationProvider> RazorConditionalSetup(string evaluatorType, bool wholeLine, bool trimWhiteSpace, string id)
        {
            // This is the operationId (flag) for the balanced nesting
            string commentFixingOperationId = "Fix pseudo comments (Razor)";

            // This is not an operationId (flag), it does not toggle the operation.
            // But conditional doesn't care, it takes the flags its given and sets them as appropriate.
            // Tt lets BalanceNesting know it's been reset
            string commentFixingResetId = "Reset pseudo comment fixer (Razor)";

            IOperationProvider balancedComments = new BalancedNesting("@*", "*@", "* @", commentFixingOperationId, commentFixingResetId);

            ConditionalTokens tokens = new ConditionalTokens
            {
                EndIfTokens = new[] { "#endif", "@*#endif" },
                ActionableIfTokens = new[] { "@*#if" },
                ActionableElseTokens = new[] { "#else", "@*#else" },
                ActionableElseIfTokens = new[] { "#elseif", "@*#elseif" },
                ActionableOperations = new[] { commentFixingOperationId, commentFixingResetId }
            };

            ConditionEvaluator evaluator = EvaluatorSelector.Select(evaluatorType);
            IOperationProvider conditional = new Conditional(tokens, wholeLine, trimWhiteSpace, evaluator, id);

            return new List<IOperationProvider>()
            {
                conditional,
                balancedComments
            };
        }

        public static List<IOperationProvider> CStyleBlockCommentConditionalSetup(string evaluatorType, bool wholeLine, bool trimWhiteSpace, string id)
        {
            // This is the operationId (flag) for the balanced nesting
            string commentFixingOperationId = "Fix pseudo comments (C Block)";

            // This is not an operationId (flag), it does not toggle the operation.
            // But conditional doesn't care, it takes the flags its given and sets them as appropriate.
            // Tt lets BalanceNesting know it's been reset
            string commentFixingResetId = "Reset pseudo comment fixer (C Block)";

            IOperationProvider balancedComments = new BalancedNesting("/*", "*/", "* /", commentFixingOperationId, commentFixingResetId);
            ConditionalTokens tokens = new ConditionalTokens
            {
                EndIfTokens = new[] { "#endif", "/*#endif" },
                ActionableIfTokens = new[] { "/*#if" },
                ActionableElseTokens = new[] { "#else", "/*#else" },
                ActionableElseIfTokens = new[] { "#elseif", "/*#elseif" },
            };

            ConditionEvaluator evaluator = EvaluatorSelector.Select(evaluatorType);
            IOperationProvider conditional = new Conditional(tokens, wholeLine, trimWhiteSpace, evaluator, id);

            return new List<IOperationProvider>()
            {
                conditional,
                balancedComments
            };
        }

        public static List<IOperationProvider> CStyleLineCommentsConditionalSetup(string evaluatorType, bool wholeLine, bool trimWhiteSpace, string id)
        {
            string replaceOperationId = "Replacement (C style): (//) -> ()";
            string uncommentOperationId = "Uncomment (C style): (////) -> (//)";
            IOperationProvider uncomment = new Replacement("////", "//", uncommentOperationId);
            IOperationProvider commentReplace = new Replacement("//", string.Empty, replaceOperationId);

            ConditionalTokens tokens = new ConditionalTokens
            {
                IfTokens = new[] { "//#if" },
                ElseTokens = new[] { "//#else" },
                ElseIfTokens = new[] { "//#elseif" },
                EndIfTokens = new[] { "//#endif" },
                ActionableIfTokens = new[] { "////#if" },
                ActionableElseIfTokens = new[] { "////#elseif" },
                ActionableElseTokens = new[] { "////#else" },
                ActionableOperations = new[] { replaceOperationId, uncommentOperationId }
            };

            ConditionEvaluator evaluator = EvaluatorSelector.Select(evaluatorType);
            IOperationProvider conditional = new Conditional(tokens, wholeLine, trimWhiteSpace, evaluator, id);

            return new List<IOperationProvider>()
            {
                conditional,
                uncomment,
                commentReplace
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

        // TODO: test
        // this should work for nginx.conf, Perl, bash, etc.
        public static List<IOperationProvider> HashSignLineCommentConditionalSetup(string evaluatorType, bool wholeLine, bool trimWhiteSpace, string id)
        {
            string uncommentOperationId = "Uncomment (hash line): (##) -> (#)";
            string replaceOperationId = "Replacement (hash line): (#) -> ()";
            IOperationProvider uncomment = new Replacement("##", "#", uncommentOperationId);
            IOperationProvider commentReplace = new Replacement("#", "", replaceOperationId);

            ConditionalTokens tokens = new ConditionalTokens
            {
                IfTokens = new[] { "#if" },
                ElseTokens = new[] { "#else" },
                ElseIfTokens = new[] { "#elseif" },
                EndIfTokens = new[] { "#endif", "##endif" },
                ActionableIfTokens = new[] { "##if" },
                ActionableElseIfTokens = new[] { "##elseif" },
                ActionableElseTokens = new[] { "##else" },
                ActionableOperations = new[] { replaceOperationId, uncommentOperationId }
            };

            ConditionEvaluator evaluator = EvaluatorSelector.Select(evaluatorType);
            IOperationProvider conditional = new Conditional(tokens, wholeLine, trimWhiteSpace, evaluator, id);

            return new List<IOperationProvider>()
            {
                conditional,
                uncomment,
                commentReplace
            };
        }
    }
}