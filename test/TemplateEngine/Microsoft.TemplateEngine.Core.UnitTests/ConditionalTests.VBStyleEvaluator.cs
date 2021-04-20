// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public partial class ConditionalTests
    {
        [Fact(DisplayName = nameof(VBVerifyIfEndifTrueCondition))]
        public void VBVerifyIfEndifTrueCondition()
        {
            string value = @"Hello
    #If (VALUE) Then
value
    #End If
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection { ["VALUE"] = true };
            IProcessor processor = SetupVBStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VBVerifyIfEndifFalseCondition))]
        public void VBVerifyIfEndifFalseCondition()
        {
            string value = @"Hello
    #If (VALUE) Then
value
    #End If
There";
            string expected = @"Hello
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection { ["VALUE"] = false };
            IProcessor processor = SetupVBStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VBVerifyIfEndifTrueAndFalseCondition))]
        public void VBVerifyIfEndifTrueAndFalseCondition()
        {
            string value = @"Hello
    #If (VALUE1 And Value2) Then
value
    #End If
There";
            string expected = @"Hello
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE1"] = true,
                ["VALUE2"] = false
            };
            IProcessor processor = SetupVBStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VBVerifyIfEndifNotNotTrueAndFalseCondition))]
        public void VBVerifyIfEndifNotNotTrueAndFalseCondition()
        {
            string value = @"Hello
    #If (Not (Not VALUE1 And VALUE2)) Then
value
    #End If
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE1"] = true,
                ["VALUE2"] = false
            };
            IProcessor processor = SetupVBStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VBVerifyIfEndifTrueAndAlsoTrueCondition))]
        public void VBVerifyIfEndifTrueAndAlsoTrueCondition()
        {
            string value = @"Hello
    #If (VALUE1 AndAlso VALUE2) Then
value
    #End If
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE1"] = true,
                ["VALUE2"] = true
            };
            IProcessor processor = SetupVBStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VBVerifyIfEndifTrueAndAlsoNotFalseCondition))]
        public void VBVerifyIfEndifTrueAndAlsoNotFalseCondition()
        {
            string value = @"Hello
    #If (VALUE1 AndAlso Not VALUE2) Then
value
    #End If
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE1"] = true,
                ["VALUE2"] = false
            };
            IProcessor processor = SetupVBStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VBVerifyIfEndifNotFalseAndNotFalseAndNotFalseCondition))]
        public void VBVerifyIfEndifNotFalseAndNotFalseAndNotFalseCondition()
        {
            string value = @"Hello
    #If (Not VALUE1 And Not VALUE2 And Not VALUE3) Then
value
    #End If
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE1"] = false,
                ["VALUE2"] = false,
                ["VALUE3"] = false
            };
            IProcessor processor = SetupVBStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VBVerifyIfEndifNotFalseAndNotFalseAndNotTrueCondition))]
        public void VBVerifyIfEndifNotFalseAndNotFalseAndNotTrueCondition()
        {
            string value = @"Hello
    #If (Not VALUE1 And Not VALUE2 And Not VALUE3) Then
value
    #End If
There";
            string expected = @"Hello
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE1"] = false,
                ["VALUE2"] = false,
                ["VALUE3"] = true
            };
            IProcessor processor = SetupVBStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VBVerifyIfEndifNotFalseOrTrueCondition))]
        public void VBVerifyIfEndifNotFalseOrTrueCondition()
        {
            string value = @"Hello
    #If (Not VALUE1 Or VALUE2) Then
value
    #End If
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE1"] = false,
                ["VALUE2"] = true
            };
            IProcessor processor = SetupVBStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VBVerifyIfEndifNotFalseOrElseTrueCondition))]
        public void VBVerifyIfEndifNotFalseOrElseTrueCondition()
        {
            string value = @"Hello
    #If (Not VALUE1 OrElse VALUE2) Then
value
    #End If
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE1"] = false,
                ["VALUE2"] = true
            };
            IProcessor processor = SetupVBStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VBVerifyIfEndifNotNotFalseAndNotFalseAndNotTrueCondition))]
        public void VBVerifyIfEndifNotNotFalseAndNotFalseAndNotTrueCondition()
        {
            string value = @"Hello
    #If (Not (Not VALUE1 And Not VALUE2 And Not VALUE3)) Then
value
    #End If
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE1"] = false,
                ["VALUE2"] = false,
                ["VALUE3"] = true
            };
            IProcessor processor = SetupVBStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VBVerifyIfEndifExponentiateEqualsCondition))]
        public void VBVerifyIfEndifExponentiateEqualsCondition()
        {
            string value = @"Hello
    #If (2^3 = 8) Then
value
    #End If
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE1"] = false,
                ["VALUE2"] = false,
                ["VALUE3"] = true
            };
            IProcessor processor = SetupVBStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(VBVerifyIfEndifNotExponentiateNotEqualsCondition))]
        public void VBVerifyIfEndifNotExponentiateNotEqualsCondition()
        {
            string value = @"Hello
    #If (Not (2^3 <> 8)) Then
value
    #End If
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE1"] = false,
                ["VALUE2"] = false,
                ["VALUE3"] = true
            };
            IProcessor processor = SetupVBStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }
    }
}
