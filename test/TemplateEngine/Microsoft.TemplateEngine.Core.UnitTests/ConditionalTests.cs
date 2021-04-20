// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Expressions.Cpp;
using Microsoft.TemplateEngine.Core.Expressions.VisualBasic;
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

        protected IProcessor SetupVBStyleNoCommentsProcessor(VariableCollection vc)
        {
            IOperationProvider[] operations = VBStyleNoCommentsConditionalOperations;
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

        protected IProcessor SetupHamlLineCommentsProcessor(VariableCollection vc)
        {
            IOperationProvider[] operations = HamlLineCommentConditionalOperations;
            return SetupTestProcessor(operations, vc);
        }

        protected IProcessor SetupJsxBlockCommentsProcessor(VariableCollection vc)
        {
            IOperationProvider[] operations = JsxBlockCommentConditionalsOperations;
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
                    EndIfTokens = new[] { "#endif", "<!--#endif" }.TokenConfigs(),
                    ActionableIfTokens = new[] { "<!--#if" }.TokenConfigs(),
                    ActionableElseTokens = new[] { "#else", "<!--#else" }.TokenConfigs(),
                    ActionableElseIfTokens = new[] { "#elseif", "<!--#elseif", "#elif", "<!--#elif" }.TokenConfigs(),
                    ActionableOperations = new[] { commentFixingOperationId, commentFixingResetId }
                };

                IOperationProvider[] operations =
                {
                    new Conditional(tokenVariants, true, true, CppStyleEvaluatorDefinition.Evaluate, null, true),
                    new BalancedNesting("<!--".TokenConfig(), "-->".TokenConfig(), "-- >".TokenConfig(), commentFixingOperationId, commentFixingResetId, false)
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
                    EndIfTokens = new[] { "#endif", "@*#endif" }.TokenConfigs(),
                    ActionableIfTokens = new[] { "@*#if" }.TokenConfigs(),
                    ActionableElseTokens = new[] { "#else", "@*#else" }.TokenConfigs(),
                    ActionableElseIfTokens = new[] { "#elseif", "@*#elseif", "#elif", "@*#elif" }.TokenConfigs(),
                    ActionableOperations = new[] { commentFixingOperationId, commentFixingResetId }
                };

                IOperationProvider[] operations =
                {
                    new Conditional(tokenVariants, true, true, CppStyleEvaluatorDefinition.Evaluate, null, true),
                    new BalancedNesting("@*".TokenConfig(), "*@".TokenConfig(), "* @".TokenConfig(), commentFixingOperationId, commentFixingResetId, false)
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
                    IfTokens = new[] { "//#if", "//#check" }.TokenConfigs(),
                    ElseTokens = new[] { "//#else", "//#otherwise" }.TokenConfigs(),
                    ElseIfTokens = new[] { "//#elseif", "//#nextcheck" }.TokenConfigs(),
                    EndIfTokens = new[] { "//#endif", "//#stop", "//#done", "//#nomore" }.TokenConfigs(),
                    ActionableIfTokens = new[] { "////#if", "////#check", "//#Z_if" }.TokenConfigs(),
                    ActionableElseTokens = new[] { "////#else", "////#otherwise", "//#Z_else" }.TokenConfigs(),
                    ActionableElseIfTokens = new[] { "////#elseif", "////#nextcheck", "//#Z_elseif" }.TokenConfigs(),
                    ActionableOperations = new[] { replaceOperationId, uncommentOperationId }
                };

                IOperationProvider[] operations =
                {
                    new Conditional(tokenVariants, true, true, CppStyleEvaluatorDefinition.Evaluate, null, true),
                    new Replacement("////".TokenConfig(), "//", uncommentOperationId, false),
                    new Replacement("//".TokenConfig(), string.Empty, replaceOperationId, false)
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
                    IfTokens = new[] { "//#if" }.TokenConfigs(),
                    ElseTokens = new[] { "//#else" }.TokenConfigs(),
                    ElseIfTokens = new[] { "//#elseif", "//#elif" }.TokenConfigs(),
                    EndIfTokens = new[] { "//#endif", "////#endif" }.TokenConfigs(),
                    ActionableIfTokens = new[] { "////#if" }.TokenConfigs(),
                    ActionableElseIfTokens = new[] { "////#elseif", "////#elif" }.TokenConfigs(),
                    ActionableElseTokens = new[] { "////#else" }.TokenConfigs(),
                    ActionableOperations = new[] { replaceOperationId, uncommentOperationId }
                };

                IOperationProvider[] operations =
                {
                    new Conditional(tokenVariants, true, true, CppStyleEvaluatorDefinition.Evaluate, null, true),
                    new Replacement("////".TokenConfig(), "//", uncommentOperationId, false),
                    new Replacement("//".TokenConfig(), string.Empty, replaceOperationId, false)
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
                    IfTokens = new[] { "#if" }.TokenConfigs(),
                    ElseTokens = new[] { "#else" }.TokenConfigs(),
                    ElseIfTokens = new[] { "#elseif", "#elif" }.TokenConfigs(),
                    EndIfTokens = new[] { "#endif" }.TokenConfigs()
                };

                IOperationProvider[] operations =
                {
                    new Conditional(tokenVariants, true, true, CppStyleEvaluatorDefinition.Evaluate, null, true)
                };

                return operations;
            }
        }

        private static IOperationProvider[] VBStyleNoCommentsConditionalOperations
        {
            get
            {
                ConditionalTokens tokenVariants = new ConditionalTokens
                {
                    IfTokens = new[] { "#If" }.TokenConfigs(),
                    ElseTokens = new[] { "#Else" }.TokenConfigs(),
                    ElseIfTokens = new[] { "#ElseIf" }.TokenConfigs(),
                    EndIfTokens = new[] { "#End If" }.TokenConfigs()
                };

                IOperationProvider[] operations =
                {
                    new Conditional(tokenVariants, true, true, VisualBasicStyleEvaluatorDefintion.Evaluate, null, true)
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
                    IfTokens = new[] { "#if" }.TokenConfigs(),
                    ElseTokens = new[] { "#else" }.TokenConfigs(),
                    ElseIfTokens = new[] { "#elseif", "#elif" }.TokenConfigs(),
                    EndIfTokens = new[] { "#endif", "##endif" }.TokenConfigs(),
                    ActionableIfTokens = new[] { "##if" }.TokenConfigs(),
                    ActionableElseIfTokens = new[] { "##elseif", "##elif" }.TokenConfigs(),
                    ActionableElseTokens = new[] { "##else" }.TokenConfigs(),
                    ActionableOperations = new[] { replaceOperationId, uncommentOperationId }
                };

                IOperationProvider[] operations =
                {
                    new Conditional(tokens, true, true, CppStyleEvaluatorDefinition.Evaluate, null, true),
                    new Replacement("##".TokenConfig(), "#", uncommentOperationId, false),
                    new Replacement("#".TokenConfig(), "", replaceOperationId, false),
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
                    IfTokens = new[] { "rem #if" }.TokenConfigs(),
                    ElseTokens = new[] { "rem #else" }.TokenConfigs(),
                    ElseIfTokens = new[] { "rem #elseif", "rem #elif" }.TokenConfigs(),
                    EndIfTokens = new[] { "rem #endif", "rem rem #endif" }.TokenConfigs(),
                    ActionableIfTokens = new[] { "rem rem #if" }.TokenConfigs(),
                    ActionableElseIfTokens = new[] { "rem rem #elseif", "rem rem #elif" }.TokenConfigs(),
                    ActionableElseTokens = new[] { "rem rem #else" }.TokenConfigs(),
                    ActionableOperations = new[] { replaceOperationId, uncommentOperationId }
                };

                IOperationProvider[] operations =
                {
                    new Conditional(tokens, true, true, CppStyleEvaluatorDefinition.Evaluate, null, true),
                    new Replacement("rem rem".TokenConfig(), "rem", uncommentOperationId, false),
                    new Replacement("rem".TokenConfig(), "", replaceOperationId, false)
                };

                return operations;
            }
        }

        private static IOperationProvider[] HamlLineCommentConditionalOperations
        {
            get
            {
                string reduceCommentOperationId = "Reduce comment (line): (-#-#) -> (-#)";
                string uncommentOperationId = "Uncomment (line): (-#) -> ()";

                ConditionalTokens tokens = new ConditionalTokens
                {
                    IfTokens = new[] { "-#if" }.TokenConfigs(),
                    ElseTokens = new[] { "-#else" }.TokenConfigs(),
                    ElseIfTokens = new[] { "-#elseif", "-#elif" }.TokenConfigs(),
                    EndIfTokens = new[] { "-#endif", "-#-#endif" }.TokenConfigs(),
                    ActionableIfTokens = new[] { "-#-#if" }.TokenConfigs(),
                    ActionableElseIfTokens = new[] { "-#-#elseif", "-#-#elif" }.TokenConfigs(),
                    ActionableElseTokens = new[] { "-#-#else" }.TokenConfigs(),
                    ActionableOperations = new[] { uncommentOperationId, reduceCommentOperationId }
                };

                IOperationProvider[] operations =
                {
                    new Conditional(tokens, true, true, CppStyleEvaluatorDefinition.Evaluate, null, true),
                    new Replacement("-#-#".TokenConfig(), "-#", reduceCommentOperationId, false),
                    new Replacement("-#".TokenConfig(), "", uncommentOperationId, false),
                };

                return operations;
            }
        }

        /// <summary>
        /// Returns an IOperationProvider setup for razor style comment processing.
        /// </summary>
        private static IOperationProvider[] JsxBlockCommentConditionalsOperations
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
                    EndIfTokens = new[] { "#endif", "{/*#endif" }.TokenConfigs(),
                    ActionableIfTokens = new[] { "{/*#if" }.TokenConfigs(),
                    ActionableElseTokens = new[] { "#else", "{/*#else" }.TokenConfigs(),
                    ActionableElseIfTokens = new[] { "#elseif", "{/*#elseif", "#elif", "{/*#elif" }.TokenConfigs(),
                    ActionableOperations = new[] { commentFixingOperationId, commentFixingResetId }
                };

                IOperationProvider[] operations =
                {
                    new Conditional(tokenVariants, true, true, CppStyleEvaluatorDefinition.Evaluate, null, true),
                    new BalancedNesting("{/*".TokenConfig(), "*/}".TokenConfig(), "*/ }".TokenConfig(), commentFixingOperationId, commentFixingResetId, false)
                };

                return operations;
            }
        }
    }
}
