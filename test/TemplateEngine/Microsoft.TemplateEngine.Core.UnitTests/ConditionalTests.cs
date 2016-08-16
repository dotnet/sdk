using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Abstractions.Engine;
using Microsoft.TemplateEngine.Core.Expressions.Cpp;
using Xunit;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class ConditionalTests : TestBase
    {
        #region commenting / uncommenting parts of conditionals

        [Fact]
        public void blahTest()
        {
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

            string expectedValue = @"Hello
    else value
    ...else commented in original
Past endif
    ...uncommented in original
// dont uncomment";

            int replaceOperationId = 99;    // this is normally handled in the config setup
            IOperationProvider[] operations = { new Conditional("//#if", "//#else", "//#elseif", "//#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator,
                                                                "////#if", "////#else", "////#elseif", replaceOperationId),
                                                new Replacment("//", string.Empty, replaceOperationId)
            };

            // setup for the if being true - always take the if
            VariableCollection vc = new VariableCollection
            {
                ["VALUE_IF"] = false,       // must be false for the else to process
                ["VALUE_ELSEIF"] = false    // must be false for the else to process
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

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

            int replaceOperationId = 99;    // this is normally handled in the config setup
            IOperationProvider[] operations = { new Conditional("//#if", "//#else", "//#elseif", "//#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator,
                                                                "////#if", "////#else", "////#elseif", replaceOperationId),
                                                new Replacment("//", string.Empty, replaceOperationId)
            };

            // setup for the if being true - always take the if
            VariableCollection vc = new VariableCollection {
                ["VALUE_IF"] = true,            // should be true to get the if to process
                ["VALUE_ELSEIF"] = false        // shouldn't matter, since the if is always true
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            int replaceOperationId = 99;    // this is normally handled in the config setup
            IOperationProvider[] operations = { new Conditional("//#if", "//#else", "//#elseif", "//#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator,
                                                                "////#if", "////#else", "////#elseif", replaceOperationId),
                                                new Replacment("//", string.Empty, replaceOperationId)
            };

            // setup for the if being true - always take the if
            VariableCollection vc = new VariableCollection
            {
                ["VALUE_IF"] = false,           // must be false, to get the elseif to process
                ["VALUE_ELSEIF"] = true         // must be true to get the elseif to process
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            // FAILS
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

// FAILS
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


            int replaceOperationId = 99;    // this is normally handled in the config setup
            IOperationProvider[] operations = { new Conditional("//#if", "//#else", "//#elseif", "//#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator,
                                                                "////#if", "////#else", "////#elseif", replaceOperationId),
                                                new Replacment("//", string.Empty, replaceOperationId)
            };

            // setup for the if being true - always take the if
            VariableCollection vc = new VariableCollection
            {
                ["VALUE_IF"] = false,       // must be false for the else to process
                ["VALUE_ELSEIF"] = false    // must be false for the else to process
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            int replaceOperationId = 99;    // this is normally handled in the config setup
            IOperationProvider[] operations = { new Conditional("//#if", "//#else", "//#elseif", "//#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator,
                                                                "////#if", "////#else", "////#elseif", replaceOperationId),
                                                new Replacment("//", string.Empty, replaceOperationId)
            };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE_IF"] = false,    // should be true to get the if to process
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            int replaceOperationId = 99;    // this is normally handled in the config setup
            IOperationProvider[] operations = { new Conditional("//#if", "//#else", "//#elseif", "//#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator,
                                                                "////#if", "////#else", "////#elseif", replaceOperationId),
                                                new Replacment("//", string.Empty, replaceOperationId)
            };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE_IF"] = false,    // should be true to get the if to process
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

            // Test with just an if condition
            RunAndVerify(ifElseValue, expectedValue, processor, 28);
        }

//        [Fact]
//        public void VerifyFalseIfTrueElseifUncommentsElseif()
//        {
//            string ifElseifElseValue = @"Hello
//////#if (VALUE_IF)
//    //if value
//    //...if commented in original
////#elseif (VALUE_ELSEIF)
//    //elseif value
//    //...elseif commented in original
////#else
//    //else value
//    //...else commented in original
////#endif
//Past endif
//    ...uncommented in original
//// dont uncomment";

//            // all of the above test cases should emit this
//            string expectedValue = @"Hello
//    if value
//    ...if commented in original
//Past endif
//    ...uncommented in original
//// dont uncomment";

//            int replaceOperationId = 99;    // this is normally handled in the config setup
//            IOperationProvider[] operations = { new Conditional("//#if", "//#else", "//#elseif", "//#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator,
//                                                                "////#if", "////#else", "////#elseif", replaceOperationId),
//                                                new Replacment("//", string.Empty, replaceOperationId)
//            };
//            VariableCollection vc = new VariableCollection
//            {
//                ["VALUE_IF"] = false,    // should be true to get the if to process
//                ["VALUE_ELSEIF"] = true     // shouldn't matter, since the if is always true
//            };
//            EngineConfig cfg = new EngineConfig(vc);
//            IProcessor processor = Processor.Create(cfg, operations);

//            // Test with just an if condition
//            RunAndVerify(ifElseifElseValue, expectedValue, processor, 28);
//        }

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

            int replaceOperationId = 99;    // this is normally handled in the config setup
            IOperationProvider[] operations = { new Conditional("//#if", "//#else", "//#elseif", "//#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator,
                                                                "////#if", "////#else", "////#elseif", replaceOperationId),
                                                new Replacment("//", string.Empty, replaceOperationId)
            };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE_IF"] = false,
                ["VALUE_ELSEIF"] = false
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            int replaceOperationId = 99;    // this is normally handled in the config setup
            IOperationProvider[] operations = { new Conditional("//#if", "//#else", "//#elseif", "//#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator,
                                                                "////#if", "////#else", "////#elseif", replaceOperationId),
                                                new Replacment("//", string.Empty, replaceOperationId)
            };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE_IF"] = false,
                ["VALUE_ELSEIF_ONE"] = true,
                ["VALUE_ELSEIF_TWO"] = true // value should not matter
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

            // test with an if-else condition
            RunAndVerify(ifElseifElseValue, expectedValue, processor, 28);
        }

//        [Fact]
//        public void VerifySecondElseifUncomments()
//        {
//            string ifElseifElseValue = @"Hello
////#if (VALUE_IF)
//    //if value
//    //...if commented in original
//////#elseif (VALUE_ELSEIF_ONE)
//    //elseif value one
//    //...elseif one commented in original
////#elseif (VALUE_ELSEIF_TWO)
//    //elseif value two
//    //...elseif two commented in original
////#else
//    //else value
//    //...else commented in original
////#endif
//Past endif
//    ...uncommented in original
//// dont uncomment";

//            // all of the above test cases should emit this
//            string expectedValue = @"Hello
//    elseif value two
//    ...elseif two commented in original
//Past endif
//    ...uncommented in original
//// dont uncomment";

//            int replaceOperationId = 99;    // this is normally handled in the config setup
//            IOperationProvider[] operations = { new Conditional("//#if", "//#else", "//#elseif", "//#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator,
//                                                                "////#if", "////#else", "////#elseif", replaceOperationId),
//                                                new Replacment("//", string.Empty, replaceOperationId)
//            };
//            VariableCollection vc = new VariableCollection
//            {
//                ["VALUE_IF"] = false,
//                ["VALUE_ELSEIF_ONE"] = false,
//                ["VALUE_ELSEIF_TWO"] = true
//            };
//            EngineConfig cfg = new EngineConfig(vc);
//            IProcessor processor = Processor.Create(cfg, operations);

//            // test with an if-else condition
//            RunAndVerify(ifElseifElseValue, expectedValue, processor, 28);
//        }

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

            int replaceOperationId = 99;    // this is normally handled in the config setup
            IOperationProvider[] operations = { new Conditional("//#if", "//#else", "//#elseif", "//#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator,
                                                                "////#if", "////#else", "////#elseif", replaceOperationId),
                                                new Replacment("//", string.Empty, replaceOperationId)
            };

            VariableCollection vc = new VariableCollection {
                ["VALUE"] = false,
                ["ELSEIF_VALUE"] = true
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        #endregion commenting / uncommenting parts of conditionals


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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection {["VALUE"] = true};
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection { ["VALUE"] = true };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection { ["VALUE"] = true };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection { ["VALUE"] = "Hello\tThere" };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection { ["VALUE"] = true };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection { ["VALUE"] = false };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = true,
                ["VALUE2"] = false
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = true,
                ["VALUE2"] = true
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false,
                ["VALUE2"] = true
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = true,
                ["VALUE2"] = false
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false,
                ["VALUE2"] = true
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false,
                ["VALUE2"] = false
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = true,
                ["VALUE2"] = true
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = true,
                ["VALUE2"] = true
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = true,
                ["VALUE2"] = false
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false,
                ["VALUE2"] = true
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false,
                ["VALUE2"] = false
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = true,
                ["VALUE2"] = false,
                ["VALUE3"] = false
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false,
                ["VALUE2"] = true,
                ["VALUE3"] = false
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false,
                ["VALUE2"] = false,
                ["VALUE3"] = true
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2L
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 4
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 4
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 3L
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 3
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 3
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = true
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 4
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 4
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2L,
                ["VALUE2"] = 3L
            };
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection();
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection();
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection();
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection();
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", false, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection();
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection();
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection();
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection();
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

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

            IOperationProvider[] operations = { new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator) };
            VariableCollection vc = new VariableCollection();
            EngineConfig cfg = new EngineConfig(vc);
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            processor.Run(input, output, 28);
            //Override the change indication - the stream was technically mutated in this case,
            //  pretend it's false because the inputs and outputs are the same
            Verify(Encoding.UTF8, output, false, value, expected);
        }
    }
}
