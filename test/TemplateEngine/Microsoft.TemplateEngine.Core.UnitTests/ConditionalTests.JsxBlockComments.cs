// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public partial class ConditionalTests
    {
        private const string JsxNoDefaultValue = @"Start
{/*#if (OUTER_IF_CLAUSE)
    content: outer-if
#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
#else
    content: outer-else
    {/*#if (INNER_IF_CLAUSE)
        content: inner-if
    #elseif (INNER_ELSEIF_CLAUSE)
        content: inner-elseif
    #else
        content: inner-else
    #endif
#endif*/}
Trailing stuff
{/* trailing comment */}";

        private const string JsxOuterIfDefaultValue = @"Start
{/*#if (OUTER_IF_CLAUSE) */}
    content: outer-if
{/*#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
#else
    content: outer-else
    {/*#if (INNER_IF_CLAUSE)
        content: inner-if
    #elseif (INNER_ELSEIF_CLAUSE)
        content: inner-elseif
    #else
        content: inner-else
    #endif
#endif*/}
Trailing stuff
{/* trailing comment */}";

        private const string JsxOuterIfTrueExpectedValue = @"Start
    content: outer-if
Trailing stuff
{/* trailing comment */}";

        private const string JsxOuterElseifDefaultValue = @"Start
{/*#if (OUTER_IF_CLAUSE)
    content: outer-if
#elseif (OUTER_ELSEIF_CLAUSE) */}
    content: outer-elseif
{/*#else
    content: outer-else
    {/*#if (INNER_IF_CLAUSE)
        content: inner-if
    #elseif (INNER_ELSEIF_CLAUSE)
        content: inner-elseif
    #else
        content: inner-else
    #endif
#endif*/}
Trailing stuff
{/* trailing comment */}";

        // this one seems formatted weird, but correct.
        private const string JsxOuterElseDefaultValue = @"Start
{/*#if (OUTER_IF_CLAUSE)
    content: outer-if
#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
#else */}
    content: outer-else
    {/*#if (INNER_IF_CLAUSE)
        content: inner-if
    #elseif (INNER_ELSEIF_CLAUSE)
        content: inner-elseif
    #else
        content: inner-else
    #endif
#endif*/}
Trailing stuff
{/* trailing comment */}";

        private const string JsxInnerIfDefaultValue = @"Start
{/*#if (OUTER_IF_CLAUSE)
    content: outer-if
#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
#else
    content: outer-else
    {/*#if (INNER_IF_CLAUSE) */}
        content: inner-if
    {/*#elseif (INNER_ELSEIF_CLAUSE)
        content: inner-elseif
    #else
        content: inner-else
    #endif
#endif*/}
Trailing stuff
{/* trailing comment */}";

        private const string JsxInnerElseifDefaultValue = @"Start
{/*#if (OUTER_IF_CLAUSE)
    content: outer-if
#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
#else
    content: outer-else
    {/*#if (INNER_IF_CLAUSE)
        content: inner-if
    {/*#elseif (INNER_ELSEIF_CLAUSE) */}
        content: inner-elseif
    {/*#else
        content: inner-else
    #endif
#endif*/}
Trailing stuff
{/* trailing comment */}";

        private const string JsxInnerElseDefaultValue = @"Start
{/*#if (OUTER_IF_CLAUSE)
    content: outer-if
#elseif (OUTER_ELSEIF_CLAUSE)
    content: outer-elseif
#else
    content: outer-else
    {/*#if (INNER_IF_CLAUSE)
        content: inner-if
    #elseif (INNER_ELSEIF_CLAUSE)
        content: inner-elseif
    {/*#else*/}
        content: inner-else
    #endif
#endif*/}
Trailing stuff
{/* trailing comment */}";

        private const string JsxOuterElseifTrueExpectedValue = @"Start
    content: outer-elseif
Trailing stuff
{/* trailing comment */}";

        private const string JsxOuterElseHappensInnerIfTrueExpectedValue = @"Start
    content: outer-else
        content: inner-if
Trailing stuff
{/* trailing comment */}";

        private const string JsxOuterElseHappensInnerElseifTrueExpectedValue = @"Start
    content: outer-else
        content: inner-elseif
Trailing stuff
{/* trailing comment */}";

        private const string JsxOuterElseHappensInnerElseHappensExpectedValue = @"Start
    content: outer-else
        content: inner-else
Trailing stuff
{/* trailing comment */}";

        private static readonly VariableCollection JsxOuterElseifTrueVariableCollection = new VariableCollection
        {
            ["OUTER_IF_CLAUSE"] = false,
            ["OUTER_ELSEIF_CLAUSE"] = true,
            ["INNER_IF_CLAUSE"] = false,
            ["INNER_ELSEIF_CLAUSE"] = false
        };

        private static readonly VariableCollection JsxInnerIfTrueVariableCollection = new VariableCollection
        {
            ["OUTER_IF_CLAUSE"] = false,
            ["OUTER_ELSEIF_CLAUSE"] = false,
            ["INNER_IF_CLAUSE"] = true,
            ["INNER_ELSEIF_CLAUSE"] = false
        };

        private static readonly VariableCollection JsxInnerElseIfTrueVariableCollection = new VariableCollection
        {
            ["OUTER_IF_CLAUSE"] = false,
            ["OUTER_ELSEIF_CLAUSE"] = false,
            ["INNER_IF_CLAUSE"] = false,
            ["INNER_ELSEIF_CLAUSE"] = true
        };

        private static readonly VariableCollection JsxAllFalse = new VariableCollection
        {
            ["OUTER_IF_CLAUSE"] = false,
            ["OUTER_ELSEIF_CLAUSE"] = false,
            ["INNER_IF_CLAUSE"] = false,
            ["INNER_ELSEIF_CLAUSE"] = false
        };

        private static VariableCollection JsxOuterIfTrueVariableCollection => new VariableCollection
        {
            ["OUTER_IF_CLAUSE"] = true,
            ["OUTER_ELSEIF_CLAUSE"] = false,
            ["INNER_IF_CLAUSE"] = false,
            ["INNER_ELSEIF_CLAUSE"] = false
        };

        [TestMethod]
        [DataRow(JsxNoDefaultValue, JsxOuterIfTrueExpectedValue)]
        [DataRow(JsxOuterIfDefaultValue, JsxOuterIfTrueExpectedValue)]
        [DataRow(JsxOuterElseifDefaultValue, JsxOuterIfTrueExpectedValue)]
        [DataRow(JsxOuterElseDefaultValue, JsxOuterIfTrueExpectedValue)]
        [DataRow(JsxInnerIfDefaultValue, JsxOuterIfTrueExpectedValue)]
        [DataRow(JsxInnerElseifDefaultValue, JsxOuterIfTrueExpectedValue)]
        [DataRow(JsxInnerElseDefaultValue, JsxOuterIfTrueExpectedValue)]
        public void VerifyJsxBlockCommentEmbeddedInElseTestOuterIfTrue(string source, string expected)
        {
            IProcessor processor = SetupJsxBlockCommentsProcessor(JsxOuterIfTrueVariableCollection);
            RunAndVerify(source, expected, processor, 9999);
        }

        [TestMethod]
        [DataRow(JsxNoDefaultValue, JsxOuterElseifTrueExpectedValue)]
        [DataRow(JsxOuterIfDefaultValue, JsxOuterElseifTrueExpectedValue)]
        [DataRow(JsxOuterElseifDefaultValue, JsxOuterElseifTrueExpectedValue)]
        [DataRow(JsxOuterElseDefaultValue, JsxOuterElseifTrueExpectedValue)]
        [DataRow(JsxInnerIfDefaultValue, JsxOuterElseifTrueExpectedValue)]
        [DataRow(JsxInnerElseifDefaultValue, JsxOuterElseifTrueExpectedValue)]
        [DataRow(JsxInnerElseDefaultValue, JsxOuterElseifTrueExpectedValue)]
        public void VerifyJsxBlockCommentEmbeddedInElseTestJsxOuterElseifTrueExpectedValue(string source, string expected)
        {
            IProcessor processor = SetupJsxBlockCommentsProcessor(JsxOuterElseifTrueVariableCollection);
            RunAndVerify(source, expected, processor, 9999);
        }

        [TestMethod]
        [DataRow(JsxNoDefaultValue, JsxOuterElseHappensInnerIfTrueExpectedValue)]
        [DataRow(JsxOuterIfDefaultValue, JsxOuterElseHappensInnerIfTrueExpectedValue)]
        [DataRow(JsxOuterElseifDefaultValue, JsxOuterElseHappensInnerIfTrueExpectedValue)]
        [DataRow(JsxOuterElseDefaultValue, JsxOuterElseHappensInnerIfTrueExpectedValue)]
        [DataRow(JsxInnerIfDefaultValue, JsxOuterElseHappensInnerIfTrueExpectedValue)]
        [DataRow(JsxInnerElseifDefaultValue, JsxOuterElseHappensInnerIfTrueExpectedValue)]
        [DataRow(JsxInnerElseDefaultValue, JsxOuterElseHappensInnerIfTrueExpectedValue)]
        public void VerifyJsxBlockCommentEmbeddedInElseTestJsxOuterElseHappensInnerIfTrueExpectedValue(string source, string expected)
        {
            IProcessor processor = SetupJsxBlockCommentsProcessor(JsxInnerIfTrueVariableCollection);
            RunAndVerify(source, expected, processor, 9999);
        }

        [TestMethod]
        [DataRow(JsxNoDefaultValue, JsxOuterElseHappensInnerElseifTrueExpectedValue)]
        [DataRow(JsxOuterIfDefaultValue, JsxOuterElseHappensInnerElseifTrueExpectedValue)]
        [DataRow(JsxOuterElseifDefaultValue, JsxOuterElseHappensInnerElseifTrueExpectedValue)]
        [DataRow(JsxOuterElseDefaultValue, JsxOuterElseHappensInnerElseifTrueExpectedValue)]
        [DataRow(JsxInnerIfDefaultValue, JsxOuterElseHappensInnerElseifTrueExpectedValue)]
        [DataRow(JsxInnerElseifDefaultValue, JsxOuterElseHappensInnerElseifTrueExpectedValue)]
        [DataRow(JsxInnerElseDefaultValue, JsxOuterElseHappensInnerElseifTrueExpectedValue)]
        public void VerifyJsxBlockCommentEmbeddedInElseTestJsxOuterElseHappensInnerElseifTrueExpectedValue(string source, string expected)
        {
            IProcessor processor = SetupJsxBlockCommentsProcessor(JsxInnerElseIfTrueVariableCollection);
            RunAndVerify(source, expected, processor, 9999);
        }

        [TestMethod]
        [DataRow(JsxNoDefaultValue, JsxOuterElseHappensInnerElseHappensExpectedValue)]
        [DataRow(JsxOuterIfDefaultValue, JsxOuterElseHappensInnerElseHappensExpectedValue)]
        [DataRow(JsxOuterElseifDefaultValue, JsxOuterElseHappensInnerElseHappensExpectedValue)]
        [DataRow(JsxOuterElseDefaultValue, JsxOuterElseHappensInnerElseHappensExpectedValue)]
        [DataRow(JsxInnerIfDefaultValue, JsxOuterElseHappensInnerElseHappensExpectedValue)]
        [DataRow(JsxInnerElseifDefaultValue, JsxOuterElseHappensInnerElseHappensExpectedValue)]
        [DataRow(JsxInnerElseDefaultValue, JsxOuterElseHappensInnerElseHappensExpectedValue)]
        public void VerifyJsxBlockCommentEmbeddedInElseTestJsxOuterElseHappensInnerElseHappensExpectedValue(string source, string expected)
        {
            IProcessor processor = SetupJsxBlockCommentsProcessor(JsxAllFalse);
            RunAndVerify(source, expected, processor, 9999);
        }

        private const string JsxBasicValue = @"Start
{/*#if (CLAUSE)
    content: if
#elseif (CLAUSE_2)
    content: elseif
#else
    content: else
#endif*/}
Trailing stuff";

        private const string JsxBasicWithDefault = @"Start
{/*#if (CLAUSE) */}
    content: if
{/*#elseif (CLAUSE_2)
    content: elseif
#else
    content: else
#endif*/}
Trailing stuff";

        private const string JsxIfEmitted = @"Start
    content: if
Trailing stuff";

        private const string JsxElseIfEmitted = @"Start
    content: elseif
Trailing stuff";

        private const string JsxElseEmitted = @"Start
    content: else
Trailing stuff";

        private static readonly VariableCollection JsxBothClausesTrue = new VariableCollection
        {
            ["CLAUSE"] = true,
            ["CLAUSE_2"] = true // irrelevant
        };

        private static readonly VariableCollection JsxClauseTwoTrue = new VariableCollection
        {
            ["CLAUSE"] = false,
            ["CLAUSE_2"] = true
        };

        private static readonly VariableCollection JsxNeitherClauseTrue = new VariableCollection
        {
            ["CLAUSE"] = false,
            ["CLAUSE_2"] = false
        };

        [TestMethod]
        [DataRow(JsxBasicValue, JsxIfEmitted)]
        [DataRow(JsxBasicWithDefault, JsxIfEmitted)]
        public void JsxBlockCommentsBasicTestBothClausesTrue(string test, string expected)
        {
            IProcessor processor = SetupJsxBlockCommentsProcessor(JsxBothClausesTrue);
            RunAndVerify(test, expected, processor, 9999);
        }

        [TestMethod]
        [DataRow(JsxBasicValue, JsxElseIfEmitted)]
        [DataRow(JsxBasicWithDefault, JsxElseIfEmitted)]
        public void JsxBlockCommentsBasicTestClauseTwoTrue(string test, string expected)
        {
            IProcessor processor = SetupJsxBlockCommentsProcessor(JsxClauseTwoTrue);
            RunAndVerify(test, expected, processor, 9999);
        }

        [TestMethod]
        [DataRow(JsxBasicValue, JsxElseEmitted)]
        [DataRow(JsxBasicWithDefault, JsxElseEmitted)]
        public void JsxBlockCommentsBasicTestNeitherClauseTrue(string test, string expected)
        {
            IProcessor processor = SetupJsxBlockCommentsProcessor(JsxNeitherClauseTrue);
            RunAndVerify(test, expected, processor, 9999);
        }
    }
}
