using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Abstractions.Engine;
using Microsoft.TemplateEngine.Core.Expressions.Cpp;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class ConditionalTests : TestBase
    {
        #region initialization & support

        private IProcessor SetupRazorStyleProcessor(VariableCollection vc)
        {
            IOperationProvider[] operations = RazorStyleCommentConditionalsOperations;
            return SetupTestProcessor(operations, vc);
        }

        private IProcessor SetupMadeUpStyleProcessor(VariableCollection vc)
        {
            IOperationProvider[] operations = MadeUpConditionalsOperations;
            return SetupTestProcessor(operations, vc);
        }

        private IProcessor SetupXmlStyleProcessor(VariableCollection vc)
        {
            IOperationProvider[] operations = XmlStyleCommentConditionalsOperations;
            return SetupTestProcessor(operations, vc);
        }

        private IProcessor SetupCStyleWithCommentsProcessor(VariableCollection vc)
        {
            IOperationProvider[] operations = CStyleWithCommentsConditionalOperations;
            return SetupTestProcessor(operations, vc);
        }

        private IProcessor SetupCStyleNoCommentsProcessor(VariableCollection vc)
        {
            IOperationProvider[] operations = CStyleNoCommentsConditionalOperations;
            return SetupTestProcessor(operations, vc);
        }

        ///
        /// Sets up a processor with the input params.
        ///
        private IProcessor SetupTestProcessor(IOperationProvider[] operations, VariableCollection vc)
        {
            EngineConfig cfg = new EngineConfig(vc);
            return Processor.Create(cfg, operations);
        }

        /// <summary>
        /// Second attempt at xml comment processing.
        ///
        /// TODO: determine if the specials with "-- >" are needed.
        /// </summary>
        private IOperationProvider[] XmlStyleCommentConditionalsOperations
        {
            get
            {
                //string trailingCommentOperationId = "Remove -->";
                //string trailingPseudoCommentOperationId = "Remove -- >";

                ConditionalTokens tokenVariants = new ConditionalTokens();
                tokenVariants.EndIfTokens = new[] { "#endif", "<!--#endif", "#endif-->", "<!--#endif-->", "<!--#endif-- >", "#endif-- >" };
                tokenVariants.ActionableIfTokens = new[] { "<!--#if" };
                tokenVariants.ActionableElseTokens = new[] { "#else", "<!--#else", "#else-->", "<!--#else-->", "<!--#else-- >", "#else-- >" };
                tokenVariants.ActionableElseIfTokens = new[] { "#elseif", "<!--#elseif", "#elseif-->", "<!--#elseif-->", "<!--#elseif-- >", "#elseif-- >" };
                tokenVariants.ActionableOperations = ConditionalTokens.NoTokens;    // superfluous, but might get some value(s)

                string conditionalOperationId = "XmlConditional";

                IOperationProvider[] operations =
                {
                    new Conditional(tokenVariants, true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator, conditionalOperationId),
                    //new Replacment("-->", string.Empty, trailingCommentOperationId),
                    //new Replacment("-- >", string.Empty, trailingPseudoCommentOperationId),
                };

                /*
                This input:

                <!-- #if (OUTER_IF_CLAUSE) -->
                    <!-- content: outer-if -- > -->

                 Needs to produce this output:
                    <!-- content: outer-if -- > -->
                */

                return operations;
            }
        }

        /// <summary>
        /// Returns an IOperationProvider setup for razor style comment processing.
        /// </summary>
        private IOperationProvider[] RazorStyleCommentConditionalsOperations
        {
            get
            {
                //string trailingCommentOperationId = "Remove: *@";
                //string trailingPseudoCommentToperationId = "Remove: * @";

                ConditionalTokens tokenVariants = new ConditionalTokens();
                tokenVariants.EndIfTokens = new[] { "#endif", "@*#endif", "#endif*@", "@*#endif*@", "@*#endif* @", "#endif* @" };
                tokenVariants.ActionableIfTokens = new[] { "@*#if" }; ;
                tokenVariants.ActionableElseTokens = new[] { "#else", "@*#else", "#else*@", "@*#else*@", "@*#else* @", "#else* @" };
                tokenVariants.ActionableElseIfTokens = new[] { "#elseif", "@*#elseif", "#elseif*@", "@*#elseif*@", "@*#elseif* @", "#elseif* @" };
                tokenVariants.ActionableOperations = ConditionalTokens.NoTokens;    // superfluous, but might get some value(s)

                //tokenVariants.ActionableOperations = new[] { trailingCommentOperationId, trailingPseudoCommentToperationId };

                string conditionalOperationId = "RazorConditional";

                //TODO: figure out the replacements that will need toggling
                IOperationProvider[] operations =
                {
                    new Conditional(tokenVariants, true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator, conditionalOperationId),
                    //new Replacment("*@", string.Empty, trailingCommentOperationId),
                    //new Replacment("* @", string.Empty, trailingPseudoCommentToperationId),
                };

                return operations;
            }
        }

        private IOperationProvider[] MadeUpConditionalsOperations
        {
            get
            {
                string replaceOperationId = "Replacement (//) ()";    // this is normally handled in the config setup

                ConditionalTokens tokenVariants = new ConditionalTokens();
                tokenVariants.IfTokens = new[] { "//#if", "//#check" };
                tokenVariants.ElseTokens = new[] { "//#else", "//#otherwise" };
                tokenVariants.ElseIfTokens = new[] { "//#elseif", "//#nextcheck" };
                tokenVariants.EndIfTokens = new[] { "//#endif", "//#stop", "//#done", "//#nomore" };
                tokenVariants.ActionableIfTokens = new[] { "////#if", "////#check", "//#Z_if" };
                tokenVariants.ActionableElseTokens = new[] { "////#else", "////#otherwise", "//#Z_else" };
                tokenVariants.ActionableElseIfTokens = new[] { "////#elseif", "////#nextcheck", "//#Z_elseif" };
                tokenVariants.ActionableOperations = new[] { replaceOperationId };

                string conditionalOperationId = "MadeUpConditional";

                IOperationProvider[] operations =
                {
                    new Conditional(tokenVariants, true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator, conditionalOperationId),
                    new Replacment("//", string.Empty, replaceOperationId)
                };

                return operations;
            }
        }

        private IOperationProvider[] CStyleWithCommentsConditionalOperations
        {
            get
            {
                string replaceOperationId = "Replacement: (//) ()";    // this is normally handled in the config setup

                ConditionalTokens tokenVariants = new ConditionalTokens();
                tokenVariants.IfTokens = new[] { "//#if" };
                tokenVariants.ElseTokens = new[] { "//#else" };
                tokenVariants.ElseIfTokens = new[] { "//#elseif" };
                tokenVariants.EndIfTokens = new[] { "//#endif" };
                tokenVariants.ActionableIfTokens = new[] { "////#if" };
                tokenVariants.ActionableElseIfTokens = new[] { "////#elseif" };
                tokenVariants.ActionableElseTokens = new[] { "////#else" };
                tokenVariants.ActionableOperations = new[] { replaceOperationId };

                string conditionalOperationId = "CStyleConditional";

                IOperationProvider[] operations =
                {
                    new Conditional(tokenVariants, true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator, conditionalOperationId),
                    new Replacment("//", string.Empty, replaceOperationId)
                };

                return operations;
            }
        }

        private IOperationProvider[] CStyleNoCommentsConditionalOperations
        {
            get
            {
                ConditionalTokens tokenVariants = new ConditionalTokens();
                tokenVariants.IfTokens = new[] { "#if" };
                tokenVariants.ElseTokens = new[] { "#else" };
                tokenVariants.ElseIfTokens = new[] { "#elseif" };
                tokenVariants.EndIfTokens = new[] { "#endif" };

                string conditionalOperationId = "CStyleNoCommentsConditional";

                IOperationProvider[] operations =
                {
                    new Conditional(tokenVariants, true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator, conditionalOperationId)
                };

                return operations;
            }
        }

        #endregion initialization & support

        #region XmlBlockComments

        /// <summary>
        /// Temporary test, experimenting with block comments
        /// </summary>
        [Fact]
        public void VerifyMultipleConsecutiveTrailingCommentsWithinContent()
        {
            string originalValue = @"Start
<!--#if (IF_VALUE) -->
    <!-- content: outer-if -- > -->
#endif-->
End";
            string expectedValue = @"Start
    <!-- content: outer-if -- > -->
End";

            VariableCollection vc = new VariableCollection
            {
                ["IF_VALUE"] = true,
            };
            IProcessor processor = SetupXmlStyleProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact]
        public void VerifyMultipleEndCommentsOnEndif()
        {
            string originalValue = @"Start
<!--#if (OUTER_IF)
    Content: Outer if
    <!--#if (INNER_IF)
        Content: Inner if
    #endif
#endif-- > -->
End";
            string expectedValue = @"Start
    Content: Outer if
        Content: Inner if
End";
            VariableCollection vc = new VariableCollection
            {
                ["OUTER_IF"] = true,
                ["INNER_IF"] = true
            };
            IProcessor processor = SetupXmlStyleProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);

            // test for 3 levels deep
            string threePartOriginalValue = @"Start
<!--#if (OUTER_IF)
    Content: Outer if
    <!--#if (INNER_IF)
        Content: Inner if
        <!--#if (THIRD_IF)
            Content: Third if
        #endif
    #endif
#endif-- > -- > -->
End";
            string threePartExpectedValue = @"Start
    Content: Outer if
        Content: Inner if
            Content: Third if
End";

            vc = new VariableCollection
            {
                ["OUTER_IF"] = true,
                ["INNER_IF"] = true,
                ["THIRD_IF"] = true
            };
            processor = SetupXmlStyleProcessor(vc);
            RunAndVerify(threePartOriginalValue, threePartExpectedValue, processor, 9999);
        }

        [Fact]
        public void VerifyMultipleEndCommentsOnElseif()
        {
            string originalValue = @"Start
<!--#if (OUTER_IF)
    Content: Outer if
    <!--#if (INNER_IF)
        Content: Inner if
    #elseif (INNER_ELSEIF) -- > -->
        Content: Inner elseif (default)
    <!--#else
        Content: Inner else
    #endif
#endif-->
End";
            string ifIfExpectedValue = @"Start
    Content: Outer if
        Content: Inner if
End";
            VariableCollection vc = new VariableCollection
            {
                ["OUTER_IF"] = true,
                ["INNER_IF"] = true,
                ["INNER_ELSEIF"] = false
            };
            IProcessor processor = SetupXmlStyleProcessor(vc);
            RunAndVerify(originalValue, ifIfExpectedValue, processor, 9999);

            string ifElseifExpectedValue = @"Start
    Content: Outer if
        Content: Inner elseif (default)
End";
            vc = new VariableCollection
            {
                ["OUTER_IF"] = true,
                ["INNER_IF"] = false,
                ["INNER_ELSEIF"] = true
            };
            processor = SetupXmlStyleProcessor(vc);
            RunAndVerify(originalValue, ifElseifExpectedValue, processor, 9999);

            string ifElseExpectedValue = @"Start
    Content: Outer if
        Content: Inner else
End";
            vc = new VariableCollection
            {
                ["OUTER_IF"] = true,
                ["INNER_IF"] = false,
                ["INNER_ELSEIF"] = false
            };
            processor = SetupXmlStyleProcessor(vc);
            RunAndVerify(originalValue, ifElseExpectedValue, processor, 9999);
        }

        [Fact]
        public void VerifyThreeLevelNestedBlockComments()
        {
            string originalValue = @"Start
<!--#if (IF_LEVEL_1)
    Content: If Level 1
    <!--#if (IF_IF_LEVEL_2)
        Content: If IF Level 2
        <!--#if (IF_IF_IF_LEVEL_3)
            Content: If IF If Level 3
        #elseif (IF_IF_ELSEIF_LEVEL_3)
            Content: If If Elseif Level 3
        #elseif (IF_IF_ELSEIF_TWO_LEVEL_3)
            Content: If If Elseif Two Level 3
        #else
            Content: If If Else Level 3
        #endif-->
    #elseif (IF_ELSEIF_LEVEL_2)
        Content: If Elseif Level 2
        <!--#if (IF_ELSEIF_IF_LEVEL_3)
            Content: If Elseif If Level 3
        #elseif (IF_ELSEIF_ELSEIF_LEVEL_3)
            Content: If Elseif Elseif Level 3
        #else
            Content: If Elseif Else Level 3
        #endif-->
    #elseif (IF_ELSEIF_TWO_LEVEL_2)
        Content: If Elseif Two Level 2
        <!--#if (IF_ELSEIF_TWO_IF_LEVEL_3)
            Content: If Elseif Two If Level 3
        #elseif (IF_ELSEIF_TWO_ELSEIF_LEVEL_3)
            Content: If Elseif Two Elseif Level 3
        #else
            Content: If Elseif Two Else Level 3
        #endif-->
    #else
        Content: If Else Level 2
        <!--#if (IF_ELSE_IF_LEVEL_3)
            Content: If Else If Level 3
        #elseif (IF_ELSE_ELSEIF_LEVEL_3)
            Content: If Else Elseif Level 3
        #else
            Content: If Else Else Level 3
        #endif-->
    #endif-->
#elseif (ELSEIF_LEVEL_1)
    Content: Elseif Level 1
#else
    Content: Else Level 1
#endif-->
End";
            // if-if-if
            string expectedValue = @"Start
    Content: If Level 1
        Content: If IF Level 2
            Content: If IF If Level 3
End";
            VariableCollection vc = new VariableCollection
            {
                ["IF_LEVEL_1"] = true,
                ["IF_IF_LEVEL_2"] = true,
                ["IF_IF_IF_LEVEL_3"] = true,
            };
            IProcessor processor = SetupXmlStyleProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);

            // if-if-elseif
            expectedValue = @"Start
    Content: If Level 1
        Content: If IF Level 2
            Content: If If Elseif Level 3
