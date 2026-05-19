// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Core.Contracts;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public partial class ConditionalTests
    {
        [Fact(DisplayName = nameof(VerifyBasicQuadCommentRemoval))]
        public void VerifyBasicQuadCommentRemoval()
        {
            string originalValue = @"Start
////#if (CLAUSE)
////    Actual Comment
//    content
//#endif
// end comment
//// end quad comment
End";
            string expectedValue = @"Start
//    Actual Comment
    content
// end comment
//// end quad comment
End";
            VariableCollection vc = new VariableCollection
            {
                ["CLAUSE"] = true,
            };

            IProcessor processor = SetupCStyleWithCommentsProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);

            // The if is //#if (as opposed to ////#if) so no comment removal in the content
            string originalNoCommentRemoval = @"Start
////#if (CLAUSE)
////    Actual Comment
//    content
//#endif
// end comment
//// end quad comment
End";
            string expectedValueNoCommentRemoval = @"Start
//    Actual Comment
    content
// end comment
//// end quad comment
End";
            RunAndVerify(originalNoCommentRemoval, expectedValueNoCommentRemoval, processor, 9999);
        }

        [Fact(DisplayName = nameof(VerifyQuadCommentRemovalForEachClauseNoEmbedding))]
        public void VerifyQuadCommentRemovalForEachClauseNoEmbedding()
        {
            string originalValue = @"Start
////#if (IF)
//    content: if
////  Comment: if
//    content: if part 2
////  Comment: if part 2
////#elseif (ELSEIF)
//// Comment: elseif
//    content: elseif
//// Comment: elseif part 2
//    content: elseif part 2
////#else
//    content: else
////  Comment: else
////  Comment: else 2
//    content: else 2
//#endif
// end comment
//// end quad comment
End";
            string ifExpectedValue = @"Start
    content: if
//  Comment: if
    content: if part 2
//  Comment: if part 2
// end comment
//// end quad comment
End";
            VariableCollection vc = new VariableCollection
            {
                ["IF"] = true,
            };
            IProcessor processor = SetupCStyleWithCommentsProcessor(vc);
            RunAndVerify(originalValue, ifExpectedValue, processor, 9999);

            string elseIfExpectedValue = @"Start
// Comment: elseif
    content: elseif
// Comment: elseif part 2
    content: elseif part 2
// end comment
//// end quad comment
End";
            vc = new VariableCollection
            {
                ["IF"] = false,
                ["ELSEIF"] = true
            };
            processor = SetupCStyleWithCommentsProcessor(vc);
            RunAndVerify(originalValue, elseIfExpectedValue, processor, 9999);

            string elseExpectedValue = @"Start
    content: else
//  Comment: else
//  Comment: else 2
    content: else 2
// end comment
//// end quad comment
End";
            vc = new VariableCollection
            {
                ["IF"] = false,
                ["ELSEIF"] = false
            };
            processor = SetupCStyleWithCommentsProcessor(vc);
            RunAndVerify(originalValue, elseExpectedValue, processor, 9999);
        }

        [Fact(DisplayName = nameof(VerifyQuadCommentRemovalWithNestedClause))]
        public void VerifyQuadCommentRemovalWithNestedClause()
        {
            string originalValue = @"Start
////#if (OUTER_IF)
    //// Comment: outer if
    //content outer if
    ////#if (INNER_IF)
        //// Comment: inner if
        //content: inner if
    //#endif
//#endif
// end comment
//// end quad comment
End";
            string outerTrueInnerFalseExpectedValue = @"Start
    // Comment: outer if
    content outer if
// end comment
//// end quad comment
End";
            VariableCollection vc = new VariableCollection
            {
                ["OUTER_IF"] = true,
                ["INNER_IF"] = false
            };
            IProcessor processor = SetupCStyleWithCommentsProcessor(vc);
            RunAndVerify(originalValue, outerTrueInnerFalseExpectedValue, processor, 9999);

            string outerTrueInnerTrueExpectedValue = @"Start
    // Comment: outer if
    content outer if
        // Comment: inner if
        content: inner if
// end comment
//// end quad comment
End";
            vc = new VariableCollection
            {
                ["OUTER_IF"] = true,
                ["INNER_IF"] = true
            };
            processor = SetupCStyleWithCommentsProcessor(vc);
            RunAndVerify(originalValue, outerTrueInnerTrueExpectedValue, processor, 9999);
        }

        [Fact(DisplayName = nameof(VerifyQuadCommentRemovalNestedDoesntRemove))]
        public void VerifyQuadCommentRemovalNestedDoesntRemove()
        {
            string originalValue = @"Start
////#if (OUTER_IF)
    //// Comment: outer if
    //content outer if
    //#if (INNER_IF)
        //// Comment: inner if
        //content: inner if
    //#endif
//#endif
// end comment
//// end quad comment
End";
            string outerTrueInnerFalseExpectedValue = @"Start
    // Comment: outer if
    content outer if
// end comment
//// end quad comment
End";
            VariableCollection vc = new VariableCollection
            {
                ["OUTER_IF"] = true,
                ["INNER_IF"] = false
            };
            IProcessor processor = SetupCStyleWithCommentsProcessor(vc);
            RunAndVerify(originalValue, outerTrueInnerFalseExpectedValue, processor, 9999);

            // TODO: determine if this is correct, or if the inner should //#if overrides the outer ////#if
            string outerTrueInnerTrueExpectedValue = @"Start
    // Comment: outer if
    content outer if
        // Comment: inner if
        content: inner if
// end comment
//// end quad comment
End";
            vc = new VariableCollection
            {
                ["OUTER_IF"] = true,
                ["INNER_IF"] = true
            };
            processor = SetupCStyleWithCommentsProcessor(vc);
            RunAndVerify(originalValue, outerTrueInnerTrueExpectedValue, processor, 9999);
        }

        [Fact(DisplayName = nameof(VerifyQuadCommentRemovalOnlyNestedRemoves))]
        public void VerifyQuadCommentRemovalOnlyNestedRemoves()
        {
            string originalValue = @"Start
//#if (OUTER_IF)
    // Comment: outer if
    content outer if
    ////#if (INNER_IF)
        //// Comment: inner if
        //content: inner if
    //#endif
//#endif
// end comment
//// end quad comment
End";
            string outerTrueInnerFalseExpectedValue = @"Start
    // Comment: outer if
    content outer if
// end comment
//// end quad comment
End";
            VariableCollection vc = new VariableCollection
            {
                ["OUTER_IF"] = true,
                ["INNER_IF"] = false
            };
            IProcessor processor = SetupCStyleWithCommentsProcessor(vc);
            RunAndVerify(originalValue, outerTrueInnerFalseExpectedValue, processor, 9999);

            string outerTrueInnerTrueExpectedValue = @"Start
    // Comment: outer if
    content outer if
        // Comment: inner if
        content: inner if
// end comment
//// end quad comment
End";
            vc = new VariableCollection
            {
                ["OUTER_IF"] = true,
                ["INNER_IF"] = true
            };
            processor = SetupCStyleWithCommentsProcessor(vc);
            RunAndVerify(originalValue, outerTrueInnerTrueExpectedValue, processor, 9999);
        }
    }
}
