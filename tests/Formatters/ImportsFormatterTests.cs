// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Tools.Formatters;
using Xunit;

namespace Microsoft.CodeAnalysis.Tools.Tests.Formatters
{
    public class ImportsFormatterTests : CSharpFormatterTests
    {
        private protected override ICodeFormatter Formatter => new ImportsFormatter();

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

            await TestAsync(testCode, expectedCode, editorConfig);
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

            await TestAsync(testCode, expectedCode, editorConfig);
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

            await TestAsync(testCode, expectedCode, editorConfig);
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

            await TestAsync(testCode, expectedCode, editorConfig);
        }
    }
}
