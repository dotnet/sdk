using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public partial class ConditionalTests
    {
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
    }
}
