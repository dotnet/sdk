// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Core.Contracts;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public partial class ConditionalTests
    {
        [Fact(DisplayName = nameof(VerifyBasicHamlCommentHandling))]
        public void VerifyBasicHamlCommentHandling()
        {
            string originalValue = @"Start
-#-#if (CLAUSE)
-#-#    Actual Comment
-#    content
-#-#endif
-# end comment
-#-# end quad comment
End";
            string expectedValue = @"Start
-#    Actual Comment
    content
-# end comment
-#-# end quad comment
End";
            VariableCollection vc = new VariableCollection
            {
                ["CLAUSE"] = true,
            };

            IProcessor processor = SetupHamlLineCommentsProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);

            string originalValueEndifChanged = @"Start
-#-#if (CLAUSE)
-#-#    Actual Comment
-#    content
-#endif
-# end comment
-#-# end quad comment
End";
            RunAndVerify(originalValueEndifChanged, expectedValue, processor, 9999);

            string originalNoCommentRemoval = @"Start
-#if (CLAUSE)
-#-#    Actual Comment
-#    content
-#-#endif
-# end comment
-#-# end quad comment
End";
            string expectedValueNoCommentRemoval = @"Start
-#-#    Actual Comment
-#    content
-# end comment
-#-# end quad comment
End";
            RunAndVerify(originalNoCommentRemoval, expectedValueNoCommentRemoval, processor, 9999);
        }

        [Fact(DisplayName = nameof(VerifyHamlCommentRemovalForEachClauseNoEmbedding))]
        public void VerifyHamlCommentRemovalForEachClauseNoEmbedding()
        {
            string originalValue = @"Start
-#-#if (IF)
-#    content: if
-#-#  Comment: if
-#    content: if part 2
-#-#  Comment: if part 2
-#-#elseif (ELSEIF)
-#-# Comment: elseif
-#    content: elseif
-#-# Comment: elseif part 2
-#    content: elseif part 2
-#-#else
-#    content: else
-#-#  Comment: else
-#-#  Comment: else 2
-#    content: else 2
-#-#endif
-# end comment
-#-# end quad comment
End";
            string ifExpectedValue = @"Start
    content: if
-#  Comment: if
    content: if part 2
-#  Comment: if part 2
-# end comment
-#-# end quad comment
End";
            VariableCollection vc = new VariableCollection
            {
                ["IF"] = true,
            };
            IProcessor processor = SetupHamlLineCommentsProcessor(vc);
            RunAndVerify(originalValue, ifExpectedValue, processor, 9999);

            string elseIfExpectedValue = @"Start
-# Comment: elseif
    content: elseif
-# Comment: elseif part 2
    content: elseif part 2
-# end comment
-#-# end quad comment
End";
            vc = new VariableCollection
            {
                ["IF"] = false,
                ["ELSEIF"] = true
            };
            processor = SetupHamlLineCommentsProcessor(vc);
            RunAndVerify(originalValue, elseIfExpectedValue, processor, 9999);

            string elseExpectedValue = @"Start
    content: else
-#  Comment: else
-#  Comment: else 2
    content: else 2
-# end comment
-#-# end quad comment
End";
            vc = new VariableCollection
            {
                ["IF"] = false,
                ["ELSEIF"] = false
            };
            processor = SetupHamlLineCommentsProcessor(vc);
            RunAndVerify(originalValue, elseExpectedValue, processor, 9999);
        }

        [Fact(DisplayName = nameof(VerifyHamlStyleCommentRemovalWithNestedClause))]
        public void VerifyHamlStyleCommentRemovalWithNestedClause()
        {
            string originalValue = @"Start
-#-#if (OUTER_IF)
    -#-# Comment: outer if
    -#content outer if
    -#-#if (INNER_IF)
        -#-# Comment: inner if
        -#content: inner if
    -#-#endif
-#-#endif
-# end comment
-#-# end quad comment
End";
            string outerTrueInnerFalseExpectedValue = @"Start
    -# Comment: outer if
    content outer if
-# end comment
-#-# end quad comment
End";
            VariableCollection vc = new VariableCollection
            {
                ["OUTER_IF"] = true,
                ["INNER_IF"] = false
            };
            IProcessor processor = SetupHamlLineCommentsProcessor(vc);
            RunAndVerify(originalValue, outerTrueInnerFalseExpectedValue, processor, 9999);

            string outerTrueInnerTrueExpectedValue = @"Start
    -# Comment: outer if
    content outer if
        -# Comment: inner if
        content: inner if
-# end comment
-#-# end quad comment
End";
            vc = new VariableCollection
            {
                ["OUTER_IF"] = true,
                ["INNER_IF"] = true
            };
            processor = SetupHamlLineCommentsProcessor(vc);
            RunAndVerify(originalValue, outerTrueInnerTrueExpectedValue, processor, 9999);
        }

        [Fact(DisplayName = nameof(VerifyHamlStyleCommentRemovalNestedDoesntRemove))]
        public void VerifyHamlStyleCommentRemovalNestedDoesntRemove()
        {
            string originalValue = @"Start
-#-#if (OUTER_IF)
    -#-# Comment: outer if
    -#content outer if
    -#-#if (INNER_IF)
        -#-# Comment: inner if
        -#content: inner if
    -#-#endif
-#-#endif
-# end comment
-#-# end quad comment
End";
            string outerTrueInnerFalseExpectedValue = @"Start
    -# Comment: outer if
    content outer if
-# end comment
-#-# end quad comment
End";
            VariableCollection vc = new VariableCollection
            {
                ["OUTER_IF"] = true,
                ["INNER_IF"] = false
            };
            IProcessor processor = SetupHamlLineCommentsProcessor(vc);
            RunAndVerify(originalValue, outerTrueInnerFalseExpectedValue, processor, 9999);

            string outerTrueInnerTrueExpectedValue = @"Start
    -# Comment: outer if
    content outer if
        -# Comment: inner if
        content: inner if
-# end comment
-#-# end quad comment
End";
            vc = new VariableCollection
            {
                ["OUTER_IF"] = true,
                ["INNER_IF"] = true
            };
            processor = SetupHamlLineCommentsProcessor(vc);
            RunAndVerify(originalValue, outerTrueInnerTrueExpectedValue, processor, 9999);
        }

        [Fact(DisplayName = nameof(VerifyHamlSignMixedConditionalsThreeLevelEmbedding))]
        public void VerifyHamlSignMixedConditionalsThreeLevelEmbedding()
        {
            string originalValue = @"Lead content
-#-#if (LEVEL_1_IF)
-#    content: level-1 if
-#    -#-#if (LEVEL_2_IF)
-#    -#    content: level-2 if
-#    -#    -#-#if (LEVEL_3_IF)
-#    -#    -#    content: level-3 if
-#    -#    -#-#elseif (LEVEL_3_ELSEIF)
-#    -#    -#    content: level-3 elseif
-#    -#    -#-#else
-#    -#    -#    content: level-3 else
-#    -#    -#-#endif
-#    -#-#elseif (LEVEL_2_ELSEIF)
-#    -#    content: level-2 elseif
-#    -#-#else
-#    -#    content: level-2 else
-#    -#-#endif
-#-#elseif true
-#    content: level-1 elseif
-#-#else
-#    content: level-1 else
-#-#endif
-# commented trailing content
moar trailing content";

            // outer if & inner if get uncommented
            string expectedValue = @"Lead content
    content: level-1 if
        content: level-2 if
            content: level-3 if
-# commented trailing content
moar trailing content";

            VariableCollection vc = new VariableCollection
            {
                ["LEVEL_1_IF"] = true,
                ["LEVEL_2_IF"] = true,
                ["LEVEL_3_IF"] = true,
                ["LEVEL_3_ELSEIF"] = true,  // irrelevant
                ["LEVEL_2_ELSEIF"] = true,  // irrelevant
            };

            IProcessor processor = SetupHamlLineCommentsProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }
    }
}
