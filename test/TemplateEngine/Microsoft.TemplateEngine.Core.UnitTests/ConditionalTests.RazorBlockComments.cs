// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Core.Contracts;
using Xunit;

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

        [Theory(DisplayName = nameof(VerifyRazorBlockCommentEmbeddedInElseTestOuterIfTrue))]
        [InlineData(NoDefaultValue, OuterIfTrueExpectedValue)]
        [InlineData(OuterIfDefaultValue, OuterIfTrueExpectedValue)]
        [InlineData(OuterElseifDefaultValue, OuterIfTrueExpectedValue)]
        [InlineData(OuterElseDefaultValue, OuterIfTrueExpectedValue)]
        [InlineData(InnerIfDefaultValue, OuterIfTrueExpectedValue)]
        [InlineData(InnerElseifDefaultValue, OuterIfTrueExpectedValue)]
        [InlineData(InnerElseDefaultValue, OuterIfTrueExpectedValue)]
        public void VerifyRazorBlockCommentEmbeddedInElseTestOuterIfTrue(string source, string expected)
        {
            IProcessor processor = SetupRazorStyleProcessor(OuterIfTrueVariableCollection);
            RunAndVerify(source, expected, processor, 9999);
        }

        [Theory(DisplayName = nameof(VerifyRazorBlockCommentEmbeddedInElseTestOuterElseifTrueExpectedValue))]
        [InlineData(NoDefaultValue, OuterElseifTrueExpectedValue)]
        [InlineData(OuterIfDefaultValue, OuterElseifTrueExpectedValue)]
        [InlineData(OuterElseifDefaultValue, OuterElseifTrueExpectedValue)]
        [InlineData(OuterElseDefaultValue, OuterElseifTrueExpectedValue)]
        [InlineData(InnerIfDefaultValue, OuterElseifTrueExpectedValue)]
        [InlineData(InnerElseifDefaultValue, OuterElseifTrueExpectedValue)]
        [InlineData(InnerElseDefaultValue, OuterElseifTrueExpectedValue)]
        public void VerifyRazorBlockCommentEmbeddedInElseTestOuterElseifTrueExpectedValue(string source, string expected)
        {
            IProcessor processor = SetupRazorStyleProcessor(OuterElseifTrueVariableCollection);
            RunAndVerify(source, expected, processor, 9999);
        }

        [Theory(DisplayName = nameof(VerifyRazorBlockCommentEmbeddedInElseTestOuterElseHappensInnerIfTrueExpectedValue))]
        [InlineData(NoDefaultValue, OuterElseHappensInnerIfTrueExpectedValue)]
        [InlineData(OuterIfDefaultValue, OuterElseHappensInnerIfTrueExpectedValue)]
        [InlineData(OuterElseifDefaultValue, OuterElseHappensInnerIfTrueExpectedValue)]
        [InlineData(OuterElseDefaultValue, OuterElseHappensInnerIfTrueExpectedValue)]
        [InlineData(InnerIfDefaultValue, OuterElseHappensInnerIfTrueExpectedValue)]
        [InlineData(InnerElseifDefaultValue, OuterElseHappensInnerIfTrueExpectedValue)]
        [InlineData(InnerElseDefaultValue, OuterElseHappensInnerIfTrueExpectedValue)]
        public void VerifyRazorBlockCommentEmbeddedInElseTestOuterElseHappensInnerIfTrueExpectedValue(string source, string expected)
        {
            IProcessor processor = SetupRazorStyleProcessor(InnerIfTrueVariableCollection);
            RunAndVerify(source, expected, processor, 9999);
        }

        [Theory(DisplayName = nameof(VerifyRazorBlockCommentEmbeddedInElseTestOuterElseHappensInnerElseifTrueExpectedValue))]
        [InlineData(NoDefaultValue, OuterElseHappensInnerElseifTrueExpectedValue)]
        [InlineData(OuterIfDefaultValue, OuterElseHappensInnerElseifTrueExpectedValue)]
        [InlineData(OuterElseifDefaultValue, OuterElseHappensInnerElseifTrueExpectedValue)]
        [InlineData(OuterElseDefaultValue, OuterElseHappensInnerElseifTrueExpectedValue)]
        [InlineData(InnerIfDefaultValue, OuterElseHappensInnerElseifTrueExpectedValue)]
        [InlineData(InnerElseifDefaultValue, OuterElseHappensInnerElseifTrueExpectedValue)]
        [InlineData(InnerElseDefaultValue, OuterElseHappensInnerElseifTrueExpectedValue)]
        public void VerifyRazorBlockCommentEmbeddedInElseTestOuterElseHappensInnerElseifTrueExpectedValue(string source, string expected)
        {
            IProcessor processor = SetupRazorStyleProcessor(InnerElseIfTrueVariableCollection);
            RunAndVerify(source, expected, processor, 9999);
        }

        [Theory(DisplayName = nameof(VerifyRazorBlockCommentEmbeddedInElseTestOuterElseHappensInnerElseHappensExpectedValue))]
        [InlineData(NoDefaultValue, OuterElseHappensInnerElseHappensExpectedValue)]
        [InlineData(OuterIfDefaultValue, OuterElseHappensInnerElseHappensExpectedValue)]
        [InlineData(OuterElseifDefaultValue, OuterElseHappensInnerElseHappensExpectedValue)]
        [InlineData(OuterElseDefaultValue, OuterElseHappensInnerElseHappensExpectedValue)]
        [InlineData(InnerIfDefaultValue, OuterElseHappensInnerElseHappensExpectedValue)]
        [InlineData(InnerElseifDefaultValue, OuterElseHappensInnerElseHappensExpectedValue)]
        [InlineData(InnerElseDefaultValue, OuterElseHappensInnerElseHappensExpectedValue)]
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

        [Theory(DisplayName = nameof(RazorBlockCommentsBasicTestBothClausesTrue))]
        [InlineData(BasicValue, IfEmitted)]
        [InlineData(BasicWithDefault, IfEmitted)]
        public void RazorBlockCommentsBasicTestBothClausesTrue(string test, string expected)
        {
            IProcessor processor = SetupRazorStyleProcessor(BothClausesTrue);
            RunAndVerify(test, expected, processor, 9999);
        }

        [Theory(DisplayName = nameof(RazorBlockCommentsBasicTestClauseTwoTrue))]
        [InlineData(BasicValue, ElseIfEmitted)]
        [InlineData(BasicWithDefault, ElseIfEmitted)]
        public void RazorBlockCommentsBasicTestClauseTwoTrue(string test, string expected)
        {
            IProcessor processor = SetupRazorStyleProcessor(ClauseTwoTrue);
            RunAndVerify(test, expected, processor, 9999);
        }

        [Theory(DisplayName = nameof(RazorBlockCommentsBasicTestNeitherClauseTrue))]
        [InlineData(BasicValue, ElseEmitted)]
        [InlineData(BasicWithDefault, ElseEmitted)]
        public void RazorBlockCommentsBasicTestNeitherClauseTrue(string test, string expected)
        {
            IProcessor processor = SetupRazorStyleProcessor(NeitherClauseTrue);
            RunAndVerify(test, expected, processor, 9999);
        }
    }
}