End";
            vc = new VariableCollection
            {
                ["IF_LEVEL_1"] = true,
                ["IF_IF_LEVEL_2"] = true,
                ["IF_IF_IF_LEVEL_3"] = false,
                ["IF_IF_ELSEIF_LEVEL_3"] = true
            };
            processor = SetupXmlStyleProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);

            // if-if-elseif2
            expectedValue = @"Start
    Content: If Level 1
        Content: If IF Level 2
            Content: If If Elseif Two Level 3
End";
            vc = new VariableCollection
            {
                ["IF_LEVEL_1"] = true,
                ["IF_IF_LEVEL_2"] = true,
                ["IF_IF_IF_LEVEL_3"] = false,
                ["IF_IF_ELSEIF_LEVEL_3"] = false,
                ["IF_IF_ELSEIF_TWO_LEVEL_3"] = true
            };
            processor = SetupXmlStyleProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);

            // if-if-else
            expectedValue = @"Start
    Content: If Level 1
        Content: If IF Level 2
            Content: If If Else Level 3
End";
            vc = new VariableCollection
            {
                ["IF_LEVEL_1"] = true,
                ["IF_IF_LEVEL_2"] = true,
                ["IF_IF_IF_LEVEL_3"] = false,
                ["IF_IF_ELSEIF_LEVEL_3"] = false,
                ["IF_IF_ELSEIF_TWO_LEVEL_3"] = false
            };
            processor = SetupXmlStyleProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);

            // if-elseif-if
            expectedValue = @"Start
    Content: If Level 1
        Content: If Elseif Level 2
            Content: If Elseif If Level 3
End";
            vc = new VariableCollection
            {
                ["IF_LEVEL_1"] = true,
                ["IF_IF_LEVEL_2"] = false,
                ["IF_ELSEIF_LEVEL_2"] = true,
                ["IF_ELSEIF_IF_LEVEL_3"] = true,
            };
            processor = SetupXmlStyleProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);

            // if-elseif-elseif
            expectedValue = @"Start
    Content: If Level 1
        Content: If Elseif Level 2
            Content: If Elseif Elseif Level 3
End";
            vc = new VariableCollection
            {
                ["IF_LEVEL_1"] = true,
                ["IF_IF_LEVEL_2"] = false,
                ["IF_ELSEIF_LEVEL_2"] = true,
                ["IF_ELSEIF_IF_LEVEL_3"] = false,
                ["IF_ELSEIF_ELSEIF_LEVEL_3"] = true,
            };
            processor = SetupXmlStyleProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);

            // if-elseif-else
            expectedValue = @"Start
    Content: If Level 1
        Content: If Elseif Level 2
            Content: If Elseif Else Level 3
End";
            vc = new VariableCollection
            {
                ["IF_LEVEL_1"] = true,
                ["IF_IF_LEVEL_2"] = false,
                ["IF_ELSEIF_LEVEL_2"] = true,
                ["IF_ELSEIF_IF_LEVEL_3"] = false,
                ["IF_ELSEIF_ELSEIF_LEVEL_3"] = false,
            };
            processor = SetupXmlStyleProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);

            // if-elseif two-if
            expectedValue = @"Start
    Content: If Level 1
        Content: If Elseif Two Level 2
            Content: If Elseif Two If Level 3
End";
            vc = new VariableCollection
            {
                ["IF_LEVEL_1"] = true,
                ["IF_IF_LEVEL_2"] = false,
                ["IF_ELSEIF_LEVEL_2"] = false,
                ["IF_ELSEIF_TWO_LEVEL_2"] = true,
                ["IF_ELSEIF_TWO_IF_LEVEL_3"] = true,
            };
            processor = SetupXmlStyleProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);

            // if-elseif two-elseif
            expectedValue = @"Start
    Content: If Level 1
        Content: If Elseif Two Level 2
            Content: If Elseif Two Elseif Level 3
End";
            vc = new VariableCollection
            {
                ["IF_LEVEL_1"] = true,
                ["IF_IF_LEVEL_2"] = false,
                ["IF_ELSEIF_LEVEL_2"] = false,
                ["IF_ELSEIF_TWO_LEVEL_2"] = true,
                ["IF_ELSEIF_TWO_IF_LEVEL_3"] = false,
                ["IF_ELSEIF_TWO_ELSEIF_LEVEL_3"] = true
            };
            processor = SetupXmlStyleProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);

            // if-elseif two-else
            expectedValue = @"Start
    Content: If Level 1
        Content: If Elseif Two Level 2
            Content: If Elseif Two Else Level 3
End";
            vc = new VariableCollection
            {
                ["IF_LEVEL_1"] = true,
                ["IF_IF_LEVEL_2"] = false,
                ["IF_ELSEIF_LEVEL_2"] = false,
                ["IF_ELSEIF_TWO_LEVEL_2"] = true,
                ["IF_ELSEIF_TWO_IF_LEVEL_3"] = false,
                ["IF_ELSEIF_TWO_ELSEIF_LEVEL_3"] = false
            };
            processor = SetupXmlStyleProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);

            // if-else-if
            expectedValue = @"Start
    Content: If Level 1
        Content: If Else Level 2
            Content: If Else If Level 3
End";
            vc = new VariableCollection
            {
                ["IF_LEVEL_1"] = true,
                ["IF_IF_LEVEL_2"] = false,
                ["IF_ELSEIF_LEVEL_2"] = false,
                ["IF_ELSEIF_TWO_LEVEL_2"] = false,
                ["IF_ELSE_IF_LEVEL_3"] = true,
            };
            processor = SetupXmlStyleProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);

            // if-else-elseif
            expectedValue = @"Start
    Content: If Level 1
        Content: If Else Level 2
            Content: If Else Elseif Level 3
End";
            vc = new VariableCollection
            {
                ["IF_LEVEL_1"] = true,
                ["IF_IF_LEVEL_2"] = false,
                ["IF_ELSEIF_LEVEL_2"] = false,
                ["IF_ELSEIF_TWO_LEVEL_2"] = false,
                ["IF_ELSE_IF_LEVEL_3"] = false,
                ["IF_ELSE_ELSEIF_LEVEL_3"] = true
            };
            processor = SetupXmlStyleProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);

            // if-else-else
            expectedValue = @"Start
    Content: If Level 1
        Content: If Else Level 2
            Content: If Else Else Level 3
