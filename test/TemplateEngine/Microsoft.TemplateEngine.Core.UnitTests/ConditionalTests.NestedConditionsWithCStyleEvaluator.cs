// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Core.Contracts;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public partial class ConditionalTests
    {
        /// <summary>
        /// Tests that the inner if-elseif-else with special tokens gets processed correctly.
        /// </summary>
        [Fact(DisplayName = nameof(VerifyOuterIfAndEmbeddedConditionals))]
        public void VerifyOuterIfAndEmbeddedConditionals()
        {
            string originalValue = @"Lead content
////#if (OUTER_IF)
//      outer if content
//      ////#if (INNER_IF)
//          // inner if content
//      ////#elseif (INNER_ELSEIF)
//          // inner elseif content
//      ////#else
//          // inner else content
//      //#endif
////#else
//      outer else content
//#endif
// commented trailing content
moar trailing content";

            // outer if & inner if get uncommented
            string expectedValue = @"Lead content
      outer if content
           inner if content
// commented trailing content
moar trailing content";

            VariableCollection vc = new VariableCollection
            {
                ["OUTER_IF"] = true,
                ["INNER_IF"] = true,
                ["INNER_ELSEIF"] = true
            };
            IProcessor processor = SetupCStyleWithCommentsProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);

            // outer if & inner elseif
            expectedValue = @"Lead content
      outer if content
           inner elseif content
// commented trailing content
moar trailing content";

            vc = new VariableCollection
            {
                ["OUTER_IF"] = true,
                ["INNER_IF"] = false,
                ["INNER_ELSEIF"] = true
            };
            processor = SetupCStyleWithCommentsProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);

            // outer if & inner else
            expectedValue = @"Lead content
      outer if content
           inner else content
// commented trailing content
moar trailing content";

            vc = new VariableCollection
            {
                ["OUTER_IF"] = true,
                ["INNER_IF"] = false,
                ["INNER_ELSEIF"] = false
            };
            processor = SetupCStyleWithCommentsProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);

            // outer else - nothing from the inner if should get processed
            expectedValue = @"Lead content
      outer else content
// commented trailing content
moar trailing content";

            vc = new VariableCollection
            {
                ["OUTER_IF"] = false,
                ["INNER_IF"] = true,   // irrelevant
                ["INNER_ELSEIF"] = true // ireelevant
            };
            processor = SetupCStyleWithCommentsProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact(DisplayName = nameof(VerifyThreeLevelEmbedding))]
        public void VerifyThreeLevelEmbedding()
        {
            string originalValue = @"Lead content
////#if (LEVEL_1_IF)
//    content: level-1 if
//    ////#if (LEVEL_2_IF)
//    //    content: level-2 if
//    //    ////#if (LEVEL_3_IF)
//    //    //    content: level-3 if
//    //    ////#elseif (LEVEL_3_ELSEIF)
//    //    //    content: level-3 elseif
//    //    ////#else
//    //    //    content: level-3 else
//    //    ////#endif
//    ////#elseif (LEVEL_2_ELSEIF)
//    //    content: level-2 elseif
//    ////#else
//    //    content: level-2 else
//    ////#endif
////#elseif true
//    content: level-1 elseif
////#else
//    content: level-1 else
//#endif
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
            IProcessor processor = SetupCStyleWithCommentsProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }
    }
}
