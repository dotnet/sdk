// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Core.Contracts;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public partial class ConditionalTests
    {
        [Fact(DisplayName = nameof(VerifyBasicBatRemCommentHandling))]
        public void VerifyBasicBatRemCommentHandling()
        {
            string originalValue = @"Start
rem rem #if (CLAUSE)
rem rem    Actual Comment
rem    content
rem #endif
rem end comment
rem rem end quad comment
End";

            string expectedValue = @"Start
rem    Actual Comment
    content
rem end comment
rem rem end quad comment
End";
            VariableCollection vc = new VariableCollection
            {
                ["CLAUSE"] = true,
            };

            IProcessor processor = SetupBatFileRemLineCommentsProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);

            string originalNoCommentRemoval = @"Start
rem #if (CLAUSE)
rem rem     Actual Comment
rem    content
rem #endif
rem end comment
rem rem end quad comment
End";

            string expectedValueNoCommentRemoval = @"Start
rem rem     Actual Comment
rem    content
rem end comment
rem rem end quad comment
End";
            RunAndVerify(originalNoCommentRemoval, expectedValueNoCommentRemoval, processor, 9999);
        }

        [Fact(DisplayName = nameof(VerifyBatRemCommentRemovalForEachClauseNoEmbedding))]
        public void VerifyBatRemCommentRemovalForEachClauseNoEmbedding()
        {
            string originalValue = @"Start
rem rem #if (IF)
rem    content: if
rem rem  Comment: if
rem    content: if part 2
rem rem  Comment: if part 2
rem rem #elseif (ELSEIF)
rem rem Comment: elseif
rem    content: elseif
rem rem Comment: elseif part 2
rem    content: elseif part 2
rem rem #else
rem    content: else
rem rem  Comment: else
rem rem  Comment: else 2
rem    content: else 2
rem #endif
rem end comment
rem rem end quad comment
End";
            string ifExpectedValue = @"Start
    content: if
rem  Comment: if
    content: if part 2
rem  Comment: if part 2
rem end comment
rem rem end quad comment
End";
            VariableCollection vc = new VariableCollection
            {
                ["IF"] = true,
            };
            IProcessor processor = SetupBatFileRemLineCommentsProcessor(vc);
            RunAndVerify(originalValue, ifExpectedValue, processor, 9999);

            string elseIfExpectedValue = @"Start
rem Comment: elseif
    content: elseif
rem Comment: elseif part 2
    content: elseif part 2
rem end comment
rem rem end quad comment
End";
            vc = new VariableCollection
            {
                ["IF"] = false,
                ["ELSEIF"] = true
            };
            processor = SetupBatFileRemLineCommentsProcessor(vc);
            RunAndVerify(originalValue, elseIfExpectedValue, processor, 9999);

            string elseExpectedValue = @"Start
    content: else
rem  Comment: else
rem  Comment: else 2
    content: else 2
rem end comment
rem rem end quad comment
End";

            vc = new VariableCollection
            {
                ["IF"] = false,
                ["ELSEIF"] = false
            };
            processor = SetupBatFileRemLineCommentsProcessor(vc);
            RunAndVerify(originalValue, elseExpectedValue, processor, 9999);
        }


        [Fact(DisplayName = nameof(VerifyBatRemCommentRemovalWithNestedClause))]
        public void VerifyBatRemCommentRemovalWithNestedClause()
        {
            string originalValue = @"Start
rem rem #if (OUTER_IF)
    rem rem Comment: outer if
    rem content outer if
    rem rem #if (INNER_IF)
        rem rem Comment: inner if
        rem content: inner if
    rem rem #endif
rem rem #endif
rem end comment
rem rem end quad comment
End";
            string outerTrueInnerFalseExpectedValue = @"Start
    rem Comment: outer if
     content outer if
rem end comment
rem rem end quad comment
End";
            VariableCollection vc = new VariableCollection
            {
                ["OUTER_IF"] = true,
                ["INNER_IF"] = false
            };
            IProcessor processor = SetupBatFileRemLineCommentsProcessor(vc);
            RunAndVerify(originalValue, outerTrueInnerFalseExpectedValue, processor, 9999);

            string outerTrueInnerTrueExpectedValue = @"Start
    rem Comment: outer if
     content outer if
        rem Comment: inner if
         content: inner if
rem end comment
rem rem end quad comment
End";
            vc = new VariableCollection
            {
                ["OUTER_IF"] = true,
                ["INNER_IF"] = true
            };
            processor = SetupBatFileRemLineCommentsProcessor(vc);
            RunAndVerify(originalValue, outerTrueInnerTrueExpectedValue, processor, 9999);
        }


        [Fact(DisplayName = nameof(VerifyBatRemCommentRemovalNestedDoesntRemove))]
        public void VerifyBatRemCommentRemovalNestedDoesntRemove()
        {
            string originalValue = @"Start
rem rem #if (OUTER_IF)
    rem rem Comment: outer if
    rem content outer if
    rem rem #if (INNER_IF)
        rem rem Comment: inner if
        rem content: inner if
    rem rem #endif
rem rem #endif
rem end comment
rem rem end quad comment
End";
            string outerTrueInnerFalseExpectedValue = @"Start
    rem Comment: outer if
     content outer if
rem end comment
rem rem end quad comment
End";
            VariableCollection vc = new VariableCollection
            {
                ["OUTER_IF"] = true,
                ["INNER_IF"] = false
            };
            IProcessor processor = SetupBatFileRemLineCommentsProcessor(vc);
            RunAndVerify(originalValue, outerTrueInnerFalseExpectedValue, processor, 9999);

            // TODO: determine if this is correct, or if the inner should //#if overrides the outer ////#if
            string outerTrueInnerTrueExpectedValue = @"Start
    rem Comment: outer if
     content outer if
        rem Comment: inner if
         content: inner if
rem end comment
rem rem end quad comment
End";
            vc = new VariableCollection
            {
                ["OUTER_IF"] = true,
                ["INNER_IF"] = true
            };
            processor = SetupBatFileRemLineCommentsProcessor(vc);
            RunAndVerify(originalValue, outerTrueInnerTrueExpectedValue, processor, 9999);
        }

        [Fact(DisplayName = nameof(VerifyBatRemMixedConditionalsThreeLevelEmbedding))]
        public void VerifyBatRemMixedConditionalsThreeLevelEmbedding()
        {
            string originalValue = @"Lead content
rem rem #if (LEVEL_1_IF)
rem    content: level-1 if
rem    rem rem #if (LEVEL_2_IF)
rem    rem    content: level-2 if
rem    rem    rem rem #if (LEVEL_3_IF)
rem    rem    rem    content: level-3 if
rem    rem    rem rem #elseif (LEVEL_3_ELSEIF)
rem    rem    rem    content: level-3 elseif
rem    rem    rem rem #else
rem    rem    rem    content: level-3 else
rem    rem    rem rem #endif
rem    rem rem #elseif (LEVEL_2_ELSEIF)
rem    rem    content: level-2 elseif
rem    rem rem #else
rem    rem    content: level-2 else
rem    rem rem #endif
rem rem #elseif true
rem    content: level-1 elseif
rem rem #else
rem    content: level-1 else
rem rem #endif
rem commented trailing content
moar trailing content";

            // outer if & inner if get uncommented
            string expectedValue = @"Lead content
    content: level-1 if
        content: level-2 if
            content: level-3 if
rem commented trailing content
moar trailing content";

            VariableCollection vc = new VariableCollection
            {
                ["LEVEL_1_IF"] = true,
                ["LEVEL_2_IF"] = true,
                ["LEVEL_3_IF"] = true,
                ["LEVEL_3_ELSEIF"] = true,  // irrelevant
                ["LEVEL_2_ELSEIF"] = true,  // irrelevant
            };

            IProcessor processor = SetupBatFileRemLineCommentsProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }
    }
}