End";
            vc = new VariableCollection
            {
                ["IF_LEVEL_1"] = true,
                ["IF_IF_LEVEL_2"] = false,
                ["IF_ELSEIF_LEVEL_2"] = false,
                ["IF_ELSEIF_TWO_LEVEL_2"] = false,
                ["IF_ELSE_IF_LEVEL_3"] = false,
                ["IF_ELSE_ELSEIF_LEVEL_3"] = false
            };
            processor = SetupXmlStyleProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        /// <summary>
        /// Temporary test, experimenting with block comments.
        /// </summary>
        [Fact]
        public void VerifyMultipleNestedBlockComments()
        {
            // the actual tests for OUTER_IF_CLAUSE = true (inner else also happens because the other inners are false)
            string expectedValue = @"Start
    content: outer-if
        content: inner-else
Trailing stuff
<!- trailing comment -->";

            VariableCollection vc = new VariableCollection
            {
                ["OUTER_IF_CLAUSE"] = true,
                ["INNER_IF_CLAUSE"] = false,
                ["INNER_ELSEIF_CLAUSE"] = false,
                ["OUTER_ELSEIF_CLAUSE"] = false,
            };
            IProcessor processor = SetupXmlStyleProcessor(vc);

            // comment spans from inner if to outer endif
            // comments are unbalanced
            // invalid ???
            string inputValue = @"Start
<!--#if (OUTER_IF_CLAUSE) -->
    content: outer-if
    <!--#if (INNER_IF_CLAUSE)
        content: inner-if
    #elseif (INNER_ELSEIF_CLAUSE)
        content: inner-elseif
    #else
        content: inner-else
    #endif-- >
#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
#else
    content: outer-else
#endif-->
Trailing stuff
<!- trailing comment -->";
            RunAndVerify(inputValue, expectedValue, processor, 9999);

            // comments are balanced
            string inputValue2 = @"Start
<!--#if (OUTER_IF_CLAUSE) -->
    content: outer-if
    <!--#if (INNER_IF_CLAUSE)
        content: inner-if
    #elseif (INNER_ELSEIF_CLAUSE)
        content: inner-elseif
    #else
        content: inner-else
    #endif-- >
<!--#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
#else
    content: outer-else
#endif-->
Trailing stuff
<!- trailing comment -->";

            RunAndVerify(inputValue2, expectedValue, processor, 9999);

            // inner elseif is default, comments are balanced and nesting-balanced.
            string inputValue3 = @"Start
<!--#if (OUTER_IF_CLAUSE)
    content: outer-if
    <!--#if (INNER_IF_CLAUSE)
        content: inner-if
    #elseif (INNER_ELSEIF_CLAUSE) -->
        content: inner-elseif
    <!--#else
        content: inner-else
    #endif-->
<!--#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
#else
    content: outer-else
#endif-->
Trailing stuff
<!- trailing comment -->";
            RunAndVerify(inputValue3, expectedValue, processor, 9999);
        }

        [Fact]
        public void VerifyXmlBlockCommentsNestedInIf_ProperComments()
        {
            string originalValue = @"Start
<!--#if (OUTER_IF_VALUE)
    content: outer if
    <!--#if (INNER_IF_VALUE)
        content: inner if
    #elseif (INNER_ELSEIF_VALUE)
        content: inner elseif
    #endif-- >
#elseif (OUTER_ELSEIF_VALUE)
    content: outer elseif
#else
    content: outer else
#endif-->
<!-- Trailing Comment -->
End";

            string expectedValue = @"Start
    content: outer if
        content: inner if
<!-- Trailing Comment -->
End";
            VariableCollection vc = new VariableCollection
            {
                ["OUTER_IF_VALUE"] = true,
                ["INNER_IF_VALUE"] = true,    
                ["INNER_ELSEIF_VALUE"] = true,    // irrelevant
                ["OUTER_ELSEIF_VALUE"] = true,    // irrelevant
            };
            IProcessor processor = SetupXmlStyleProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        // Below tests may not have properly formatted comments, but still work

        [Fact]
        public void XmlBlockCommentBasicTest()
        {
            IList<string> testCases = new List<string>();

            string basicValue = @"Start
<!--#if (CLAUSE)
    content: if
#elseif (CLAUSE_2)
    content: elseif
#else
    content: else
#endif-->
Trailing stuff";
            testCases.Add(basicValue);

            string basicWithDefault = @"Start
<!--#if (CLAUSE) -->
    content: if
<!--#elseif (CLAUSE_2)
    content: elseif
#else
    content: else
#endif-->
Trailing stuff";
            testCases.Add(basicWithDefault);

            string expectedValueIfEmits = @"Start
    content: if
Trailing stuff";

            VariableCollection vc = new VariableCollection
            {
                ["CLAUSE"] = true,
                ["CLAUSE_2"] = true,    // irrelevant
            };
            IProcessor processor = SetupXmlStyleProcessor(vc);

            foreach (string test in testCases)
            {
                RunAndVerify(test, expectedValueIfEmits, processor, 9999);
            }

            // change the clause values so the elseif emits
            string expectedValueElseifEmits = @"Start
    content: elseif
Trailing stuff";

            vc = new VariableCollection
            {
                ["CLAUSE"] = false,
                ["CLAUSE_2"] = true,
            };
            processor = SetupXmlStyleProcessor(vc);
            foreach (string test in testCases)
            {
                RunAndVerify(test, expectedValueElseifEmits, processor, 9999);
            }

            // change the clause values so the else emits
            string expectedValueElseEmits = @"Start
    content: else
Trailing stuff";
            vc = new VariableCollection
            {
                ["CLAUSE"] = false,
                ["CLAUSE_2"] = false,
            };
            processor = SetupXmlStyleProcessor(vc);
            foreach (string test in testCases)
            {
                RunAndVerify(test, expectedValueElseEmits, processor, 9999);
            }
        }

        /// <summary>
        /// This test fails
        /// 
        /// Test cases for conditionals in xml block comments.
        /// Comment stripping is needed for some of these.
        /// </summary>
        [Fact]
        public void XmlBlockCommentIfElseifElseTestWithCommentStripping()
        {
            IList<string> testCases = new List<string>();

            // this is wrong, the one below is how it should be
            string BLAH_originalValue = @"Start
<!--#if (IF_CLAUSE)
    content: if stuff, line 1
    content: if stuff line 2-->
<!--#elseif (ELSEIF_CLAUSE) -->
    content: default stuff in the elseif
    content: default line 2 (elseif)
<!--#else
    content: else stuff, line 1
    content: default stuff in the else
    content: trailing else stuff, not default-->
<!--#endif-->
Trailing stuff
<!-- trailing comment -->";

            string originalValue = @"Start
<!--#if (IF_CLAUSE)
    content: if stuff, line 1
    content: if stuff line 2
#elseif (ELSEIF_CLAUSE) -->
    content: default stuff in the elseif
    content: default line 2 (elseif)
<!--#else
    content: else stuff, line 1
    content: default stuff in the else
    content: trailing else stuff, not default
#endif-->
Trailing stuff
<!-- trailing comment -->";
            testCases.Add(originalValue);

            string fullerCommentsOnlyValue = @"Start
<!--#if (IF_CLAUSE)
    content: if stuff, line 1
    content: if stuff line 2
#elseif (ELSEIF_CLAUSE) -->
    content: default stuff in the elseif
    content: default line 2 (elseif)
<!--#else
    content: else stuff, line 1
    content: default stuff in the else
    content: trailing else stuff, not default
#endif-->
Trailing stuff
<!-- trailing comment -->";
            testCases.Add(fullerCommentsOnlyValue);

            // note that there is no default here
            string oneBigCommentValue = @"Start
<!--#if (IF_CLAUSE)
    content: if stuff, line 1
    content: if stuff line 2
#elseif (ELSEIF_CLAUSE)
    content: default stuff in the elseif
    content: default line 2 (elseif)
#else
    content: else stuff, line 1
    content: default stuff in the else
    content: trailing else stuff, not default
#endif-->
Trailing stuff
<!-- trailing comment -->";
            testCases.Add(oneBigCommentValue);

            string ifTrueExpectedValue = @"Start
    content: if stuff, line 1
    content: if stuff line 2
Trailing stuff
<!-- trailing comment -->";
            VariableCollection vc = new VariableCollection
            {
                ["IF_CLAUSE"] = true,
                ["ELSEIF_CLAUSE"] = false,
            };
            IProcessor processor = SetupXmlStyleProcessor(vc);
            foreach (string test in testCases)
            {
                RunAndVerify(test, ifTrueExpectedValue, processor, 9999);
            }

            string elseifTrueExpectedValue = @"Start
    content: default stuff in the elseif
    content: default line 2 (elseif)
Trailing stuff
<!-- trailing comment -->";
            vc = new VariableCollection
            {
                ["IF_CLAUSE"] = false,
                ["ELSEIF_CLAUSE"] = true,
            };
            processor = SetupXmlStyleProcessor(vc);
            foreach (string test in testCases)
            {
                RunAndVerify(test, elseifTrueExpectedValue, processor, 9999);
            }

            string elseHappensExpectedValue = @"Start
    content: else stuff, line 1
    content: default stuff in the else
    content: trailing else stuff, not default
Trailing stuff
<!-- trailing comment -->";
            vc = new VariableCollection
            {
                ["IF_CLAUSE"] = false,
                ["ELSEIF_CLAUSE"] = false,
            };
            processor = SetupXmlStyleProcessor(vc);
            foreach (string test in testCases)
            {
                RunAndVerify(test, elseHappensExpectedValue, processor, 9999);
            }
        }

        /// <summary>
        /// Tests basic conditional embedding for block XML comments
        /// </summary>
        [Fact]
        public void VerifyXmlBlockCommentEmbeddedInIfTest()
        {
            IList<string> testCases = new List<string>();

            string noDefaultValue = @"Start
<!--#if (OUTER_IF_CLAUSE)
    content: outer-if
    <!--#if (INNER_IF_CLAUSE)
        content: inner-if
    #elseif (INNER_ELSEIF_CLAUSE)
        content: inner-elseif
    #else
        content: inner-else
    #endif-- >
#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
#else
    content: outer-else
#endif-->
Trailing stuff
<!- trailing comment -->";
            testCases.Add(noDefaultValue);

            string outerIfDefaultValue = @"Start
<!--#if (OUTER_IF_CLAUSE) -->
    content: outer-if
    <!--#if (INNER_IF_CLAUSE)
        content: inner-if
    #elseif (INNER_ELSEIF_CLAUSE)
        content: inner-elseif
    #else
        content: inner-else
    #endif
#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
#else
    content: outer-else
#endif-->
Trailing stuff
<!- trailing comment -->";
            testCases.Add(outerIfDefaultValue);

            string innerIfDefaultValue = @"Start
<!--#if (OUTER_IF_CLAUSE)
    content: outer-if
    <!--#if (INNER_IF_CLAUSE) -->
        content: inner-if
    <!--#elseif (INNER_ELSEIF_CLAUSE)
        content: inner-elseif
    #else
        content: inner-else
    #endif
#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
#else
    content: outer-else
#endif-->
Trailing stuff
<!- trailing comment -->";
            testCases.Add(innerIfDefaultValue);

            string innerElseifDefaultValue = @"Start
<!--#if (OUTER_IF_CLAUSE) -->
    content: outer-if
    <!--#if (INNER_IF_CLAUSE)
        content: inner-if
    <!--#elseif (INNER_ELSEIF_CLAUSE) -->
        content: inner-elseif
    <!--#else
        content: inner-else
    #endif
#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
#else
    content: outer-else
#endif-->
Trailing stuff
<!- trailing comment -->";
            testCases.Add(innerElseifDefaultValue);

            string innerElseDefaultValue = @"Start
<!--#if (OUTER_IF_CLAUSE)
    content: outer-if
    <!--#if (INNER_IF_CLAUSE)
        content: inner-if
    #elseif (INNER_ELSEIF_CLAUSE)
        content: inner-elseif
    <!--#else-->
        content: inner-else
    <!--#endif
#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
#else
    content: outer-else
#endif-->
Trailing stuff
<!- trailing comment -->";
            testCases.Add(innerElseDefaultValue);

            string outerElseifDefaultValue = @"Start
<!--#if (OUTER_IF_CLAUSE)
    content: outer-if
    <!--#if (INNER_IF_CLAUSE)
        content: inner-if
    #elseif (INNER_ELSEIF_CLAUSE)
        content: inner-elseif
    #else
        content: inner-else
    #endif
<!--#elseif (OUTER_ELSEIF_CLAUSE)-->
    content: outer-elseif
#else
    content: outer-else
#endif-->
Trailing stuff
<!- trailing comment -->";
            testCases.Add(outerElseifDefaultValue);

            string outerElseDefaultValue = @"Start
<!--#if (OUTER_IF_CLAUSE)
    content: outer-if
    <!--#if (INNER_IF_CLAUSE)
        content: inner-if
    #elseif (INNER_ELSEIF_CLAUSE)
        content: inner-elseif
    #else
        content: inner-else
    #endif
#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
<!--#else-->
    content: outer-else
#endif-->
Trailing stuff
<!- trailing comment -->";
            testCases.Add(outerElseDefaultValue);

            // the actual tests for OUTER_IF_CLAUSE = true (inner else also happens because the other inners are false)
            string outerIfTrueExpectedValue = @"Start
    content: outer-if
        content: inner-else
Trailing stuff
<!- trailing comment -->";

            VariableCollection vc = new VariableCollection
            {
                ["OUTER_IF_CLAUSE"] = true,
                ["INNER_IF_CLAUSE"] = false,
                ["INNER_ELSEIF_CLAUSE"] = false,
                ["OUTER_ELSEIF_CLAUSE"] = false,
            };
            IProcessor processor = SetupXmlStyleProcessor(vc);
            foreach (string test in testCases)
            {
                RunAndVerify(test, outerIfTrueExpectedValue, processor, 9999);
            }

            // the actual tests for INNER_IF_CLAUSE = true (the OUTER_IF_CLAUSE must be true for this to matter)
            string innerIfTrueExpectedValue = @"Start
    content: outer-if
        content: inner-if
Trailing stuff
<!- trailing comment -->";

            vc = new VariableCollection
            {
                ["OUTER_IF_CLAUSE"] = true,
                ["INNER_IF_CLAUSE"] = true,
                ["INNER_ELSEIF_CLAUSE"] = false,
                ["OUTER_ELSEIF_CLAUSE"] = false,
            };
            processor = SetupXmlStyleProcessor(vc);
            foreach (string test in testCases)
            {
                RunAndVerify(test, innerIfTrueExpectedValue, processor, 9999);
            }

            // the actual tests for INNER_ELSEIF_CLAUSE = true (the OUTER_IF_CLAUSE must be true for this to matter)
            string innerElseifTrueExpectedValue = @"Start
    content: outer-if
        content: inner-elseif
Trailing stuff
<!- trailing comment -->";

            vc = new VariableCollection
            {
                ["OUTER_IF_CLAUSE"] = true,
                ["INNER_IF_CLAUSE"] = false,
                ["INNER_ELSEIF_CLAUSE"] = true,
                ["OUTER_ELSEIF_CLAUSE"] = false,
            };
            processor = SetupXmlStyleProcessor(vc);
            foreach (string test in testCases)
            {
                RunAndVerify(test, innerElseifTrueExpectedValue, processor, 9999);
            }

            // the actual tests for OUTER_ELSEIF_CLAUSE = true (the OUTER_IF_CLAUSE must be false for this to matter)
            string outerElseifTrueExpectedValue = @"Start
    content: outer-elseif
Trailing stuff
<!- trailing comment -->";

            vc = new VariableCollection
            {
                ["OUTER_IF_CLAUSE"] = false,
                ["INNER_IF_CLAUSE"] = false,    // irrelevant
                ["INNER_ELSEIF_CLAUSE"] = false,    // irrelevant
                ["OUTER_ELSEIF_CLAUSE"] = true,
            };
            processor = SetupXmlStyleProcessor(vc);
            foreach (string test in testCases)
            {
                RunAndVerify(test, outerElseifTrueExpectedValue, processor, 9999);
            }

            // the actual tests for when the outer else happens
            string outerElseTrueExpectedValue = @"Start
    content: outer-else
Trailing stuff
<!- trailing comment -->";

            vc = new VariableCollection
            {
                ["OUTER_IF_CLAUSE"] = false,
                ["INNER_IF_CLAUSE"] = false,    // irrelevant
                ["INNER_ELSEIF_CLAUSE"] = false,    // irrelevant
                ["OUTER_ELSEIF_CLAUSE"] = false,
            };
            processor = SetupXmlStyleProcessor(vc);
            foreach (string test in testCases)
            {
                RunAndVerify(test, outerElseTrueExpectedValue, processor, 9999);
            }
        }

        /// <summary>
        /// Temporary test for isolating bugs
        /// </summary>
        [Fact]
        public void MinimalXmlElseifEmbeddingTest()
        {
            string testValue = @"Start
<!--#if (OUTER_IF_CLAUSE)
    content: outer-if
#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
    <!--#if (INNER_IF_CLAUSE)
        content: inner-if
    #elseif (INNER_ELSEIF_CLAUSE)
        content: inner-elseif
    #else
        content: inner-else
    #endif
#else
    content: outer-else
#endif-->
Trailing stuff
<!- trailing comment -->";

            string outerIfTrueExpectedValue = @"Start
    content: outer-else
Trailing stuff
<!- trailing comment -->";

            VariableCollection vc = new VariableCollection
            {
                ["OUTER_IF_CLAUSE"] = false,
                ["OUTER_ELSEIF_CLAUSE"] = false,
                ["INNER_IF_CLAUSE"] = false,
                ["INNER_ELSEIF_CLAUSE"] = false,
            };
            IProcessor processor = SetupXmlStyleProcessor(vc);
            RunAndVerify(testValue, outerIfTrueExpectedValue, processor, 9999);
        }

        /// <summary>
        /// Tests block comment embedding of conditionals in the elseif
        /// </summary>
        [Fact]
        public void VerifyXmlBlockCommentEmbeddedInElseifTest()
        {
            IList<string> testCases = new List<string>();
            string noDefaultValue = @"Start
<!--#if (OUTER_IF_CLAUSE)
    content: outer-if
#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
    <!--#if (INNER_IF_CLAUSE)
        content: inner-if
    #elseif (INNER_ELSEIF_CLAUSE)
        content: inner-elseif
    #else
        content: inner-else
    #endif
#else
    content: outer-else
#endif-->
Trailing stuff
<!- trailing comment -->";
            testCases.Add(noDefaultValue);

            string outerIfDefaultValue = @"Start
<!--#if (OUTER_IF_CLAUSE) -->
    content: outer-if
<!--#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
    <!--#if (INNER_IF_CLAUSE)
        content: inner-if
    #elseif (INNER_ELSEIF_CLAUSE)
        content: inner-elseif
    #else
        content: inner-else
    #endif
#else
    content: outer-else
#endif-->
Trailing stuff
<!- trailing comment -->";
            testCases.Add(outerIfDefaultValue);

            string innerIfDefaultValue = @"Start
<!--#if (OUTER_IF_CLAUSE)
    content: outer-if
#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
    <!--#if (INNER_IF_CLAUSE) -->
        content: inner-if
    <!--#elseif (INNER_ELSEIF_CLAUSE)
        content: inner-elseif
    #else
        content: inner-else
    #endif
#else
    content: outer-else
#endif-->
Trailing stuff
<!- trailing comment -->";
            testCases.Add(innerIfDefaultValue);

            string innerElseifDefaultValue = @"Start
<!--#if (OUTER_IF_CLAUSE)
    content: outer-if
#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
    <!--#if (INNER_IF_CLAUSE)
        content: inner-if
    <!--#elseif (INNER_ELSEIF_CLAUSE) -->
        content: inner-elseif
    <!--#else
        content: inner-else
    #endif
#else
    content: outer-else
#endif-->
Trailing stuff
<!- trailing comment -->";
            testCases.Add(innerElseifDefaultValue);

            string innerElseDefaultValue = @"Start
<!--#if (OUTER_IF_CLAUSE)
    content: outer-if
#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
    <!--#if (INNER_IF_CLAUSE)
        content: inner-if
    #elseif (INNER_ELSEIF_CLAUSE)
        content: inner-elseif
    <!--#else-->
        content: inner-else
    <!--#endif
#else
    content: outer-else
#endif-->
Trailing stuff
<!- trailing comment -->";
            testCases.Add(innerElseDefaultValue);

            string outerElseifDefaultValue = @"Start
<!--#if (OUTER_IF_CLAUSE)
    content: outer-if
<!-- #elseif (OUTER_ELSEIF_CLAUSE) -->
    content: outer-elseif
    <!--#if (INNER_IF_CLAUSE)
        content: inner-if
    #elseif (INNER_ELSEIF_CLAUSE)
        content: inner-elseif
    #else
        content: inner-else
    #endif
#else
    content: outer-else
#endif-->
Trailing stuff
<!- trailing comment -->";
            testCases.Add(outerElseifDefaultValue);

            string outerElseDefaultValue = @"Start
<!--#if (OUTER_IF_CLAUSE)
    content: outer-if
#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
    <!--#if (INNER_IF_CLAUSE)
        content: inner-if
    #elseif (INNER_ELSEIF_CLAUSE)
        content: inner-elseif
    #else
        content: inner-else
    #endif
<!--#else-->
    content: outer-else
#endif-->
Trailing stuff
<!- trailing comment -->";
            testCases.Add(outerElseDefaultValue);

            // the actual tests for OUTER_IF_CLAUSE = true
            string outerIfTrueExpectedValue = @"Start
    content: outer-if
Trailing stuff
<!- trailing comment -->";

            VariableCollection vc = new VariableCollection
            {
                ["OUTER_IF_CLAUSE"] = true,
                ["OUTER_ELSEIF_CLAUSE"] = false,
                ["INNER_IF_CLAUSE"] = false,
                ["INNER_ELSEIF_CLAUSE"] = false,
            };
            IProcessor processor = SetupXmlStyleProcessor(vc);
            foreach (string test in testCases)
            {
                RunAndVerify(test, outerIfTrueExpectedValue, processor, 9999);
            }

            // the actual tests for OUTER_ELSEIF_CLAUSE = true (inner else happens too)
            string outerElseifTrueExpectedValue = @"Start
    content: outer-elseif
        content: inner-else
Trailing stuff
<!- trailing comment -->";

            vc = new VariableCollection
            {
                ["OUTER_IF_CLAUSE"] = false,
                ["OUTER_ELSEIF_CLAUSE"] = true,
                ["INNER_IF_CLAUSE"] = false,
                ["INNER_ELSEIF_CLAUSE"] = false,
            };
            processor = SetupXmlStyleProcessor(vc);
            foreach (string test in testCases)
            {
                RunAndVerify(test, outerElseifTrueExpectedValue, processor, 9999);
            }

            // the actual tests for OUTER_ELSEIF_CLAUSE = true and INNER_IF_CLAUSE = true
            string outerElseifTrueInnerIfTrueExpectedValue = @"Start
    content: outer-elseif
        content: inner-if
Trailing stuff
<!- trailing comment -->";

            vc = new VariableCollection
            {
                ["OUTER_IF_CLAUSE"] = false,
                ["OUTER_ELSEIF_CLAUSE"] = true,
                ["INNER_IF_CLAUSE"] = true,
                ["INNER_ELSEIF_CLAUSE"] = false,
            };
            processor = SetupXmlStyleProcessor(vc);
            foreach (string test in testCases)
            {
                RunAndVerify(test, outerElseifTrueInnerIfTrueExpectedValue, processor, 9999);
            }

            // the actual tests for OUTER_ELSEIF_CLAUSE = true and INNER_ELSEIF_CLAUSE = true
            string outerElseifTrueInnerElseifTrueExpectedValue = @"Start
    content: outer-elseif
        content: inner-elseif
Trailing stuff
<!- trailing comment -->";

            vc = new VariableCollection
            {
                ["OUTER_IF_CLAUSE"] = false,
                ["OUTER_ELSEIF_CLAUSE"] = true,
                ["INNER_IF_CLAUSE"] = false,
                ["INNER_ELSEIF_CLAUSE"] = true,
            };
            processor = SetupXmlStyleProcessor(vc);
            foreach (string test in testCases)
            {
                RunAndVerify(test, outerElseifTrueInnerElseifTrueExpectedValue, processor, 9999);
            }

            // the actual tests for the outer else happening
            string outerElseHappensExpectedValue = @"Start
    content: outer-else
Trailing stuff
<!- trailing comment -->";

            vc = new VariableCollection
            {
                ["OUTER_IF_CLAUSE"] = false,
                ["OUTER_ELSEIF_CLAUSE"] = false,
                ["INNER_IF_CLAUSE"] = false,
                ["INNER_ELSEIF_CLAUSE"] = false,
            };
            processor = SetupXmlStyleProcessor(vc);
            foreach (string test in testCases)
            {
                RunAndVerify(test, outerElseHappensExpectedValue, processor, 9999);
            }
        }

        #endregion XmlBlockComments

        #region Razor style tests

        [Fact]
        public void VerifyRazorBlockCommentEmbeddedInElseTest()
        {
            IList<string> testCases = new List<string>();
            string noDefaultValue = @"Start
@*#if (OUTER_IF_CLAUSE)
    content: outer-if
#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
#else
    content: outer-else
    @*#if (INNER_IF_CLAUSE)
        content: inner-if
    #elseif (INNER_ELSEIF_CLAUSE)
        content: inner-elseif
    #else
        content: inner-else
    #endif
#endif*@
Trailing stuff
@* trailing comment *@";
            testCases.Add(noDefaultValue);

            string outerIfDefaultValue = @"Start
@*#if (OUTER_IF_CLAUSE) *@
    content: outer-if
@*#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
#else
    content: outer-else
    @*#if (INNER_IF_CLAUSE)
        content: inner-if
    #elseif (INNER_ELSEIF_CLAUSE)
        content: inner-elseif
    #else
        content: inner-else
    #endif
#endif*@
Trailing stuff
@* trailing comment *@";
            testCases.Add(outerIfDefaultValue);

            string outerElseifDefaultValue = @"Start
@*#if (OUTER_IF_CLAUSE)
    content: outer-if
#elseif (OUTER_ELSEIF_CLAUSE) *@
    content: outer-elseif
@*#else
    content: outer-else
    @*#if (INNER_IF_CLAUSE)
        content: inner-if
    #elseif (INNER_ELSEIF_CLAUSE)
        content: inner-elseif
    #else
        content: inner-else
    #endif
#endif*@
Trailing stuff
@* trailing comment *@";
            testCases.Add(outerElseifDefaultValue);

            // this one sees formatted weird, but correct.
            string outerElseDefaultValue = @"Start
@*#if (OUTER_IF_CLAUSE)
    content: outer-if
#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
#else *@
    content: outer-else
    @*#if (INNER_IF_CLAUSE)
        content: inner-if
    #elseif (INNER_ELSEIF_CLAUSE)
        content: inner-elseif
    #else
        content: inner-else
    #endif
#endif*@
Trailing stuff
@* trailing comment *@";
            testCases.Add(outerElseDefaultValue);

            string innerIfDefaultValue = @"Start
@*#if (OUTER_IF_CLAUSE)
    content: outer-if
#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
#else 
    content: outer-else
    @*#if (INNER_IF_CLAUSE) *@
        content: inner-if
    @*#elseif (INNER_ELSEIF_CLAUSE)
        content: inner-elseif
    #else
        content: inner-else
    #endif
#endif*@
Trailing stuff
@* trailing comment *@";
            testCases.Add(innerIfDefaultValue);

            string innerElseifDefaultValue = @"Start
@*#if (OUTER_IF_CLAUSE)
    content: outer-if
#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
#else 
    content: outer-else
    @*#if (INNER_IF_CLAUSE)
        content: inner-if
    @*#elseif (INNER_ELSEIF_CLAUSE) *@
        content: inner-elseif
    @*#else
        content: inner-else
    #endif
#endif*@
Trailing stuff
@* trailing comment *@";
            testCases.Add(innerElseifDefaultValue);

            string innerElseDefaultValue = @"Start
@*#if (OUTER_IF_CLAUSE)
    content: outer-if
#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
#else 
    content: outer-else
    @*#if (INNER_IF_CLAUSE)
        content: inner-if
    #elseif (INNER_ELSEIF_CLAUSE)
        content: inner-elseif
    @*#else*@
        content: inner-else
    #endif
#endif*@
Trailing stuff
@* trailing comment *@";
            testCases.Add(innerElseDefaultValue);

            string outerIfTrueExpectedValue = @"Start
    content: outer-if
Trailing stuff
@* trailing comment *@";
            VariableCollection vc = new VariableCollection
            {
                ["OUTER_IF_CLAUSE"] = true,
                ["OUTER_ELSEIF_CLAUSE"] = false,
                ["INNER_IF_CLAUSE"] = false,
                ["INNER_ELSEIF_CLAUSE"] = false,
            };
            IProcessor processor = SetupRazorStyleProcessor(vc);
            foreach (string test in testCases)
            {
                RunAndVerify(test, outerIfTrueExpectedValue, processor, 9999);
            }

            string outerElseifTrueExpectedValue = @"Start
    content: outer-elseif
Trailing stuff
@* trailing comment *@";
            vc = new VariableCollection
            {
                ["OUTER_IF_CLAUSE"] = false,
                ["OUTER_ELSEIF_CLAUSE"] = true,
                ["INNER_IF_CLAUSE"] = false,
                ["INNER_ELSEIF_CLAUSE"] = false,
            };
            processor = SetupRazorStyleProcessor(vc);
            foreach (string test in testCases)
            {
                RunAndVerify(test, outerElseifTrueExpectedValue, processor, 9999);
            }

            string outerElseHappensInnerIfTrueExpectedValue = @"Start
    content: outer-else
        content: inner-if
Trailing stuff
@* trailing comment *@";
            vc = new VariableCollection
            {
                ["OUTER_IF_CLAUSE"] = false,
                ["OUTER_ELSEIF_CLAUSE"] = false,
                ["INNER_IF_CLAUSE"] = true,
                ["INNER_ELSEIF_CLAUSE"] = false,
            };
            processor = SetupRazorStyleProcessor(vc);
            foreach (string test in testCases)
            {
                RunAndVerify(test, outerElseHappensInnerIfTrueExpectedValue, processor, 9999);
            }

            string outerElseHappensInnerElseifTrueExpectedValue = @"Start
    content: outer-else
        content: inner-elseif
Trailing stuff
@* trailing comment *@";
            vc = new VariableCollection
            {
                ["OUTER_IF_CLAUSE"] = false,
                ["OUTER_ELSEIF_CLAUSE"] = false,
                ["INNER_IF_CLAUSE"] = false,
                ["INNER_ELSEIF_CLAUSE"] = true,
            };
            processor = SetupRazorStyleProcessor(vc);
            foreach (string test in testCases)
            {
                RunAndVerify(test, outerElseHappensInnerElseifTrueExpectedValue, processor, 9999);
            }

            string outerElseHappensInnerElseHappensExpectedValue = @"Start
    content: outer-else
        content: inner-else
Trailing stuff
@* trailing comment *@";
            vc = new VariableCollection
            {
                ["OUTER_IF_CLAUSE"] = false,
                ["OUTER_ELSEIF_CLAUSE"] = false,
                ["INNER_IF_CLAUSE"] = false,
                ["INNER_ELSEIF_CLAUSE"] = false,
            };
            processor = SetupRazorStyleProcessor(vc);
            foreach (string test in testCases)
            {
                RunAndVerify(test, outerElseHappensInnerElseHappensExpectedValue, processor, 9999);
            }
        }

        [Fact]
        public void RazorBlockCommentBasicTest()
        {
            IList<string> testCases = new List<string>();

            string basicValue = @"Start
@*#if (CLAUSE)
    content: if
#elseif (CLAUSE_2)
    content: elseif
#else
    content: else
#endif*@
Trailing stuff";
            testCases.Add(basicValue);

            string basicWithDefault = @"Start
@*#if (CLAUSE) *@
    content: if
@*#elseif (CLAUSE_2)
    content: elseif
#else
    content: else
#endif*@
Trailing stuff";
            testCases.Add(basicWithDefault);

            string expectedValueIfEmits = @"Start
    content: if
Trailing stuff";

            VariableCollection vc = new VariableCollection
            {
                ["CLAUSE"] = true,
                ["CLAUSE_2"] = true,    // irrelevant
            };
            IProcessor processor = SetupRazorStyleProcessor(vc);

            foreach (string test in testCases)
            {
                RunAndVerify(test, expectedValueIfEmits, processor, 9999);
            }

            // change the clause values so the elseif emits
            string expectedValueElseifEmits = @"Start
    content: elseif
Trailing stuff";

            vc = new VariableCollection
            {
                ["CLAUSE"] = false,
                ["CLAUSE_2"] = true,
            };
            processor = SetupRazorStyleProcessor(vc);
            foreach (string test in testCases)
            {
                RunAndVerify(test, expectedValueElseifEmits, processor, 9999);
            }

            // change the clause values so the else emits
            string expectedValueElseEmits = @"Start
    content: else
Trailing stuff";
            vc = new VariableCollection
            {
                ["CLAUSE"] = false,
                ["CLAUSE_2"] = false,
            };
            processor = SetupRazorStyleProcessor(vc);
            foreach (string test in testCases)
            {
                RunAndVerify(test, expectedValueElseEmits, processor, 9999);
            }
        }

        #endregion Razor style tests

        #region C-style conditionals with comment handling

        [Fact]
        public void VerifyMixedConditionalsThreeLevelEmbedding()
        {
            string originalValue = @"Lead content
////#check (LEVEL_1_IF)
//    content: level-1 if
//    ////#if (LEVEL_2_IF)
//    //    content: level-2 if
//    //    ////#check (LEVEL_3_IF)
//    //    //    content: level-3 if
//    //    ////#elseif (LEVEL_3_ELSEIF)
//    //    //    content: level-3 elseif
//    //    ////#otherwise 
//    //    //    content: level-3 else
//    //    ////#endif
//    ////#nextcheck (LEVEL_2_ELSEIF)
//    //    content: level-2 elseif
//    ////#else 
//    //    content: level-2 else
//    ////#stop
////#nextcheck true
//    content: level-1 elseif
////#else 
//    content: level-1 else
//#done
// commented trailing content
moar trailing content";

            // outer if & inner if get uncommented
            string expectedValue = @"Lead content
    content: level-1 if
        content: level-2 if
            content: level-3 if
// commented trailing content
moar trailing content";

            VariableCollection vc = new VariableCollection
            {
                ["LEVEL_1_IF"] = true,
                ["LEVEL_2_IF"] = true,
                ["LEVEL_3_IF"] = true,
                ["LEVEL_3_ELSEIF"] = true,  // irrelevant
                ["LEVEL_2_ELSEIF"] = true,  // irrelevant
            };

            IProcessor processor = SetupMadeUpStyleProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact]
        public void MixedTokenFormsTest()
        {
            IList<string> testCases = new List<string>();

            string originalValue = @"Hello
////#if (VALUE_IF)
    //if value
    //...if commented in original
////#elseif (VALUE_ELSEIF)
    //elseif value
    //...elseif commented in original
////#else
    //else value
    //...else commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment";
            testCases.Add(originalValue);

            originalValue = @"Hello
////#check (VALUE_IF)
    //if value
    //...if commented in original
////#nextcheck (VALUE_ELSEIF)
    //elseif value
    //...elseif commented in original
////#otherwise
    //else value
    //...else commented in original
//#done
Past endif
    ...uncommented in original
// dont uncomment";
            testCases.Add(originalValue);

            // this one is pretty weird
            originalValue = @"Hello
//#Z_if (VALUE_IF)
    //if value
    //...if commented in original
////#Z_elseif (VALUE_ELSEIF)
    //elseif value
    //...elseif commented in original
////#Z_else
    //else value
    //...else commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment";
            testCases.Add(originalValue);

            originalValue = @"Hello
////#check (VALUE_IF)
    //if value
    //...if commented in original
////#elseif (VALUE_ELSEIF)
    //elseif value
    //...elseif commented in original
////#otherwise
    //else value
    //...else commented in original
//#nomore
Past endif
    ...uncommented in original
// dont uncomment";
            testCases.Add(originalValue);

            string expectedValue = @"Hello
    else value
    ...else commented in original
Past endif
    ...uncommented in original
// dont uncomment";

            VariableCollection vc = new VariableCollection
            {
                ["VALUE_IF"] = false,       // must be false for the else to process
                ["VALUE_ELSEIF"] = false    // must be false for the else to process
            };
            IProcessor processor = SetupMadeUpStyleProcessor(vc);

            foreach (string test in testCases)
            {
                RunAndVerify(test, expectedValue, processor, 28);
            }
        }

        [Fact]
        public void MultiTokenFormsBaseTest()
        {
            IList<string> testCases = new List<string>();

            // special #if (true)
            string originalValue = @"Hello
////#if (VALUE_IF)
    //if value
    //...if commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment";
            testCases.Add(originalValue);

            string madeUpIfsValue = @"Hello
////#check (VALUE_IF)
    //if value
    //...if commented in original
//#nomore
Past endif
    ...uncommented in original
// dont uncomment";
            testCases.Add(madeUpIfsValue);

            string expectedValue = @"Hello
    if value
    ...if commented in original
Past endif
    ...uncommented in original
// dont uncomment";

            VariableCollection vc = new VariableCollection
            {
                ["VALUE_IF"] = true,       // must be false for the else to process
            };
            IProcessor processor = SetupMadeUpStyleProcessor(vc);

            foreach (string test in testCases)
            {
                RunAndVerify(test, expectedValue, processor, 28);
            }
        }

        #endregion C-style conditionals with comment handling

        #region C-style embedded conditionals with comments

        /// <summary>
        /// Tests that the inner if-elseif-else with special tokens gets processed correctly.
        /// </summary>
        [Fact]
        public void VerifyOuterIfAndEmbeddedConditionals()
        {
            string originalValue = @"Lead content
////#if (OUTER_IF)
//      outer if content
//		////#if (INNER_IF)
//          // inner if content
//      ////#elseif (INNER_ELSEIF)
//          // inner elseif content
//		////#else
//          // inner else content
//		//#endif
////#else
//      outer else content
//#endif
// commented trailing content
moar trailing content";

            // outer if & inner if get uncommented
            string expectedValue = @"Lead content
      outer if content
           inner if content
// commented trailing content
moar trailing content";

            VariableCollection vc = new VariableCollection
            {
                ["OUTER_IF"] = true,
                ["INNER_IF"] = true,
                ["INNER_ELSEIF"] = true
            };
            IProcessor processor = SetupCStyleWithCommentsProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);

            // outer if & inner elseif
            expectedValue = @"Lead content
      outer if content
           inner elseif content
// commented trailing content
moar trailing content";

            vc = new VariableCollection
            {
                ["OUTER_IF"] = true,
                ["INNER_IF"] = false,
                ["INNER_ELSEIF"] = true
            };
            processor = SetupCStyleWithCommentsProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);

            // outer if & inner else
            expectedValue = @"Lead content
      outer if content
           inner else content
