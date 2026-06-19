// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public partial class ConditionalTests
    {
        private const string NoDefaultValue = @"Start
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

        private const string OuterIfDefaultValue = @"Start
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

        private const string OuterIfTrueExpectedValue = @"Start
    content: outer-if
Trailing stuff
@* trailing comment *@";

        private const string OuterElseifDefaultValue = @"Start
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

        // this one seems formatted weird, but correct.
        private const string OuterElseDefaultValue = @"Start
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

        private const string InnerIfDefaultValue = @"Start
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

        private const string InnerElseifDefaultValue = @"Start
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

        private const string InnerElseDefaultValue = @"Start
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

        private const string OuterElseifTrueExpectedValue = @"Start
    content: outer-elseif
Trailing stuff
@* trailing comment *@";

        private const string OuterElseHappensInnerIfTrueExpectedValue = @"Start
    content: outer-else
        content: inner-if
Trailing stuff
@* trailing comment *@";

        private const string OuterElseHappensInnerElseifTrueExpectedValue = @"Start
    content: outer-else
        content: inner-elseif
Trailing stuff
@* trailing comment *@";

        private const string OuterElseHappensInnerElseHappensExpectedValue = @"Start
    content: outer-else
        content: inner-else
Trailing stuff
@* trailing comment *@";

        private static readonly VariableCollection OuterElseifTrueVariableCollection = new VariableCollection
        {
            ["OUTER_IF_CLAUSE"] = false,
            ["OUTER_ELSEIF_CLAUSE"] = true,
            ["INNER_IF_CLAUSE"] = false,
            ["INNER_ELSEIF_CLAUSE"] = false
        };

        private static readonly VariableCollection InnerIfTrueVariableCollection = new VariableCollection
        {
            ["OUTER_IF_CLAUSE"] = false,
            ["OUTER_ELSEIF_CLAUSE"] = false,
            ["INNER_IF_CLAUSE"] = true,
            ["INNER_ELSEIF_CLAUSE"] = false
        };

        private static readonly VariableCollection InnerElseIfTrueVariableCollection = new VariableCollection
        {
            ["OUTER_IF_CLAUSE"] = false,
            ["OUTER_ELSEIF_CLAUSE"] = false,
            ["INNER_IF_CLAUSE"] = false,
            ["INNER_ELSEIF_CLAUSE"] = true
        };

        private static readonly VariableCollection AllFalse = new VariableCollection
        {
            ["OUTER_IF_CLAUSE"] = false,
            ["OUTER_ELSEIF_CLAUSE"] = false,
            ["INNER_IF_CLAUSE"] = false,
            ["INNER_ELSEIF_CLAUSE"] = false
        };

        private static VariableCollection OuterIfTrueVariableCollection => new VariableCollection
        {
            ["OUTER_IF_CLAUSE"] = true,
            ["OUTER_ELSEIF_CLAUSE"] = false,
            ["INNER_IF_CLAUSE"] = false,
            ["INNER_ELSEIF_CLAUSE"] = false
        };

        [TestMethod(DisplayName = nameof(VerifyRazorBlockCommentEmbeddedInElseTestOuterIfTrue))]
        [DataRow(NoDefaultValue, OuterIfTrueExpectedValue)]
        [DataRow(OuterIfDefaultValue, OuterIfTrueExpectedValue)]
        [DataRow(OuterElseifDefaultValue, OuterIfTrueExpectedValue)]
        [DataRow(OuterElseDefaultValue, OuterIfTrueExpectedValue)]
        [DataRow(InnerIfDefaultValue, OuterIfTrueExpectedValue)]
        [DataRow(InnerElseifDefaultValue, OuterIfTrueExpectedValue)]
        [DataRow(InnerElseDefaultValue, OuterIfTrueExpectedValue)]
        public void VerifyRazorBlockCommentEmbeddedInElseTestOuterIfTrue(string source, string expected)
        {
            IProcessor processor = SetupRazorStyleProcessor(OuterIfTrueVariableCollection);
            RunAndVerify(source, expected, processor, 9999);
        }

        [TestMethod(DisplayName = nameof(VerifyRazorBlockCommentEmbeddedInElseTestOuterElseifTrueExpectedValue))]
        [DataRow(NoDefaultValue, OuterElseifTrueExpectedValue)]
        [DataRow(OuterIfDefaultValue, OuterElseifTrueExpectedValue)]
        [DataRow(OuterElseifDefaultValue, OuterElseifTrueExpectedValue)]
        [DataRow(OuterElseDefaultValue, OuterElseifTrueExpectedValue)]
        [DataRow(InnerIfDefaultValue, OuterElseifTrueExpectedValue)]
        [DataRow(InnerElseifDefaultValue, OuterElseifTrueExpectedValue)]
        [DataRow(InnerElseDefaultValue, OuterElseifTrueExpectedValue)]
        public void VerifyRazorBlockCommentEmbeddedInElseTestOuterElseifTrueExpectedValue(string source, string expected)
        {
            IProcessor processor = SetupRazorStyleProcessor(OuterElseifTrueVariableCollection);
            RunAndVerify(source, expected, processor, 9999);
        }

        [TestMethod(DisplayName = nameof(VerifyRazorBlockCommentEmbeddedInElseTestOuterElseHappensInnerIfTrueExpectedValue))]
        [DataRow(NoDefaultValue, OuterElseHappensInnerIfTrueExpectedValue)]
        [DataRow(OuterIfDefaultValue, OuterElseHappensInnerIfTrueExpectedValue)]
        [DataRow(OuterElseifDefaultValue, OuterElseHappensInnerIfTrueExpectedValue)]
        [DataRow(OuterElseDefaultValue, OuterElseHappensInnerIfTrueExpectedValue)]
        [DataRow(InnerIfDefaultValue, OuterElseHappensInnerIfTrueExpectedValue)]
        [DataRow(InnerElseifDefaultValue, OuterElseHappensInnerIfTrueExpectedValue)]
        [DataRow(InnerElseDefaultValue, OuterElseHappensInnerIfTrueExpectedValue)]
        public void VerifyRazorBlockCommentEmbeddedInElseTestOuterElseHappensInnerIfTrueExpectedValue(string source, string expected)
        {
            IProcessor processor = SetupRazorStyleProcessor(InnerIfTrueVariableCollection);
            RunAndVerify(source, expected, processor, 9999);
        }

        [TestMethod(DisplayName = nameof(VerifyRazorBlockCommentEmbeddedInElseTestOuterElseHappensInnerElseifTrueExpectedValue))]
        [DataRow(NoDefaultValue, OuterElseHappensInnerElseifTrueExpectedValue)]
        [DataRow(OuterIfDefaultValue, OuterElseHappensInnerElseifTrueExpectedValue)]
        [DataRow(OuterElseifDefaultValue, OuterElseHappensInnerElseifTrueExpectedValue)]
        [DataRow(OuterElseDefaultValue, OuterElseHappensInnerElseifTrueExpectedValue)]
        [DataRow(InnerIfDefaultValue, OuterElseHappensInnerElseifTrueExpectedValue)]
        [DataRow(InnerElseifDefaultValue, OuterElseHappensInnerElseifTrueExpectedValue)]
        [DataRow(InnerElseDefaultValue, OuterElseHappensInnerElseifTrueExpectedValue)]
        public void VerifyRazorBlockCommentEmbeddedInElseTestOuterElseHappensInnerElseifTrueExpectedValue(string source, string expected)
        {
            IProcessor processor = SetupRazorStyleProcessor(InnerElseIfTrueVariableCollection);
            RunAndVerify(source, expected, processor, 9999);
        }

        [TestMethod(DisplayName = nameof(VerifyRazorBlockCommentEmbeddedInElseTestOuterElseHappensInnerElseHappensExpectedValue))]
        [DataRow(NoDefaultValue, OuterElseHappensInnerElseHappensExpectedValue)]
        [DataRow(OuterIfDefaultValue, OuterElseHappensInnerElseHappensExpectedValue)]
        [DataRow(OuterElseifDefaultValue, OuterElseHappensInnerElseHappensExpectedValue)]
        [DataRow(OuterElseDefaultValue, OuterElseHappensInnerElseHappensExpectedValue)]
        [DataRow(InnerIfDefaultValue, OuterElseHappensInnerElseHappensExpectedValue)]
        [DataRow(InnerElseifDefaultValue, OuterElseHappensInnerElseHappensExpectedValue)]
        [DataRow(InnerElseDefaultValue, OuterElseHappensInnerElseHappensExpectedValue)]
        public void VerifyRazorBlockCommentEmbeddedInElseTestOuterElseHappensInnerElseHappensExpectedValue(string source, string expected)
        {
            IProcessor processor = SetupRazorStyleProcessor(AllFalse);
            RunAndVerify(source, expected, processor, 9999);
        }

        private const string BasicValue = @"Start
@*#if (CLAUSE)
    content: if
#elseif (CLAUSE_2)
    content: elseif
#else
    content: else
#endif*@
Trailing stuff";

        private const string BasicWithDefault = @"Start
@*#if (CLAUSE) *@
    content: if
@*#elseif (CLAUSE_2)
    content: elseif
#else
    content: else
#endif*@
Trailing stuff";

        private const string IfEmitted = @"Start
    content: if
Trailing stuff";

        private const string ElseIfEmitted = @"Start
    content: elseif
Trailing stuff";

        private const string ElseEmitted = @"Start
    content: else
Trailing stuff";

        private static readonly VariableCollection BothClausesTrue = new VariableCollection
        {
            ["CLAUSE"] = true,
            ["CLAUSE_2"] = true // irrelevant
        };

        private static readonly VariableCollection ClauseTwoTrue = new VariableCollection
        {
            ["CLAUSE"] = false,
            ["CLAUSE_2"] = true
        };

        private static readonly VariableCollection NeitherClauseTrue = new VariableCollection
        {
            ["CLAUSE"] = false,
            ["CLAUSE_2"] = false
        };

        [TestMethod(DisplayName = nameof(RazorBlockCommentsBasicTestBothClausesTrue))]
        [DataRow(BasicValue, IfEmitted)]
        [DataRow(BasicWithDefault, IfEmitted)]
        public void RazorBlockCommentsBasicTestBothClausesTrue(string test, string expected)
        {
            IProcessor processor = SetupRazorStyleProcessor(BothClausesTrue);
            RunAndVerify(test, expected, processor, 9999);
        }

        [TestMethod(DisplayName = nameof(RazorBlockCommentsBasicTestClauseTwoTrue))]
        [DataRow(BasicValue, ElseIfEmitted)]
        [DataRow(BasicWithDefault, ElseIfEmitted)]
        public void RazorBlockCommentsBasicTestClauseTwoTrue(string test, string expected)
        {
            IProcessor processor = SetupRazorStyleProcessor(ClauseTwoTrue);
            RunAndVerify(test, expected, processor, 9999);
        }

        [TestMethod(DisplayName = nameof(RazorBlockCommentsBasicTestNeitherClauseTrue))]
        [DataRow(BasicValue, ElseEmitted)]
        [DataRow(BasicWithDefault, ElseEmitted)]
        public void RazorBlockCommentsBasicTestNeitherClauseTrue(string test, string expected)
        {
            IProcessor processor = SetupRazorStyleProcessor(NeitherClauseTrue);
            RunAndVerify(test, expected, processor, 9999);
        }

        [TestMethod]
        [DataRow(true, "No Auth")]
        [DataRow(false, "Auth")]
        // File encoding is not maintained when first line of file is a conditional statement
        // https://github.com/dotnet/templating/issues/2217
        public void BomMentained(bool noAuth, string expected)
        {
            IProcessor processor = SetupRazorStyleProcessor(new VariableCollection
            {
                ["NoAuth"] = noAuth
            });
            RunAndVerify(
                @"@*#if (NoAuth)
No Auth
#else
Auth
#endif*@
Trailing stuff",
                expected + @"
Trailing stuff",
                processor,
                9999,
                emitBOM: true);
        }
    }
}
