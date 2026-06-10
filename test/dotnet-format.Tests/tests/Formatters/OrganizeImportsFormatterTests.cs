// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Tools.Formatters;

namespace Microsoft.CodeAnalysis.Tools.Tests.Formatters
{
    public class OrganizeImportsFormatterTests : CSharpFormatterTests
    {
        private protected override ICodeFormatter Formatter => new OrganizeImportsFormatter();

        public OrganizeImportsFormatterTests(ITestOutputHelper output)
        {
            TestOutputHelper = output;
        }

        [Fact]
        public async Task WhenOptionsDisabled_AndImportsNotSorted_ImportsSorted()
        {
            var testCode = @"
using Microsoft.CodeAnalysis;
using System.Linq;
using System;

class C
{
}";

            var expectedCode = @"
using Microsoft.CodeAnalysis;
using System;
using System.Linq;

class C
{
}";

            var editorConfig = new Dictionary<string, string>()
            {
                ["end_of_line"] = EndOfLineFormatter.GetEndOfLineOption(Environment.NewLine),
                ["dotnet_sort_system_directives_first"] = "false",
                ["dotnet_separate_import_directive_groups"] = "false"
            };

            await AssertCodeChangedAsync(testCode, expectedCode, editorConfig);
        }

        [Fact]
        public async Task WhenSystemDirectivesFirst_AndImportsNotSorted_ImportsSorted()
        {
            var testCode = @"
using Microsoft.CodeAnalysis;
using System.Linq;
using System;

class C
{
}";

            var expectedCode = @"
using System;
using System.Linq;
using Microsoft.CodeAnalysis;

class C
{
}";

            var editorConfig = new Dictionary<string, string>()
            {
                ["end_of_line"] = EndOfLineFormatter.GetEndOfLineOption(Environment.NewLine),
                ["dotnet_sort_system_directives_first"] = "true",
                ["dotnet_separate_import_directive_groups"] = "false"
            };

            await AssertCodeChangedAsync(testCode, expectedCode, editorConfig);
        }

        [Fact]
        public async Task WhenImportGroupsSeparated_AndImportsNotSeparated_ImportsSeparated()
        {
            var testCode = @"
using Microsoft.CodeAnalysis;
using System.Linq;
using System;

class C
{
}";

            var expectedCode = @"
using Microsoft.CodeAnalysis;

using System;
using System.Linq;

class C
{
}";

            var editorConfig = new Dictionary<string, string>()
            {
                ["end_of_line"] = EndOfLineFormatter.GetEndOfLineOption(Environment.NewLine),
                ["dotnet_sort_system_directives_first"] = "false",
                ["dotnet_separate_import_directive_groups"] = "true"
            };

            await AssertCodeChangedAsync(testCode, expectedCode, editorConfig);
        }

        [Fact]
        public async Task WhenBothOptionsEnabled_AndImportsNotSortedOrSeparated_ImportsSortedAndSeparated()
        {
            var testCode = @"
using Microsoft.CodeAnalysis;
using System.Linq;
using System;

class C
{
}";

            var expectedCode = @"
using System;
using System.Linq;

using Microsoft.CodeAnalysis;

class C
{
}";

            var editorConfig = new Dictionary<string, string>()
            {
                ["end_of_line"] = EndOfLineFormatter.GetEndOfLineOption(Environment.NewLine),
                ["dotnet_sort_system_directives_first"] = "true",
                ["dotnet_separate_import_directive_groups"] = "true"
            };

            await AssertCodeChangedAsync(testCode, expectedCode, editorConfig);
        }

        [Fact]
        public async Task WhenNeitherOptionIsConfigured_AndImportsNotSortedOrSeparated_NoChange()
        {
            var code = @"
using Microsoft.CodeAnalysis;
using System.Linq;
using System;

class C
{
}";

            var editorConfig = new Dictionary<string, string>()
            {
                ["end_of_line"] = EndOfLineFormatter.GetEndOfLineOption(Environment.NewLine)
            };

            await AssertCodeUnchangedAsync(code, editorConfig);
        }
    }
}
