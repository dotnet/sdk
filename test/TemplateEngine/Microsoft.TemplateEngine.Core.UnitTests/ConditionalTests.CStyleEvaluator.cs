// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public partial class ConditionalTests
    {
        [TestMethod]
        public void VerifyIfEndifTrueCondition()
        {
            string value = @"Hello
    #if (VALUE)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection { ["VALUE"] = true };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfElseEndifTrueCondition()
        {
            string value = @"Hello
    #if (VALUE)
value
    #else
other
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection { ["VALUE"] = true };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfElseEndifTrueConditionContainsTabs()
        {
            string value = @"Hello
    #if " + "\t" + @" (VALUE)
value
    #else
other
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection { ["VALUE"] = true };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfElseEndifTrueConditionQuotedString()
        {
            string value = @"Hello
    #if (""Hello" + "\t" + @"There"" == VALUE)
value
    #else
other
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection { ["VALUE"] = "Hello\tThere" };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod(DisplayName = nameof(VerifyIfElseEndifTrueConditionQuotedString))]
        public void VerifyIfElseEndifTrueConditionUnquotedString()
        {
            string value = @"Hello
    #if (Foo == VALUE)
value
    #else
other
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();
            VariableCollection vc = new VariableCollection { ["VALUE"] = "Foo", ["Foo"] = "Foo" };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfElseEndifTrueConditionLiteralFirst()
        {
            string value = @"Hello
    #if (3 > VALUE)
value
    #else
other
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfElseEndifTrueConditionLiteralAgainst()
        {
            string value = @"Hello
    #if(3 > VALUE)
value
    #else
other
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfElseEndifTrueConditionAgainstIf()
        {
            string value = @"Hello
    #if(VALUE)
value
    #else
other
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection { ["VALUE"] = true };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfElseEndifFalseCondition()
        {
            string value = @"Hello
    #if VALUE
value
    #else
other
    #endif
There";
            string expected = @"Hello
other
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection { ["VALUE"] = false };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfElseifEndifTrueFalseCondition()
        {
            string value = @"Hello
    #if (VALUE)
value
    #elseif (VALUE2)
other
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = true,
                ["VALUE2"] = false
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfElifEndifTrueFalseCondition()
        {
            string value = @"Hello
    #if (VALUE)
value
    #elif (VALUE2)
other
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = true,
                ["VALUE2"] = false
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfElseifEndifTrueTrueCondition()
        {
            string value = @"Hello
    #if (VALUE)
value
    #elseif (VALUE2)
other
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = true,
                ["VALUE2"] = true
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfElifEndifTrueTrueCondition()
        {
            string value = @"Hello
    #if (VALUE)
value
    #elif (VALUE2)
other
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = true,
                ["VALUE2"] = true
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfElseifEndifFalseTrueCondition()
        {
            string value = @"Hello
    #if (VALUE)
value
    #elseif (VALUE2)
other
    #endif
There";
            string expected = @"Hello
other
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false,
                ["VALUE2"] = true
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfElseifElseEndifTrueFalseCondition()
        {
            string value = @"Hello
    #if (VALUE)
value
    #elseif (VALUE2)
other
    #else
other2
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = true,
                ["VALUE2"] = false
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfElseifElseEndifFalseTrueCondition()
        {
            string value = @"Hello
    #if (VALUE)
value
    #elseif (VALUE2)
other
    #else
other2
    #endif
There";
            string expected = @"Hello
other
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false,
                ["VALUE2"] = true
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfElseifElseEndifFalseFalseCondition()
        {
            string value = @"Hello
    #if VALUE
value
    #elseif VALUE2
other
    #else
other2
    #endif
There";
            string expected = @"Hello
other2
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false,
                ["VALUE2"] = false
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyNestedIfTrueTrue()
        {
            string value = @"Hello
    #if (VALUE)
        #if (VALUE2)
value
        #else
other
        #endif
    #else
other2
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = true,
                ["VALUE2"] = true
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfElseifElseifElseEndifTrueTrueCondition()
        {
            string value = @"Hello
        #if (VALUE)
value
        #elseif (VALUE2)
other
        #else
other2
        #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = true,
                ["VALUE2"] = true
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfElseifElseifElseEndifTrueFalseCondition()
        {
            string value = @"Hello
        #if (VALUE)
value
        #elseif (VALUE2)
other
        #else
other2
        #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = true,
                ["VALUE2"] = false
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfElseifElseifElseEndifFalseTrueCondition()
        {
            string value = @"Hello
        #if (VALUE)
value
        #elseif (VALUE2)
other
        #else
other2
        #endif
There";
            string expected = @"Hello
other
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false,
                ["VALUE2"] = true
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfElseifElseifElseEndifFalseFalseCondition()
        {
            string value = @"Hello
        #if VALUE
value
        #elseif VALUE2
other
        #else
other2
        #endif
There";
            string expected = @"Hello
other2
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false,
                ["VALUE2"] = false
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfElseifElseifEndifTrueFalseFalseCondition()
        {
            string value = @"Hello
    #if (VALUE)
value
    #elseif (VALUE2)
other
    #elseif (VALUE3)
other2
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = true,
                ["VALUE2"] = false,
                ["VALUE3"] = false
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfElseifElseifEndifFalseTrueFalseCondition()
        {
            string value = @"Hello
    #if (VALUE)
value
    #elseif (VALUE2)
other
    #elseif (VALUE3)
other2
    #endif
There";
            string expected = @"Hello
other
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false,
                ["VALUE2"] = true,
                ["VALUE3"] = false
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfElseifElseifEndifFalseFalseTrueCondition()
        {
            string value = @"Hello
    #if (VALUE)
value
    #elseif (VALUE2)
other
    #elseif (VALUE3)
other2
    #endif
There";
            string expected = @"Hello
other2
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false,
                ["VALUE2"] = false,
                ["VALUE3"] = true
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfEndifTrueEqualsCondition()
        {
            string value = @"Hello
    #if (VALUE == 2)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2L
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfEndifTrueNotEqualsCondition()
        {
            string value = @"Hello
    #if (VALUE != 3)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 4
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfEndifTrueGreaterThanCondition()
        {
            string value = @"Hello
    #if (VALUE > 3)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 4
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfEndifOperandStealing()
        {
            string value = @"Hello
    #if ((VALUE == 3) == true)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 3L
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfEndifOperandStealing2()
        {
            string value = @"Hello
    #if (!VALUE == true)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfEndifTrueGreaterThanOrEqualToCondition()
        {
            string value = @"Hello
    #if (VALUE >= 3)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 3
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfEndifFalseGreaterThanOrEqualToCondition()
        {
            string value = @"Hello
    #if (VALUE >= 3)
value
    #endif
There";
            string expected = @"Hello
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfEndifTrueLessThanCondition()
        {
            string value = @"Hello
    #if (VALUE < 3)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfEndifTrueLessThanOrEqualToCondition()
        {
            string value = @"Hello
    #if (VALUE <= 3)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 3
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfEndifTrueNotCondition()
        {
            string value = @"Hello
    #if (!VALUE)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = false
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfEndifTrueNotNotCondition()
        {
            string value = @"Hello
    #if (!!VALUE)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = true
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfEndifTrueAndCondition()
        {
            string value = @"Hello
    #if (VALUE < 3 && VALUE > 0)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfEndifTrueXorCondition()
        {
            string value = @"Hello
    #if (VALUE < 3 ^ VALUE == 7)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfEndifTrueAndAndCondition()
        {
            string value = @"Hello
    #if (VALUE < 3 && VALUE < 4 && VALUE < 5)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfEndifTrueOrCondition()
        {
            string value = @"Hello
    #if (VALUE == 6 || VALUE < 3)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfEndifTrueOrOrCondition()
        {
            string value = @"Hello
    #if (VALUE == 6 || VALUE == 7 || VALUE < 3)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfEndifTrueOrAndCondition()
        {
            string value = @"Hello
    #if (VALUE == 6 || (VALUE != 7 && VALUE < 3))
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfEndifTrueAndOrCondition()
        {
            string value = @"Hello
    #if ((VALUE != 7 && VALUE < 3) || VALUE == 6)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfEndifTrueBitwiseAndEqualsCondition()
        {
            string value = @"Hello
    #if ((VALUE & 0xFFFF) == 2)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfEndifTrueBitwiseOrEqualsCondition()
        {
            string value = @"Hello
    #if (VALUE | 0xFFFD == 0xFFFF)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfEndifTrueShlCondition()
        {
            string value = @"Hello
    #if (VALUE << 1 == 8)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 4
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfEndifTrueShrCondition()
        {
            string value = @"Hello
    #if (VALUE >> 1 == 2)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 4
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfEndifTrueGroupedCondition()
        {
            string value = @"Hello
    #if ((VALUE == 2) && (VALUE2 == 3))
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection
            {
                ["VALUE"] = 2L,
                ["VALUE2"] = 3L
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfElseEndifConditionUsesNull()
        {
            string value = @"Hello
    #if (VALUE2 == null)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection();
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfElseEndifConditionUsesFalse()
        {
            string value = @"Hello
    #if (!false)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection();
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        [DataRow("", "Hello\r\n#if (1.2 < 2.5)\r\nvalue\r\n#endif\r\nThere", "Hello\r\nvalue\r\nThere")]
        [DataRow("invariant", "Hello\r\n#if (1.2 < 2.5)\r\nvalue\r\n#endif\r\nThere", "Hello\r\nvalue\r\nThere")]
        [DataRow("pl-PL", "Hello\r\n#if (1.2 < 2.5)\r\nvalue\r\n#endif\r\nThere", "Hello\r\nvalue\r\nThere")]
        [DataRow("ru-RU", "Hello\r\n#if (1.2 < 2.5)\r\nvalue\r\n#endif\r\nThere", "Hello\r\nvalue\r\nThere")]
        [DataRow("ru-RU", "Hello\r\n#if (2.5 < 25)\r\nvalue\r\n#endif\r\nThere", "Hello\r\nvalue\r\nThere")]
        [DataRow("tr-TR", "Hello\r\n#if (2.5 < 25)\r\nvalue\r\n#endif\r\nThere", "Hello\r\nvalue\r\nThere")]
        [DataRow("tr-TR", "Hello\r\n#if (2,5 < 25)\r\nvalue\r\n#endif\r\nThere", "Hello\r\nvalue\r\nThere")]
        [DataRow("en-US", "Hello\r\n#if (2.5 < 25)\r\nvalue\r\n#endif\r\nThere", "Hello\r\nvalue\r\nThere")]
        [DataRow("en-US", "Hello\r\n#if (2,5 < 3,5)\r\nvalue\r\n#endif\r\nThere", "Hello\r\nThere")]
        [DataRow("en-GB", "Hello\r\n#if (2.5 < 25)\r\nvalue\r\n#endif\r\nThere", "Hello\r\nvalue\r\nThere")]
        [DataRow("hr-HR", "Hello\r\n#if (2.5 < 25)\r\nvalue\r\n#endif\r\nThere", "Hello\r\nvalue\r\nThere")]
        [DataRow("hi-IN", "Hello\r\n#if (2.5 < 25)\r\nvalue\r\n#endif\r\nThere", "Hello\r\nvalue\r\nThere")]
        [DataRow("fr-CH", "Hello\r\n#if (2.5 < 25)\r\nvalue\r\n#endif\r\nThere", "Hello\r\nvalue\r\nThere")]
        [DataRow("zh-CN", "Hello\r\n#if (2.5 < 25)\r\nvalue\r\n#endif\r\nThere", "Hello\r\nvalue\r\nThere")]
        [DataRow("zh-SG", "Hello\r\n#if (2.5 < 25)\r\nvalue\r\n#endif\r\nThere", "Hello\r\nvalue\r\nThere")]
        [DataRow("zh-TW", "Hello\r\n#if (2.5 < 25)\r\nvalue\r\n#endif\r\nThere", "Hello\r\nvalue\r\nThere")]
        [DataRow("zh-CHS", "Hello\r\n#if (2.5 < 25)\r\nvalue\r\n#endif\r\nThere", "Hello\r\nvalue\r\nThere")]
        [DataRow("zh-CHT", "Hello\r\n#if (2.5 < 25)\r\nvalue\r\n#endif\r\nThere", "Hello\r\nvalue\r\nThere")]
        public void VerifyIfElseEndifConditionUsesDouble(string culture, string value, string expected)
        {
            if (!string.IsNullOrEmpty(culture))
            {
                CultureInfo.CurrentCulture = culture == "invariant" ? CultureInfo.InvariantCulture : new CultureInfo(culture);
            }

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection();
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfElseEndifConditionUsesFalsePositiveHex()
        {
            string value = @"Hello
    #if (0xChicken == null)
value
    #endif
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection();
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyIfNoCondition()
        {
            string value = @"Hello
    #if
value
    #endif
There";
            string expected = @"Hello
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection();
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyConditionAtEnd()
        {
            string value = @"Hello
    #if (1.2 < 2.5)";
            string expected = @"Hello
";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection();
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyExcludeNestedCondition()
        {
            string value = @"Hello
    #if false
        #if true
            #if true
            #endif
        #endif
        #if true
            #if true
            #endif
        #endif
    #endif";
            string expected = @"Hello
";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection();
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyExcludeNestedConditionInNonTakenBranch()
        {
            string value = @"Hello
    #if true
    #else
        #if true
            #if true
            #endif
        #endif
        #if true
            #if true
            #endif
        #endif
    #endif
There";
            string expected = @"Hello
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection();
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            bool changed = processor.Run(input, output, 28);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void VerifyEmitStrayToken()
        {
            string value = @"Hello
    #endif
    #else
    #elseif foo";
            string expected = @"Hello
    #endif
    #else
    #elseif foo";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection();
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);

            //Changes should be made
            processor.Run(input, output, 28);
            //Override the change indication - the stream was technically mutated in this case,
            //  pretend it's false because the inputs and outputs are the same
            Verify(Encoding.UTF8, output, false, value, expected);
        }

        [TestMethod]
        [DataRow("foo", true)]
        [DataRow("foo", false)]
        [DataRow("def", true)]
        [DataRow("def", false)]
        // dotnet new crashes if template contains #ifdef
        // https://github.com/dotnet/templating/issues/3085
        public void VerifyMisstypedIfTokenDoesntCrash(string varName, bool varValue)
        {
            string value = @"#ifdef
GAGA
#endif";
            string expected = varName == "def" && varValue ? @"GAGA
" : string.Empty;

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            VariableCollection vc = new VariableCollection()
            {
                [varName] = varValue
            };
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);
            processor.Run(input, output, 999);
            Verify(Encoding.UTF8, output, true, value, expected);
        }

        [TestMethod]
        public void VerifyTheScopeAfterLongConditionIsNotLost()
        {
            //the buffer size is selected in the way so after processing the condition, the buffer is in the end and should be read
            //for trie, the last know position is 4 and it misses the buffer window after condition processing.
            string value = """
                #ifdef false
                Long text, long text, long text, long text, long text, long text, long text, long text, long text, long text, long text, long text, long tex
                #endif
                Long test after condition, Long test after condition,Long test after condition, Long test after condition,Long test after condition
                """;
            string expected = "Long test after condition, Long test after condition,Long test after condition, Long test after condition,Long test after condition";
            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            using MemoryStream input = new(valueBytes);
            using MemoryStream output = new();

            VariableCollection vc = new();
            IProcessor processor = SetupCStyleNoCommentsProcessor(vc);
            processor.Run(input, output, 20);
            Verify(Encoding.UTF8, output, true, value, expected);
        }
    }
}
