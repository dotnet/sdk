// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Tools.Formatters;
using Xunit;

namespace Microsoft.CodeAnalysis.Tools.Tests.Formatters
{
    public class EOFNewLineFormatterTests : CSharpFormatterTests
    {
        private protected override ICodeFormatter Formatter => new EndOfFileNewLineFormatter();

        [Fact]
        public async Task WhenFinalNewLineUnspecified_AndFinalNewLineMissing_NoChange()
        {
            var testCode = @"
class C
{
}";

            var expectedCode = @"
class C
{
}";

            var editorConfig = new Dictionary<string, string>()
            {
                ["end_of_line"] = "crlf",
            };

            await TestAsync(testCode, expectedCode, Formatter, editorConfig);
        }

        [Fact]
        public async Task WhenFinalNewLineUnspecified_AndFinalNewLineExits_NoChange()
        {
            var testCode = @"
class C
{
}
";

            var expectedCode = @"
class C
{
}
";

            var editorConfig = new Dictionary<string, string>()
            {
                ["end_of_line"] = "crlf",
            };

            await TestAsync(testCode, expectedCode, Formatter, editorConfig);
        }

        [Fact]
        public async Task WhenFinalNewLineRequired_AndEndOfLineIsLineFeed_LineFeedAdded()
        {
            var testCode = "class C\n{\n}";

            var expectedCode = "class C\n{\n}\n";

            var editorConfig = new Dictionary<string, string>()
            {
                ["insert_final_newline"] = "true",
                ["end_of_line"] = "lf",
            };

            await TestAsync(testCode, expectedCode, Formatter, editorConfig);
        }

        [Fact]
        public async Task WhenFinalNewLineRequired_AndEndOfLineIsCarriageReturnLineFeed_CarriageReturnLineFeedAdded()
        {
            var testCode = "class C\r\n{\r\n}";

            var expectedCode = "class C\r\n{\r\n}\r\n";

            var editorConfig = new Dictionary<string, string>()
            {
                ["insert_final_newline"] = "true",
                ["end_of_line"] = "crlf",
            };

            await TestAsync(testCode, expectedCode, Formatter, editorConfig);
        }

        [Fact]
        public async Task WhenFinalNewLineRequired_AndEndOfLineIsCarriageReturn_CarriageReturnAdded()
        {
            var testCode = "class C\r{\r}";

            var expectedCode = "class C\r{\r}\r";

            var editorConfig = new Dictionary<string, string>()
            {
                ["insert_final_newline"] = "true",
                ["end_of_line"] = "cr",
            };

            await TestAsync(testCode, expectedCode, Formatter, editorConfig);
        }
        [Fact]
        public async Task WhenFinalNewLineRequired_AndFinalNewLineExits_NoChange()
        {
            var testCode = @"
class C
{
}
";

            var expectedCode = @"
class C
{
}
";

            var editorConfig = new Dictionary<string, string>()
            {
                ["insert_final_newline"] = "true",
                ["end_of_line"] = "crlf",
            };

            await TestAsync(testCode, expectedCode, Formatter, editorConfig);
        }

        [Fact]
        public async Task WhenFinalNewLineUnwanted_AndFinalNewLineExists_CarriageReturnLineFeedRemoved()
        {
            var testCode = "class C\r\n{\r\n}\r\n\r\n\r\n";

            var expectedCode = "class C\r\n{\r\n}";

            var editorConfig = new Dictionary<string, string>()
            {
                ["insert_final_newline"] = "false",
                ["end_of_line"] = "crlf",
            };

            await TestAsync(testCode, expectedCode, Formatter, editorConfig);
        }

        [Fact]
        public async Task WhenFinalNewLineUnwanted_AndFinalNewLineExists_LineFeedRemoved()
        {
            var testCode = "class C\n{\n}\n\n\n";

            var expectedCode = "class C\n{\n}";

            var editorConfig = new Dictionary<string, string>()
            {
                ["insert_final_newline"] = "false",
                ["end_of_line"] = "crlf",
            };

            await TestAsync(testCode, expectedCode, Formatter, editorConfig);
        }

        [Fact]
        public async Task WhenFinalNewLineUnwanted_AndFinalNewLineExists_CarriageReturnRemoved()
        {
            var testCode = "class C\r{\r}\r\r\r";

            var expectedCode = "class C\r{\r}";

            var editorConfig = new Dictionary<string, string>()
            {
                ["insert_final_newline"] = "false",
                ["end_of_line"] = "crlf",
            };

            await TestAsync(testCode, expectedCode, Formatter, editorConfig);
        }

        [Fact]
        public async Task WhenFinalNewLineUnwanted_AndFinalNewLineMissing_NoChange()
        {
            var testCode = @"
class C
{
}";

            var expectedCode = @"
class C
{
}";

            var editorConfig = new Dictionary<string, string>()
            {
                ["insert_final_newline"] = "false",
                ["end_of_line"] = "crlf",
            };

            await TestAsync(testCode, expectedCode, Formatter, editorConfig);
        }

        [Fact]
        public async Task WhenFinalNewLineUnwanted_AndFileIsEmpty_NoChange()
        {
            var testCode = @"";

            var expectedCode = @"";

            var editorConfig = new Dictionary<string, string>()
            {
                ["insert_final_newline"] = "false",
                ["end_of_line"] = "crlf",
            };

            await TestAsync(testCode, expectedCode, Formatter, editorConfig);
        }
    }
}
