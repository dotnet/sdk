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
        [Fact(DisplayName = nameof(VbVerifyOuterIfAndEmbeddedConditionals))]
        public void VbVerifyOuterIfAndEmbeddedConditionals()
        {
            const string originalValue = @"Lead content
#If (OUTER_IF) Then
      outer if content
      #If (INNER_IF) Then
           inner if content
      #ElseIf (INNER_ELSEIF) Then
           inner elseif content
      #Else
           inner else content
      #End If
#Else
      outer else content
#End If
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
            IProcessor processor = SetupVBStyleNoCommentsProcessor(vc);
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
            processor = SetupVBStyleNoCommentsProcessor(vc);
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
            processor = SetupVBStyleNoCommentsProcessor(vc);
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
            processor = SetupVBStyleNoCommentsProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }

        [Fact(DisplayName = nameof(VbVerifyThreeLevelEmbedding))]
        public void VbVerifyThreeLevelEmbedding()
        {
            const string originalValue = @"Lead content
#If (LEVEL_1_IF) Then
    content: level-1 if
    #If (LEVEL_2_IF) Then
        content: level-2 if
        #If (LEVEL_3_IF) Then
            content: level-3 if
        #ElseIf (LEVEL_3_ELSEIF) Then
            content: level-3 elseif
        #Else
            content: level-3 else
        #End If
    #ElseIf (LEVEL_2_ELSEIF) Then
        content: level-2 elseif
    #Else
        content: level-2 else
    #End If
#ElseIf true Then
    content: level-1 elseif
#Else
    content: level-1 else
#End If
// commented trailing content
moar trailing content";

            // outer if & inner if get uncommented
            const string expectedValue = @"Lead content
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
            IProcessor processor = SetupVBStyleNoCommentsProcessor(vc);
            RunAndVerify(originalValue, expectedValue, processor, 9999);
        }
    }
}
