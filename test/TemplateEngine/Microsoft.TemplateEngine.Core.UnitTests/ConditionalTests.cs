using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Expressions.Cpp;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Core.Util;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public partial class ConditionalTests : TestBase
    {
        protected IProcessor SetupRazorStyleProcessor(VariableCollection vc)
        {
            IOperationProvider[] operations = RazorStyleCommentConditionalsOperations;
            return SetupTestProcessor(operations, vc);
        }

        protected IProcessor SetupMadeUpStyleProcessor(VariableCollection vc)
        {
            IOperationProvider[] operations = MadeUpConditionalsOperations;
            return SetupTestProcessor(operations, vc);
        }

        protected IProcessor SetupXmlStyleProcessor(VariableCollection vc)
        {
            IOperationProvider[] operations = XmlStyleCommentConditionalsOperations;
            return SetupTestProcessor(operations, vc);
        }

        internal IProcessor SetupCStyleWithCommentsProcessor(VariableCollection vc)
        {
            IOperationProvider[] operations = CStyleWithCommentsConditionalOperations;
            return SetupTestProcessor(operations, vc);
        }

        protected IProcessor SetupCStyleNoCommentsProcessor(VariableCollection vc)
        {
            IOperationProvider[] operations = CStyleNoCommentsConditionalOperations;
            return SetupTestProcessor(operations, vc);
        }

        protected IProcessor SetupHashSignLineCommentsProcessor(VariableCollection vc)
        {
            IOperationProvider[] operations = HashSignLineCommentConditionalOperations;
            return SetupTestProcessor(operations, vc);
        }

        protected IProcessor SetupBatFileRemLineCommentsProcessor(VariableCollection vc)
        {
            IOperationProvider[] operations = BatFileRemLineCommentConditionalOperations;
            return SetupTestProcessor(operations, vc);
        }

        ///
        /// Sets up a processor with the input params.
        ///
        private IProcessor SetupTestProcessor(IOperationProvider[] operations, VariableCollection vc)
        {
            EngineConfig cfg = new EngineConfig(EnvironmentSettings, vc);
            return Processor.Create(cfg, operations);
        }

        /// <summary>
        /// Second attempt at xml comment processing.
        /// </summary>
        private static IOperationProvider[] XmlStyleCommentConditionalsOperations
        {
            get
            {
                // This is the operationId (flag) for the balanced nesting
                string commentFixingOperationId = "Fix pseudo comments";

                // This is not an operationId (flag), it does not toggle the operation.
                // But conditional doesn't care, it takes the flags its given and sets them as appropriate.
                // It lets BalanceNesting know it's been reset
                string commentFixingResetId = "Reset pseudo comment fixer";

                ConditionalTokens tokenVariants = new ConditionalTokens
                {
                    EndIfTokens = new[] { "#endif", "<!--#endif" },
                    ActionableIfTokens = new[] { "<!--#if" },
                    ActionableElseTokens = new[] { "#else", "<!--#else" },
                    ActionableElseIfTokens = new[] { "#elseif", "<!--#elseif" },
                    ActionableOperations = new[] { commentFixingOperationId, commentFixingResetId }
                };

                IOperationProvider[] operations =
                {
                    new Conditional(tokenVariants, true, true, CppStyleEvaluatorDefinition.Evaluate, null),
                    new BalancedNesting("<!--", "-->", "-- >", commentFixingOperationId, commentFixingResetId)
                };

                return operations;
            }
        }

        /// <summary>
        /// Returns an IOperationProvider setup for razor style comment processing.
        /// </summary>
        private static IOperationProvider[] RazorStyleCommentConditionalsOperations
        {
            get
            {
                // This is the operationId (flag) for the balanced nesting
                string commentFixingOperationId = "Fix pseudo comments";

                // This is not an operationId (flag), it does not toggle the operation.
                // But conditional doesn't care, it takes the flags its given and sets them as appropriate.
                // Tt lets BalanceNesting know it's been reset
                string commentFixingResetId = "Reset pseudo comment fixer";

                ConditionalTokens tokenVariants = new ConditionalTokens
                {
                    EndIfTokens = new[] { "#endif", "@*#endif" },
                    ActionableIfTokens = new[] { "@*#if" },
                    ActionableElseTokens = new[] { "#else", "@*#else" },
                    ActionableElseIfTokens = new[] { "#elseif", "@*#elseif" },
                    ActionableOperations = new[] { commentFixingOperationId, commentFixingResetId }
                };

                IOperationProvider[] operations =
                {
                    new Conditional(tokenVariants, true, true, CppStyleEvaluatorDefinition.Evaluate, null),
                    new BalancedNesting("@*", "*@", "* @", commentFixingOperationId, commentFixingResetId)
                };

                return operations;
            }
        }


        /// <summary>
        /// This started as a proof-of-concept / demonstration of having multiple tokens of each type,
        /// not to mention the arbitrariness of the conditional tokens.
        /// It could go away, in conjunction with updating the unit tests that use it.
        /// </summary>
        private static IOperationProvider[] MadeUpConditionalsOperations
        {
            get
            {
                // this is normally handled in the config setup
                string replaceOperationId = "Replacement (//) ()";
                string uncommentOperationId = "Uncomment (////) -> (//)";

                ConditionalTokens tokenVariants = new ConditionalTokens
                {
                    IfTokens = new[] { "//#if", "//#check" },
                    ElseTokens = new[] { "//#else", "//#otherwise" },
                    ElseIfTokens = new[] { "//#elseif", "//#nextcheck" },
                    EndIfTokens = new[] { "//#endif", "//#stop", "//#done", "//#nomore" },
                    ActionableIfTokens = new[] { "////#if", "////#check", "//#Z_if" },
                    ActionableElseTokens = new[] { "////#else", "////#otherwise", "//#Z_else" },
                    ActionableElseIfTokens = new[] { "////#elseif", "////#nextcheck", "//#Z_elseif" },
                    ActionableOperations = new[] { replaceOperationId, uncommentOperationId }
                };

                IOperationProvider[] operations =
                {
                    new Conditional(tokenVariants, true, true, CppStyleEvaluatorDefinition.Evaluate, null),
                    new Replacement("////", "//", uncommentOperationId),
                    new Replacement("//", string.Empty, replaceOperationId)
                };

                return operations;
            }
        }

        private static IOperationProvider[] CStyleWithCommentsConditionalOperations
        {
            get
            {
                // this is normally handled in the config setup
                string replaceOperationId = "Replacement: (//) ()";
                string uncommentOperationId = "Uncomment (////) -> (//)";

                ConditionalTokens tokenVariants = new ConditionalTokens
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

                IOperationProvider[] operations =
                {
                    new Conditional(tokenVariants, true, true, CppStyleEvaluatorDefinition.Evaluate, null),
                    new Replacement("////", "//", uncommentOperationId),
                    new Replacement("//", string.Empty, replaceOperationId)
                };

                return operations;
            }
        }

        private static IOperationProvider[] CStyleNoCommentsConditionalOperations
        {
            get
            {
                ConditionalTokens tokenVariants = new ConditionalTokens
                {
                    IfTokens = new[] { "#if" },
                    ElseTokens = new[] { "#else" },
                    ElseIfTokens = new[] { "#elseif" },
                    EndIfTokens = new[] { "#endif" }
                };

                IOperationProvider[] operations =
                {
                    new Conditional(tokenVariants, true, true, CppStyleEvaluatorDefinition.Evaluate, null)
                };

                return operations;
            }
        }

        private static IOperationProvider[] HashSignLineCommentConditionalOperations
        {
            get
            {
                string uncommentOperationId = "Uncomment (hash line): (##) -> (#)";
                string replaceOperationId = "Replacement (hash line): (#) -> ()";

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

                IOperationProvider[] operations =
                {
                    new Conditional(tokens, true, true, CppStyleEvaluatorDefinition.Evaluate, null),
                    new Replacement("##", "#", uncommentOperationId),
                    new Replacement("#", "", replaceOperationId),
                };

                return operations;
            }
        }

        private static IOperationProvider[] BatFileRemLineCommentConditionalOperations
        {
            get
            {
                string uncommentOperationId = "Uncomment (bat rem): (rem rem) -> (rem)";
                string replaceOperationId = "Replacement (bat rem): (rem) -> ()";

                ConditionalTokens tokens = new ConditionalTokens
                {
                    IfTokens = new[] { "rem #if" },
                    ElseTokens = new[] { "rem #else" },
                    ElseIfTokens = new[] { "rem #elseif" },
                    EndIfTokens = new[] { "rem #endif", "rem rem #endif" },
                    ActionableIfTokens = new[] { "rem rem #if" },
                    ActionableElseIfTokens = new[] { "rem rem #elseif" },
                    ActionableElseTokens = new[] { "rem rem #else" },
                    ActionableOperations = new[] { replaceOperationId, uncommentOperationId }
                };

                IOperationProvider[] operations =
                {
                    new Conditional(tokens, true, true, CppStyleEvaluatorDefinition.Evaluate, null),
                    new Replacement("rem rem", "rem", uncommentOperationId),
                    new Replacement("rem", "", replaceOperationId)
                };

                return operations;
            }
        }
    }
}
