// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public partial class ConditionalTests
    {
        // The emitted value is not valid Xml in this test because the unbalanced comment in the first if
        // doesn't cause the pseudo comment to become a real comment.
        // The test is to demonstrate that the balance checking gets reset after leaving the #if-#endif block.
        // The fact that the comment in the second if gets its final pseudo comment fixed is demonstration of the reset.
        [Fact(DisplayName = nameof(VerifyBlockCommentUnbalancedMissingEndCommentsResets))]
        public void VerifyBlockCommentUnbalancedMissingEndCommentsResets()
        {
            string originalValue = @"Start
<!-- <!-- lead comment, just 'cuz -- > -->
<!--#if (FIRST_IF)
    <!-- <!-- Unbalanced comment -- >
#endif-->

Intermediate content

<!--#if (SECOND_IF)
    <!-- <!-- second comment -- > -- >
#endif-->
<!-- <!-- trailing comment -- > -->
End";

            string expectedValue = @"Start
<!-- <!-- lead comment, just 'cuz -- > -->
    <!-- <!-- Unbalanced comment -- >

Intermediate content

    <!-- <!-- second comment -- > -->
<!-- <!-- trailing comment -- > -->
End";
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = true,
                ["SECOND_IF"] = true
            };
            IProcessor processor = SetupXmlStyleProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact(DisplayName = nameof(VerifyBlockCommentUnbalancedExtraEndCommentsResets))]
        public void VerifyBlockCommentUnbalancedExtraEndCommentsResets()
        {
            string originalValue = @"Start
<!-- <!-- lead comment, just 'cuz -- > -->
<!--#if (FIRST_IF)
    <!-- <!-- Unbalanced comment -- > -- > -- >
#endif-->

Intermediate content

<!--#if (SECOND_IF)
    <!-- <!-- second comment -- > -- >
#endif-->
<!-- <!-- trailing comment -- > -->
End";

            string expectedValue = @"Start
<!-- <!-- lead comment, just 'cuz -- > -->
    <!-- <!-- Unbalanced comment -- > --> -- >

Intermediate content

    <!-- <!-- second comment -- > -->
<!-- <!-- trailing comment -- > -->
End";
            VariableCollection vc = new VariableCollection
            {
                ["FIRST_IF"] = true,
                ["SECOND_IF"] = true
            };
            IProcessor processor = SetupXmlStyleProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact(DisplayName = nameof(VerifyBlockCommentedContentStaysCommented))]
        public void VerifyBlockCommentedContentStaysCommented()
        {
            string originalValue = @"Start
<!-- <!-- lead comment, just 'cuz -- > -->
<!--#if (VALUE)
    <!-- Comment in the content -- >
    Actual content
    <!-- Moar Comments -- >
#endif-->
<!-- <!-- trailing comment -- > -->
End";

            string expectedValue = @"Start
<!-- <!-- lead comment, just 'cuz -- > -->
    <!-- Comment in the content -->
    Actual content
    <!-- Moar Comments -->
<!-- <!-- trailing comment -- > -->
End";
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = true,
            };
            IProcessor processor = SetupXmlStyleProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        /// <summary>
        /// Temporary test, experimenting with block comments.
        /// </summary>
        [Fact(DisplayName = nameof(VerifyMultipleConsecutiveTrailingCommentsWithinContent))]
        public void VerifyMultipleConsecutiveTrailingCommentsWithinContent()
        {
            string originalValue = @"Start
<!--#if (IF_VALUE) -->
    <!-- <!-- content: outer-if -- > -- >
#endif-->
End";
            string expectedValue = @"Start
    <!-- <!-- content: outer-if -- > -->
End";

            VariableCollection vc = new VariableCollection
            {
                ["IF_VALUE"] = true,
            };
            IProcessor processor = SetupXmlStyleProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact(DisplayName = nameof(VerifyMultipleEndCommentsOnEndif))]
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

        [Fact(DisplayName = nameof(VerifyMultipleEndCommentsOnElseif))]
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

        // Tests 3-level nesting of if blocks
        // Tests multiple elseif's in the same block
        [Fact(DisplayName = nameof(VerifyThreeLevelNestedBlockComments))]
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
        [Fact(DisplayName = nameof(VerifyMultipleNestedBlockComments))]
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

        [Fact(DisplayName = nameof(VerifyXmlBlockCommentsNestedInIf_ProperComments))]
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

        [Fact(DisplayName = nameof(XmlBlockCommentBasicTest))]
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
        [Fact(DisplayName = nameof(XmlBlockCommentIfElseifElseTestWithCommentStripping))]
        public void XmlBlockCommentIfElseifElseTestWithCommentStripping()
        {
            IList<string> testCases = new List<string>();

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
        /// Tests basic conditional embedding for block XML comments.
        /// </summary>
        [Fact(DisplayName = nameof(VerifyXmlBlockCommentEmbeddedInIfTest))]
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
        /// Temporary test for isolating bugs.
        /// </summary>
        [Fact(DisplayName = nameof(MinimalXmlElseifEmbeddingTest))]
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
        /// Tests block comment embedding of conditionals in the elseif.
        /// </summary>
        [Fact(DisplayName = nameof(VerifyXmlBlockCommentEmbeddedInElseifTest))]
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
    }
}