// commented trailing content
moar trailing content";

            vc = new VariableCollection
            {
                ["OUTER_IF"] = true,
                ["INNER_IF"] = false,
                ["INNER_ELSEIF"] = false
            };
            processor = SetupCStyleWithCommentsProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);

            // outer else - nothing from the inner if should get processed
            expectedValue = @"Lead content
      outer else content
// commented trailing content
moar trailing content";

            vc = new VariableCollection
            {
                ["OUTER_IF"] = false,
                ["INNER_IF"] = true,   // irrelevant
                ["INNER_ELSEIF"] = true // ireelevant
            };
            processor = SetupCStyleWithCommentsProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact]
        public void VerifyThreeLevelEmbedding()
        {
            string originalValue = @"Lead content
////#if (LEVEL_1_IF)
//    content: level-1 if
//    ////#if (LEVEL_2_IF)
//    //    content: level-2 if
//    //    ////#if (LEVEL_3_IF)
//    //    //    content: level-3 if
//    //    ////#elseif (LEVEL_3_ELSEIF)
//    //    //    content: level-3 elseif
//    //    ////#else 
//    //    //    content: level-3 else
//    //    ////#endif
//    ////#elseif (LEVEL_2_ELSEIF)
//    //    content: level-2 elseif
//    ////#else 
//    //    content: level-2 else
//    ////#endif
////#elseif true
//    content: level-1 elseif
////#else 
//    content: level-1 else
//#endif
// commented trailing content
moar trailing content";

            // outer if & inner if get uncommented
            string expectedValue = @"Lead content
    content: level-1 if
        content: level-2 if
            content: level-3 if
// commented trailing content
moar trailing content";

            VariableCollection vc = new VariableCollection
            {
                ["LEVEL_1_IF"] = true,
                ["LEVEL_2_IF"] = true,
                ["LEVEL_3_IF"] = true,
                ["LEVEL_3_ELSEIF"] = true,  // irrelevant
                ["LEVEL_2_ELSEIF"] = true,  // irrelevant
            };
            IProcessor processor = SetupCStyleWithCommentsProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        #endregion embedded conditionals 

        #region commenting / uncommenting parts of conditionals
        
        /// <summary>
        /// Tests that the if block is uncommented in each of the scenarios
        /// because the if token is special and the clause is true in each case.
        /// </summary>
        [Fact]
        public void VerifySpecialIfTrueUncomments()
        {
            IList<string> testCases = new List<string>();

            // special #if (true)
            string ifOnlyValue = @"Hello
////#if (VALUE_IF)
    //if value
    //...if commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment";
            testCases.Add(ifOnlyValue);

            // special #if (true)
            // regular #else
            string ifElseRegularValue = @"Hello
////#if (VALUE_IF)
    //if value
    //...if commented in original
//#else
    //else value
    //...else commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment";
            testCases.Add(ifElseRegularValue);

            // special #if (true)
            // special #else ignored
            string ifElseSpecialValue = @"Hello
////#if (VALUE_IF)
    //if value
    //...if commented in original
////#else
    //else value
    //...else commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment";
            testCases.Add(ifElseSpecialValue);

            // special #if (true)
            // regular #elseif
            // regular #else
            string ifElseifRegularElseRegularValue = @"Hello
////#if (VALUE_IF)
    //if value
    //...if commented in original
//#elseif (VALUE_ELSEIF)
    //elseif value
    //...elseif commented in original
//#else
    //else value
    //...else commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment";
            testCases.Add(ifElseifRegularElseRegularValue);

            // special #if (true)
            // special #elseif
            // regular #else
            string ifElseifSpecialElseRegularValue = @"Hello
////#if (VALUE_IF)
    //if value
    //...if commented in original
////#elseif (VALUE_ELSEIF)
    //elseif value
    //...elseif commented in original
//#else
    //else value
    //...else commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment";
            testCases.Add(ifElseifSpecialElseRegularValue);

            // special #if (true)
            // regular #elseif
            // special #else
            string ifElseifRegularElseSpecialValue = @"Hello
////#if (VALUE_IF)
    //if value
    //...if commented in original
//#elseif (VALUE_ELSEIF)
    //elseif value
    //...elseif commented in original
////#else
    //else value
    //...else commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment";
            testCases.Add(ifElseifRegularElseSpecialValue);

            // special #if (true)
            // special #elseif
            // special #else
            string ifElseifSpecialElseSpecialValue = @"Hello
////#if (VALUE_IF)
    //if value
    //...if commented in original
////#elseif (VALUE_ELSEIF)
    //elseif value
    //...elseif commented in original
////#else
    //else value
    //...else commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment";
            testCases.Add(ifElseifSpecialElseSpecialValue);

            // with the if is true, all of the above test cases should emit this
            string expectedValue = @"Hello
    if value
    ...if commented in original
Past endif
    ...uncommented in original
// dont uncomment";

            // setup for the if being true - always take the if
            VariableCollection vc = new VariableCollection {
                ["VALUE_IF"] = true,            // should be true to get the if to process
                ["VALUE_ELSEIF"] = false        // shouldn't matter, since the if is always true
            };
            IProcessor processor = SetupCStyleWithCommentsProcessor(vc);

            foreach (string test in testCases)
            {
                RunAndVerify(test, expectedValue, processor, 28);
            }
        }

        /// <summary>
        /// Tests that the elseif block is uncommented in each of the scenarios
        /// because the elseif token is special and the clause is true in each case.
        /// </summary>
        [Fact]
        public void VerifySpecialElseifTrueUncomments()
        {
            IList<string> testCases = new List<string>();

            //#if
            ////#elseif
            string ifRegularValue = @"Hello
//#if (VALUE_IF)
    //if value
    //...if commented in original
////#elseif (VALUE_ELSEIF)
    //elseif value
    //...elseif commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment";
            testCases.Add(ifRegularValue);

            ////#if
            ////#elseif
            string ifSpecialValue = @"Hello
////#if (VALUE_IF)
    //if value
    //...if commented in original
////#elseif (VALUE_ELSEIF)
    //elseif value
    //...elseif commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment";
            testCases.Add(ifSpecialValue);

            //#if
            ////#elseif
            //#else
            string ifRegularElseRegularValue = @"Hello
//#if (VALUE_IF)
    //if value
    //...if commented in original
////#elseif (VALUE_ELSEIF)
    //elseif value
    //...elseif commented in original
//#else
    //else value
    //...else commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment";
            testCases.Add(ifRegularElseRegularValue);

            ////#if
            ////#elseif
            //#else
            string ifSpecialElseRegularValue = @"Hello
////#if (VALUE_IF)
    //if value
    //...if commented in original
////#elseif (VALUE_ELSEIF)
    //elseif value
    //...elseif commented in original
//#else
    //else value
    //...else commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment";
            testCases.Add(ifSpecialElseRegularValue);

            //#if
            ////#elseif
            ////#else
            string ifRegularElseSpecialValue = @"Hello
//#if (VALUE_IF)
    //if value
    //...if commented in original
////#elseif (VALUE_ELSEIF)
    //elseif value
    //...elseif commented in original
////#else
    //else value
    //...else commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment";
            testCases.Add(ifRegularElseSpecialValue);

            ////#if
            ////#elseif
            ////#else
            string ifSpecialElseSpecialValue = @"Hello
////#if (VALUE_IF)
    //if value
    //...if commented in original
////#elseif (VALUE_ELSEIF)
    //elseif value
    //...elseif commented in original
////#else
    //else value
    //...else commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment";
            testCases.Add(ifSpecialElseSpecialValue);

            // with the if false and the elseif true, all of the above test cases should emit this
            string expectedValue = @"Hello
    elseif value
    ...elseif commented in original
Past endif
    ...uncommented in original
// dont uncomment";

            // setup for the if being true - always take the if
            VariableCollection vc = new VariableCollection
            {
                ["VALUE_IF"] = false,           // must be false, to get the elseif to process
                ["VALUE_ELSEIF"] = true         // must be true to get the elseif to process
            };
            IProcessor processor = SetupCStyleWithCommentsProcessor(vc);

            foreach (string test in testCases)
            {
                RunAndVerify(test, expectedValue, processor, 28);
            }
        }

        /// <summary>
        /// Tests that the else block is uncommented in each of the scenarios
        /// because the if and elseif conditions (if present) are false in each case.
        /// </summary>
        [Fact]
        public void VerifySpecialElseTrueUncomments()
        {
            IList<string> testCases = new List<string>();

            //#if
            ////#else
            string ifRegularValue = @"Hello
//#if (VALUE_IF)
    //if value
    //...if commented in original
////#else
    //else value
    //...else commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment";
            testCases.Add(ifRegularValue);

            ////#if
            ////#else
            string ifSpecialValue = @"Hello
////#if (VALUE_IF)
    //if value
    //...if commented in original
////#else
    //else value
    //...else commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment";
            testCases.Add(ifSpecialValue);

            //#if
            //#elseif
            ////#else
            string ifRegularElseifRegularValue = @"Hello
//#if (VALUE_IF)
    //if value
    //...if commented in original
//#elseif (VALUE_ELSEIF)
    //elseif value
    //...elseif commented in original
////#else
    //else value
    //...else commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment";
            testCases.Add(ifRegularElseifRegularValue);

            ////#if
            //#elseif
            ////#else
            string ifSpecialElseifRegularValue = @"Hello
////#if (VALUE_IF)
    //if value
    //...if commented in original
//#elseif (VALUE_ELSEIF)
    //elseif value
    //...elseif commented in original
////#else
    //else value
    //...else commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment";
            testCases.Add(ifSpecialElseifRegularValue);

            //#if
            ////#elseif
            ////#else
            string ifRegularElseifSpecialValue = @"Hello
//#if (VALUE_IF)
    //if value
    //...if commented in original
////#elseif (VALUE_ELSEIF)
    //elseif value
    //...elseif commented in original
////#else
    //else value
    //...else commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment";
            testCases.Add(ifRegularElseifSpecialValue);

            ////#if
            ////#elseif
            ////#else
            string ifSpecialElseifSpecialValue = @"Hello
////#if (VALUE_IF)
    //if value
    //...if commented in original
////#elseif (VALUE_ELSEIF)
    //elseif value
    //...elseif commented in original
////#else
    //else value
    //...else commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment";
            testCases.Add(ifSpecialElseifSpecialValue);

            // with the if false and the elseif true, all of the above test cases should emit this
            string expectedValue = @"Hello
    else value
    ...else commented in original
Past endif
    ...uncommented in original
// dont uncomment";

            // setup for the if being true - always take the if
            VariableCollection vc = new VariableCollection
            {
                ["VALUE_IF"] = false,       // must be false for the else to process
                ["VALUE_ELSEIF"] = false    // must be false for the else to process
            };
            IProcessor processor = SetupCStyleWithCommentsProcessor(vc);

            foreach (string test in testCases)
            {
                RunAndVerify(test, expectedValue, processor, 28);
            }
        }

        /// <summary>
        /// The #if condition is false, so don't emit its value in any way.
        /// </summary>
        [Fact]
        public void VerifyFalseIfDoesNotUncomment()
        {
            string ifOnlyValue = @"Hello
////#if (VALUE_IF)
    //if value
    //...if commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment";

            string expectedValue = @"Hello
Past endif
    ...uncommented in original
// dont uncomment";

            VariableCollection vc = new VariableCollection
            {
                ["VALUE_IF"] = false,    // should be true to get the if to process
            };
            IProcessor processor = SetupCStyleWithCommentsProcessor(vc);

            // Test with just an if condition
            RunAndVerify(ifOnlyValue, expectedValue, processor, 28);
        }

        /// <summary>
        /// The #if condition is false, so don't emit its value in any way.
        /// But emit the else value without modification (because its not the special #else)
        /// </summary>
        [Fact]
        public void VerifyFalseIfDoesNotUncommentButElseIsEmitted()
        {
            string ifElseValue = @"Hello
////#if (VALUE_IF)
    //if value
    //...if commented in original
//#else
    //else value
    //...else commented in original - stays commented
//#endif
Past endif
    ...uncommented in original
// dont uncomment";

            string expectedValue = @"Hello
    //else value
    //...else commented in original - stays commented
Past endif
    ...uncommented in original
// dont uncomment";

            VariableCollection vc = new VariableCollection
            {
                ["VALUE_IF"] = false,    // should be true to get the if to process
            };
            IProcessor processor = SetupCStyleWithCommentsProcessor(vc);

            // Test with just an if condition
            RunAndVerify(ifElseValue, expectedValue, processor, 28);
        }

        /// <summary>
        /// Tests that the #else block is uncommented in each of the scenarios because:
        ///     its the special #else
        ///     and the if & elseif conditions are false.
        /// </summary>
        [Fact]
        public void VerifyElseUncomments()
        {
            string ifElseValue = @"Hello
//#if (VALUE_IF)
    //if value
    //...if commented in original
////#else
    //else value
    //...else commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment";

            string ifElseifElseValue = @"Hello
//#if (VALUE_IF)
    //if value
    //...if commented in original
//#elseif (VALUE_ELSEIF)
    //elseif value
    //...elseif commented in original
////#else
    //else value
    //...else commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment";

            // all of the above test cases should emit this
            string expectedValue = @"Hello
    else value
    ...else commented in original
Past endif
    ...uncommented in original
// dont uncomment";

            VariableCollection vc = new VariableCollection
            {
                ["VALUE_IF"] = false,
                ["VALUE_ELSEIF"] = false
            };
            IProcessor processor = SetupCStyleWithCommentsProcessor(vc);

            // test with an if-else condition
            RunAndVerify(ifElseValue, expectedValue, processor, 28);

            // test with an if-elseif-else condition
            RunAndVerify(ifElseifElseValue, expectedValue, processor, 28);
        }

        /// <summary>
        /// Tests that the first elseif block is uncommented
        /// It's the one with the true condition
        /// </summary>
        [Fact]
        public void VerifyFirstElseifUncomments()
        {
            string ifElseifElseValue = @"Hello
//#if (VALUE_IF)
    //if value
    //...if commented in original
////#elseif (VALUE_ELSEIF_ONE)
    //elseif value one
    //...elseif one commented in original
//#elseif (VALUE_ELSEIF_TWO)
    //elseif value two
    //...elseif two commented in original
//#else
    //else value
    //...else commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment";

            // all of the above test cases should emit this
            string expectedValue = @"Hello
    elseif value one
    ...elseif one commented in original
Past endif
    ...uncommented in original
// dont uncomment";

            VariableCollection vc = new VariableCollection
            {
                ["VALUE_IF"] = false,
                ["VALUE_ELSEIF_ONE"] = true,
                ["VALUE_ELSEIF_TWO"] = true // value should not matter
            };
            IProcessor processor = SetupMadeUpStyleProcessor(vc);

            // test with an if-else condition
            RunAndVerify(ifElseifElseValue, expectedValue, processor, 28);
        }

        /// <summary>
        /// Tests the multiple special elseif's are respected. In this test, the 2nd elseif is special and should have its content uncommented.
        /// TODO: make more test with multiple elseif's
        /// </summary>
        [Fact]
        public void VerifySecondElseifUncomments()
        {
            string ifElseifElseValue = @"Hello
//#if (VALUE_IF)
    //if value
    //...if commented in original
////#elseif (VALUE_ELSEIF_ONE)
    //elseif value one
    //...elseif one commented in original
////#elseif (VALUE_ELSEIF_TWO)
    //elseif value two
    //...elseif two commented in original
//#else
    //else value
    //...else commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment";

            // all of the above test cases should emit this
            string expectedValue = @"Hello
    elseif value two
    ...elseif two commented in original
Past endif
    ...uncommented in original
// dont uncomment";

            VariableCollection vc = new VariableCollection
            {
                ["VALUE_IF"] = false,
                ["VALUE_ELSEIF_ONE"] = false,
                ["VALUE_ELSEIF_TWO"] = true
            };
            IProcessor processor = SetupMadeUpStyleProcessor(vc);

            // test with an if-else condition
            RunAndVerify(ifElseifElseValue, expectedValue, processor, 28);
        }

        [Fact]
        public void VerifyElseIfUncomments()
        {
            string value = @"Hello
//#if (VALUE)
    value
    another line
////#elseif (ELSEIF_VALUE)
    //elseif uncommented
    //...hopefully
//#else
    //Dont Uncommented the else
    //...as expected
//#endif
Past the endif
// dont uncomment";

            string expected = @"Hello
    elseif uncommented
    ...hopefully
Past the endif
// dont uncomment";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection {
                ["VALUE"] = false,
                ["ELSEIF_VALUE"] = true
            };
            IProcessor processor = SetupMadeUpStyleProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        #endregion commenting / uncommenting parts of conditionals

        #region Original Tests

        [Fact]
        public void VerifyIfEndifTrueCondition()
        {
            string value = @"Hello
    #if (VALUE)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection { ["VALUE"] = true };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfElseEndifTrueCondition()
        {
            string value = @"Hello
    #if (VALUE)
value
    #else
other
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection { ["VALUE"] = true };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfElseEndifTrueConditionContainsTabs()
        {
            string value = @"Hello
    #if " + "\t" + @" (VALUE)
value
    #else
other
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection { ["VALUE"] = true };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfElseEndifTrueConditionQuotedString()
        {
            string value = @"Hello
    #if (""Hello" + "\t" + @"There"" == VALUE)
value
    #else
other
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection { ["VALUE"] = "Hello\tThere" };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfElseEndifTrueConditionLiteralFirst()
        {
            string value = @"Hello
    #if (3 > VALUE)
value
    #else
other
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfElseEndifTrueConditionLiteralAgainst()
        {
            string value = @"Hello
    #if(3 > VALUE)
value
    #else
other
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfElseEndifTrueConditionAgainstIf()
        {
            string value = @"Hello
    #if(VALUE)
value
    #else
other
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection { ["VALUE"] = true };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfElseEndifFalseCondition()
        {
            string value = @"Hello
    #if VALUE
value
    #else
other
    #endif
There";
            string expected = @"Hello
other
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection { ["VALUE"] = false };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfElseifEndifTrueFalseCondition()
        {
            string value = @"Hello
    #if (VALUE)
value
    #elseif (VALUE2)
other
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = true,
                ["VALUE2"] = false
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfElseifEndifTrueTrueCondition()
        {
            string value = @"Hello
    #if (VALUE)
value
    #elseif (VALUE2)
other
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = true,
                ["VALUE2"] = true
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfElseifEndifFalseTrueCondition()
        {
            string value = @"Hello
    #if (VALUE)
value
    #elseif (VALUE2)
other
    #endif
There";
            string expected = @"Hello
other
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false,
                ["VALUE2"] = true
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfElseifElseEndifTrueFalseCondition()
        {
            string value = @"Hello
    #if (VALUE)
value
    #elseif (VALUE2)
other
    #else
other2
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = true,
                ["VALUE2"] = false
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfElseifElseEndifFalseTrueCondition()
        {
            string value = @"Hello
    #if (VALUE)
value
    #elseif (VALUE2)
other
    #else
other2
    #endif
There";
            string expected = @"Hello
other
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false,
                ["VALUE2"] = true
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfElseifElseEndifFalseFalseCondition()
        {
            string value = @"Hello
    #if VALUE
value
    #elseif VALUE2
other
    #else
other2
    #endif
There";
            string expected = @"Hello
other2
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false,
                ["VALUE2"] = false
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyNestedIfTrueTrue()
        {
            string value = @"Hello
    #if (VALUE)
        #if (VALUE2)
value
        #else
other
        #endif
    #else
other2
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = true,
                ["VALUE2"] = true
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfElseifElseifElseEndifTrueTrueCondition()
        {
            string value = @"Hello
        #if (VALUE)
value
        #elseif (VALUE2)
other
        #else
other2
        #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = true,
                ["VALUE2"] = true
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfElseifElseifElseEndifTrueFalseCondition()
        {
            string value = @"Hello
        #if (VALUE)
value
        #elseif (VALUE2)
other
        #else
other2
        #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = true,
                ["VALUE2"] = false
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfElseifElseifElseEndifFalseTrueCondition()
        {
            string value = @"Hello
        #if (VALUE)
value
        #elseif (VALUE2)
other
        #else
other2
        #endif
There";
            string expected = @"Hello
other
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false,
                ["VALUE2"] = true
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfElseifElseifElseEndifFalseFalseCondition()
        {
            string value = @"Hello
        #if VALUE
value
        #elseif VALUE2
other
        #else
other2
        #endif
There";
            string expected = @"Hello
other2
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false,
                ["VALUE2"] = false
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfElseifElseifEndifTrueFalseFalseCondition()
        {
            string value = @"Hello
    #if (VALUE)
value
    #elseif (VALUE2)
other
    #elseif (VALUE3)
other2
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = true,
                ["VALUE2"] = false,
                ["VALUE3"] = false
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfElseifElseifEndifFalseTrueFalseCondition()
        {
            string value = @"Hello
    #if (VALUE)
value
    #elseif (VALUE2)
other
    #elseif (VALUE3)
other2
    #endif
There";
            string expected = @"Hello
other
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false,
                ["VALUE2"] = true,
                ["VALUE3"] = false
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfElseifElseifEndifFalseFalseTrueCondition()
        {
            string value = @"Hello
    #if (VALUE)
value
    #elseif (VALUE2)
other
    #elseif (VALUE3)
other2
    #endif
There";
            string expected = @"Hello
other2
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false,
                ["VALUE2"] = false,
                ["VALUE3"] = true
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfEndifTrueEqualsCondition()
        {
            string value = @"Hello
    #if (VALUE == 2)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2L
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfEndifTrueNotEqualsCondition()
        {
            string value = @"Hello
    #if (VALUE != 3)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 4
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfEndifTrueGreaterThanCondition()
        {
            string value = @"Hello
    #if (VALUE > 3)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 4
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfEndifOperandStealing()
        {
            string value = @"Hello
    #if ((VALUE == 3) == true)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 3L
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfEndifOperandStealing2()
        {
            string value = @"Hello
    #if (!VALUE == true)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfEndifTrueGreaterThanOrEqualToCondition()
        {
            string value = @"Hello
    #if (VALUE >= 3)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 3
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfEndifFalseGreaterThanOrEqualToCondition()
        {
            string value = @"Hello
    #if (VALUE >= 3)
value
    #endif
There";
            string expected = @"Hello
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfEndifTrueLessThanCondition()
        {
            string value = @"Hello
    #if (VALUE < 3)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfEndifTrueLessThanOrEqualToCondition()
        {
            string value = @"Hello
    #if (VALUE <= 3)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 3
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfEndifTrueNotCondition()
        {
            string value = @"Hello
    #if (!VALUE)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfEndifTrueNotNotCondition()
        {
            string value = @"Hello
    #if (!!VALUE)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = true
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfEndifTrueAndCondition()
        {
            string value = @"Hello
    #if (VALUE < 3 && VALUE > 0)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfEndifTrueXorCondition()
        {
            string value = @"Hello
    #if (VALUE < 3 ^ VALUE == 7)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfEndifTrueAndAndCondition()
        {
            string value = @"Hello
    #if (VALUE < 3 && VALUE < 4 && VALUE < 5)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfEndifTrueOrCondition()
        {
            string value = @"Hello
    #if (VALUE == 6 || VALUE < 3)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfEndifTrueOrOrCondition()
        {
            string value = @"Hello
    #if (VALUE == 6 || VALUE == 7 || VALUE < 3)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfEndifTrueOrAndCondition()
        {
            string value = @"Hello
    #if (VALUE == 6 || (VALUE != 7 && VALUE < 3))
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfEndifTrueAndOrCondition()
        {
            string value = @"Hello
    #if ((VALUE != 7 && VALUE < 3) || VALUE == 6)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfEndifTrueBitwiseAndEqualsCondition()
        {
            string value = @"Hello
    #if (VALUE & 0xFFFF == 2)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfEndifTrueBitwiseOrEqualsCondition()
        {
            string value = @"Hello
    #if (VALUE | 0xFFFD == 0xFFFF)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfEndifTrueShlCondition()
        {
            string value = @"Hello
    #if (VALUE << 1 == 8)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 4
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfEndifTrueShrCondition()
        {
            string value = @"Hello
    #if (VALUE >> 1 == 2)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 4
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfEndifTrueGroupedCondition()
        {
            string value = @"Hello
    #if ((VALUE == 2) && (VALUE2 == 3))
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2L,
                ["VALUE2"] = 3L
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfElseEndifConditionUsesNull()
        {
            string value = @"Hello
    #if (VALUE2 == null)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection();
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfElseEndifConditionUsesFalse()
        {
            string value = @"Hello
    #if (!false)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection();
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfElseEndifConditionUsesDouble()
        {
            string value = @"Hello
    #if (1.2 < 2.5)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection();
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfElseEndifConditionUsesFalsePositiveHex()
        {
            string value = @"Hello
    #if (0xChicken == null)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection();
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyIfNoCondition()
        {
            string value = @"Hello
    #if
value
    #endif
There";
            string expected = @"Hello
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection();
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyConditionAtEnd()
        {
            string value = @"Hello
    #if (1.2 < 2.5)";
            string expected = @"Hello
";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection();
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyExcludeNestedCondition()
        {
            string value = @"Hello
    #if false
        #if true
            #if true
            #endif
        #endif
        #if true
            #if true
            #endif
        #endif
    #endif";
            string expected = @"Hello
";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection();
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyExcludeNestedConditionInNonTakenBranch()
        {
            string value = @"Hello
    #if true
    #else
        #if true
            #if true
            #endif
        #endif
        #if true
            #if true
            #endif
        #endif
    #endif";
            string expected = @"Hello
";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection();
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyEmitStrayToken()
        {
            string value = @"Hello
    #endif
    #else
    #elseif foo";
            string expected = @"Hello
    #endif
    #else
    #elseif foo";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection();
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            processor.Run(input, output, 28);
            //Override the change indication - the stream was technically mutated in this case,
            //  pretend it's false because the inputs and outputs are the same
            Verify(Encoding.UTF8, output, false, value, expected);
        }

        #endregion Original Tests
    }
}
