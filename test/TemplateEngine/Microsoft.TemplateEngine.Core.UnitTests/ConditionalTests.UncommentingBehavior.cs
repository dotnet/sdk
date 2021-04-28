// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public partial class ConditionalTests
    {
        [Fact(DisplayName = nameof(VerifyMixedConditionalsThreeLevelEmbedding))]
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

        [Fact(DisplayName = nameof(MixedTokenFormsTest))]
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
                ["VALUE_IF"] = false, // must be false for the else to process
                ["VALUE_ELSEIF"] = false // must be false for the else to process
            };
            IProcessor processor = SetupMadeUpStyleProcessor(vc);

            foreach (string test in testCases)
            {
                RunAndVerify(test, expectedValue, processor, 28);
            }
        }

        [Fact(DisplayName = nameof(MultiTokenFormsBaseTest))]
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

        /// <summary>
        /// Tests that the if block is uncommented in each of the scenarios
        /// because the if token is special and the clause is true in each case.
        /// </summary>
        [Theory(DisplayName = nameof(VerifySpecialIfTrueUncomments))]
        [InlineData(
            @"Hello
////#if (VALUE_IF)
    //if value
    //...if commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment",
            "special #if (true)")]
        [InlineData(
            @"Hello
////#if (VALUE_IF)
    //if value
    //...if commented in original
//#else
    //else value
    //...else commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment",
            "special #if (true), regular #else")]
        [InlineData(
            @"Hello
////#if (VALUE_IF)
    //if value
    //...if commented in original
////#else
    //else value
    //...else commented in original
//#endif
Past endif
    ...uncommented in original
// dont uncomment",
            "special #if (true), special #else ignored")]
        [InlineData(
            @"Hello
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
// dont uncomment",
            "special #if (true), regular #elseif, regular #else")]
        [InlineData(
            @"Hello
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
// dont uncomment",
            "special #if (true), special #elseif, regular #else")]
        [InlineData(
            @"Hello
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
// dont uncomment",
            "special #if (true), regular #elseif, special #else")]
        [InlineData(
            @"Hello
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
// dont uncomment",
            "special #if (true), special #elseif, special #else")]
        public void VerifySpecialIfTrueUncomments(string test, string comment)
        {
            // with the if is true, all of the above test cases should emit this
            string expectedValue = @"Hello
    if value
    ...if commented in original
Past endif
    ...uncommented in original
// dont uncomment";

            // setup for the if being true - always take the if
            VariableCollection vc = new VariableCollection
            {
                ["VALUE_IF"] = true, // should be true to get the if to process
                ["VALUE_ELSEIF"] = false // shouldn't matter, since the if is always true
            };
            IProcessor processor = SetupCStyleWithCommentsProcessor(vc);
            RunAndVerify(test, expectedValue, processor, 28);

            //comment is unused - Asserting to bypass the warning.
            Assert.Equal(comment, comment);
        }

        /// <summary>
        /// Tests that the elseif block is uncommented in each of the scenarios
        /// because the elseif token is special and the clause is true in each case.
        /// </summary>
        [Fact(DisplayName = nameof(VerifySpecialElseifTrueUncomments))]
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
                ["VALUE_IF"] = false, // must be false, to get the elseif to process
                ["VALUE_ELSEIF"] = true // must be true to get the elseif to process
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
        [Fact(DisplayName = nameof(VerifySpecialElseTrueUncomments))]
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
                ["VALUE_ELSEIF"] = false // must be false for the else to process
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
        [Fact(DisplayName = nameof(VerifyFalseIfDoesNotUncomment))]
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
        /// But emit the else value without modification (because its not the special #else).
        /// </summary>
        [Fact(DisplayName = nameof(VerifyFalseIfDoesNotUncommentButElseIsEmitted))]
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
        [Fact(DisplayName = nameof(VerifyElseUncomments))]
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
        /// It's the one with the true condition.
        /// </summary>
        [Fact(DisplayName = nameof(VerifyFirstElseifUncomments))]
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
        /// TODO: make more test with multiple elseif's.
        /// </summary>
        [Fact(DisplayName = nameof(VerifySecondElseifUncomments))]
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

        [Fact(DisplayName = nameof(VerifyElseIfUncomments))]
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

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false,
                ["ELSEIF_VALUE"] = true
            };
            IProcessor processor = SetupMadeUpStyleProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }
    }
}
