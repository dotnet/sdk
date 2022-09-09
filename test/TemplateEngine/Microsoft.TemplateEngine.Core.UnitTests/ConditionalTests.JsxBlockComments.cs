// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Core.Contracts;
using Xunit;

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

        private static readonly VariableCollection s_jsxOuterElseifTrueVariableCollection = new VariableCollection
        {
            ["OUTER_IF_CLAUSE"] = false,
            ["OUTER_ELSEIF_CLAUSE"] = true,
            ["INNER_IF_CLAUSE"] = false,
            ["INNER_ELSEIF_CLAUSE"] = false
        };

        private static readonly VariableCollection s_jsxInnerIfTrueVariableCollection = new VariableCollection
        {
            ["OUTER_IF_CLAUSE"] = false,
            ["OUTER_ELSEIF_CLAUSE"] = false,
            ["INNER_IF_CLAUSE"] = true,
            ["INNER_ELSEIF_CLAUSE"] = false
        };

        private static readonly VariableCollection s_jsxInnerElseIfTrueVariableCollection = new VariableCollection
        {
            ["OUTER_IF_CLAUSE"] = false,
            ["OUTER_ELSEIF_CLAUSE"] = false,
            ["INNER_IF_CLAUSE"] = false,
            ["INNER_ELSEIF_CLAUSE"] = true
        };

        private static readonly VariableCollection s_jsxAllFalse = new VariableCollection
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

        [Theory(DisplayName = nameof(VerifyJsxBlockCommentEmbeddedInElseTestOuterIfTrue))]
        [InlineData(JsxNoDefaultValue, JsxOuterIfTrueExpectedValue)]
        [InlineData(JsxOuterIfDefaultValue, JsxOuterIfTrueExpectedValue)]
        [InlineData(JsxOuterElseifDefaultValue, JsxOuterIfTrueExpectedValue)]
        [InlineData(JsxOuterElseDefaultValue, JsxOuterIfTrueExpectedValue)]
        [InlineData(JsxInnerIfDefaultValue, JsxOuterIfTrueExpectedValue)]
        [InlineData(JsxInnerElseifDefaultValue, JsxOuterIfTrueExpectedValue)]
        [InlineData(JsxInnerElseDefaultValue, JsxOuterIfTrueExpectedValue)]
        public void VerifyJsxBlockCommentEmbeddedInElseTestOuterIfTrue(string source, string expected)
        {
            IProcessor processor = SetupJsxBlockCommentsProcessor(JsxOuterIfTrueVariableCollection);
            RunAndVerify(source, expected, processor, 9999);
        }

        [Theory(DisplayName = nameof(VerifyJsxBlockCommentEmbeddedInElseTestJsxOuterElseifTrueExpectedValue))]
        [InlineData(JsxNoDefaultValue, JsxOuterElseifTrueExpectedValue)]
        [InlineData(JsxOuterIfDefaultValue, JsxOuterElseifTrueExpectedValue)]
        [InlineData(JsxOuterElseifDefaultValue, JsxOuterElseifTrueExpectedValue)]
        [InlineData(JsxOuterElseDefaultValue, JsxOuterElseifTrueExpectedValue)]
        [InlineData(JsxInnerIfDefaultValue, JsxOuterElseifTrueExpectedValue)]
        [InlineData(JsxInnerElseifDefaultValue, JsxOuterElseifTrueExpectedValue)]
        [InlineData(JsxInnerElseDefaultValue, JsxOuterElseifTrueExpectedValue)]
        public void VerifyJsxBlockCommentEmbeddedInElseTestJsxOuterElseifTrueExpectedValue(string source, string expected)
        {
            IProcessor processor = SetupJsxBlockCommentsProcessor(s_jsxOuterElseifTrueVariableCollection);
            RunAndVerify(source, expected, processor, 9999);
        }

        [Theory(DisplayName = nameof(VerifyJsxBlockCommentEmbeddedInElseTestJsxOuterElseHappensInnerIfTrueExpectedValue))]
        [InlineData(JsxNoDefaultValue, JsxOuterElseHappensInnerIfTrueExpectedValue)]
        [InlineData(JsxOuterIfDefaultValue, JsxOuterElseHappensInnerIfTrueExpectedValue)]
        [InlineData(JsxOuterElseifDefaultValue, JsxOuterElseHappensInnerIfTrueExpectedValue)]
        [InlineData(JsxOuterElseDefaultValue, JsxOuterElseHappensInnerIfTrueExpectedValue)]
        [InlineData(JsxInnerIfDefaultValue, JsxOuterElseHappensInnerIfTrueExpectedValue)]
        [InlineData(JsxInnerElseifDefaultValue, JsxOuterElseHappensInnerIfTrueExpectedValue)]
        [InlineData(JsxInnerElseDefaultValue, JsxOuterElseHappensInnerIfTrueExpectedValue)]
        public void VerifyJsxBlockCommentEmbeddedInElseTestJsxOuterElseHappensInnerIfTrueExpectedValue(string source, string expected)
        {
            IProcessor processor = SetupJsxBlockCommentsProcessor(s_jsxInnerIfTrueVariableCollection);
            RunAndVerify(source, expected, processor, 9999);
        }

        [Theory(DisplayName = nameof(VerifyJsxBlockCommentEmbeddedInElseTestJsxOuterElseHappensInnerElseifTrueExpectedValue))]
        [InlineData(JsxNoDefaultValue, JsxOuterElseHappensInnerElseifTrueExpectedValue)]
        [InlineData(JsxOuterIfDefaultValue, JsxOuterElseHappensInnerElseifTrueExpectedValue)]
        [InlineData(JsxOuterElseifDefaultValue, JsxOuterElseHappensInnerElseifTrueExpectedValue)]
        [InlineData(JsxOuterElseDefaultValue, JsxOuterElseHappensInnerElseifTrueExpectedValue)]
        [InlineData(JsxInnerIfDefaultValue, JsxOuterElseHappensInnerElseifTrueExpectedValue)]
        [InlineData(JsxInnerElseifDefaultValue, JsxOuterElseHappensInnerElseifTrueExpectedValue)]
        [InlineData(JsxInnerElseDefaultValue, JsxOuterElseHappensInnerElseifTrueExpectedValue)]
        public void VerifyJsxBlockCommentEmbeddedInElseTestJsxOuterElseHappensInnerElseifTrueExpectedValue(string source, string expected)
        {
            IProcessor processor = SetupJsxBlockCommentsProcessor(s_jsxInnerElseIfTrueVariableCollection);
            RunAndVerify(source, expected, processor, 9999);
        }

        [Theory(DisplayName = nameof(VerifyJsxBlockCommentEmbeddedInElseTestJsxOuterElseHappensInnerElseHappensExpectedValue))]
        [InlineData(JsxNoDefaultValue, JsxOuterElseHappensInnerElseHappensExpectedValue)]
        [InlineData(JsxOuterIfDefaultValue, JsxOuterElseHappensInnerElseHappensExpectedValue)]
        [InlineData(JsxOuterElseifDefaultValue, JsxOuterElseHappensInnerElseHappensExpectedValue)]
        [InlineData(JsxOuterElseDefaultValue, JsxOuterElseHappensInnerElseHappensExpectedValue)]
        [InlineData(JsxInnerIfDefaultValue, JsxOuterElseHappensInnerElseHappensExpectedValue)]
        [InlineData(JsxInnerElseifDefaultValue, JsxOuterElseHappensInnerElseHappensExpectedValue)]
        [InlineData(JsxInnerElseDefaultValue, JsxOuterElseHappensInnerElseHappensExpectedValue)]
        public void VerifyJsxBlockCommentEmbeddedInElseTestJsxOuterElseHappensInnerElseHappensExpectedValue(string source, string expected)
        {
            IProcessor processor = SetupJsxBlockCommentsProcessor(s_jsxAllFalse);
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

        private static readonly VariableCollection s_jsxBothClausesTrue = new VariableCollection
        {
            ["CLAUSE"] = true,
            ["CLAUSE_2"] = true // irrelevant
        };

        private static readonly VariableCollection s_jsxClauseTwoTrue = new VariableCollection
        {
            ["CLAUSE"] = false,
            ["CLAUSE_2"] = true
        };

        private static readonly VariableCollection s_jsxNeitherClauseTrue = new VariableCollection
        {
            ["CLAUSE"] = false,
            ["CLAUSE_2"] = false
        };

        [Theory(DisplayName = nameof(JsxBlockCommentsBasicTestBothClausesTrue))]
        [InlineData(JsxBasicValue, JsxIfEmitted)]
        [InlineData(JsxBasicWithDefault, JsxIfEmitted)]
        public void JsxBlockCommentsBasicTestBothClausesTrue(string test, string expected)
        {
            IProcessor processor = SetupJsxBlockCommentsProcessor(s_jsxBothClausesTrue);
            RunAndVerify(test, expected, processor, 9999);
        }

        [Theory(DisplayName = nameof(JsxBlockCommentsBasicTestClauseTwoTrue))]
        [InlineData(JsxBasicValue, JsxElseIfEmitted)]
        [InlineData(JsxBasicWithDefault, JsxElseIfEmitted)]
        public void JsxBlockCommentsBasicTestClauseTwoTrue(string test, string expected)
        {
            IProcessor processor = SetupJsxBlockCommentsProcessor(s_jsxClauseTwoTrue);
            RunAndVerify(test, expected, processor, 9999);
        }

        [Theory(DisplayName = nameof(JsxBlockCommentsBasicTestNeitherClauseTrue))]
        [InlineData(JsxBasicValue, JsxElseEmitted)]
        [InlineData(JsxBasicWithDefault, JsxElseEmitted)]
        public void JsxBlockCommentsBasicTestNeitherClauseTrue(string test, string expected)
        {
            IProcessor processor = SetupJsxBlockCommentsProcessor(s_jsxNeitherClauseTrue);
            RunAndVerify(test, expected, processor, 9999);
        }
    }
}
