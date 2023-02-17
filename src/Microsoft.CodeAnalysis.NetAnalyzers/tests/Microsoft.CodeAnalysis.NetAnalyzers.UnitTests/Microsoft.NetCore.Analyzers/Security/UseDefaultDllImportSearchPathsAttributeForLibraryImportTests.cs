// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.UseDefaultDllImportSearchPathsAttribute,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    // All the test cases use user32.dll as an example,
    // however it is a commonly used system dll and will be influenced by Known Dlls mechanism,
    // which will ignore all the configuration about the search algorithm.
    // Fow now, this rule didn't take Known Dlls into consideration.
    // If it is needed in the future, we can recover this rule.
    public class UseDefaultDllImportSearchPathsAttributeWithTests
    {
        const string LibraryImportAttribute = @"
namespace System.Runtime.InteropServices
{
    [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple=false, Inherited=false)]
    public sealed class LibraryImportAttribute : Attribute
    {
        public LibraryImportAttribute(string libraryName) { }
    }
}
";

        // It will try to retrieve the MessageBox from user32.dll, which will be searched in a default order.
        [Fact]
        public async Task Test_LibraryImportAttribute_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.InteropServices;

class TestClass
{
    [LibraryImport(""user32.dll"")]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

    public void TestMethod()
    {
        MessageBox(new IntPtr(0), ""Hello World!"", ""Hello Dialog"", 0);
    }
}" + LibraryImportAttribute,
                GetCSharpResultAt(8, 30,
                    UseDefaultDllImportSearchPathsAttribute.UseDefaultDllImportSearchPathsAttributeRule, "MessageBox"));
        }

        [Fact]
        public async Task Test_DllInUpperCase_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.InteropServices;

class TestClass
{
    [LibraryImport(""user32.DLL"")]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

    public void TestMethod()
    {
        MessageBox(new IntPtr(0), ""Hello World!"", ""Hello Dialog"", 0);
    }
}" + LibraryImportAttribute,
                GetCSharpResultAt(8, 30,
                    UseDefaultDllImportSearchPathsAttribute.UseDefaultDllImportSearchPathsAttributeRule, "MessageBox"));
        }

        [Fact]
        public async Task Test_WithoutDllExtension_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.InteropServices;

class TestClass
{
    [LibraryImport(""user32"")]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

    public void TestMethod()
    {
        MessageBox(new IntPtr(0), ""Hello World!"", ""Hello Dialog"", 0);
    }
}" + LibraryImportAttribute,
                GetCSharpResultAt(8, 30,
                    UseDefaultDllImportSearchPathsAttribute.UseDefaultDllImportSearchPathsAttributeRule, "MessageBox"));
        }

        [Fact]
        public async Task Test_DllImportSearchPathAssemblyDirectory_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.InteropServices;

class TestClass
{
    [LibraryImport(""user32.dll"")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

    public void TestMethod()
    {
        MessageBox(new IntPtr(0), ""Hello World!"", ""Hello Dialog"", 0);
    }
}" + LibraryImportAttribute,
                GetCSharpResultAt(9, 30, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule,
                    "AssemblyDirectory"));
        }

        [Fact]
        public async Task Test_UnsafeDllImportSearchPathBits_BitwiseCombination_OneValueIsBad_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.InteropServices;

class TestClass
{
    [LibraryImport(""user32.dll"")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.UserDirectories)]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

    public void TestMethod()
    {
        MessageBox(new IntPtr(0), ""Hello World!"", ""Hello Dialog"", 0);
    }
}" + LibraryImportAttribute,
                GetCSharpResultAt(9, 30, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule,
                    "AssemblyDirectory"));
        }

        [Fact]
        public async Task Test_UnsafeDllImportSearchPathBits_BitwiseCombination_BothIsBad_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.InteropServices;

class TestClass
{
    [LibraryImport(""user32.dll"")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

    public void TestMethod()
    {
        MessageBox(new IntPtr(0), ""Hello World!"", ""Hello Dialog"", 0);
    }
}" + LibraryImportAttribute,
                GetCSharpResultAt(9, 30, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule,
                    "AssemblyDirectory, ApplicationDirectory"));
        }

        [Fact]
        public async Task Test_DllImportSearchPathLegacyBehavior_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.InteropServices;

class TestClass
{
    [LibraryImport(""user32.dll"")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.LegacyBehavior)]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

    public void TestMethod()
    {
        MessageBox(new IntPtr(0), ""Hello World!"", ""Hello Dialog"", 0);
    }
}" + LibraryImportAttribute,
                GetCSharpResultAt(9, 30, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule,
                    "LegacyBehavior"));
        }

        [Fact]
        public async Task Test_DllImportSearchPathUseDllDirectoryForDependencies_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.InteropServices;

class TestClass
{
    [LibraryImport(""user32.dll"")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.UseDllDirectoryForDependencies)]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

    public void TestMethod()
    {
        MessageBox(new IntPtr(0), ""Hello World!"", ""Hello Dialog"", 0);
    }
}" + LibraryImportAttribute,
                GetCSharpResultAt(9, 30, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule,
                    "UseDllDirectoryForDependencies"));
        }

        [Fact]
        public async Task Test_DllImportSearchPathAssemblyDirectory_Assembly_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.InteropServices;

[assembly:DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]

class TestClass
{
    [LibraryImport(""user32.dll"")]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

    public void TestMethod()
    {
        MessageBox(new IntPtr(0), ""Hello World!"", ""Hello Dialog"", 0);
    }
}" + LibraryImportAttribute,
                GetCSharpResultAt(10, 30, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule,
                    "AssemblyDirectory"));
        }

        [Fact]
        public async Task Test_AssemblyDirectory_ApplicationDirectory_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.InteropServices;

[assembly:DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]

class TestClass
{
    [LibraryImport(""user32.dll"")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.ApplicationDirectory)]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

    public void TestMethod()
    {
        MessageBox(new IntPtr(0), ""Hello World!"", ""Hello Dialog"", 0);
    }
}" + LibraryImportAttribute,
                GetCSharpResultAt(11, 30, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule,
                    "ApplicationDirectory"));
        }

        [Fact]
        public async Task Test_ApplicationDirectory_AssemblyDirectory_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.InteropServices;

[assembly:DefaultDllImportSearchPaths(DllImportSearchPath.ApplicationDirectory)]

class TestClass
{
    [LibraryImport(""user32.dll"")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

    public void TestMethod()
    {
        MessageBox(new IntPtr(0), ""Hello World!"", ""Hello Dialog"", 0);
    }
}" + LibraryImportAttribute,
                GetCSharpResultAt(11, 30, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule,
                    "AssemblyDirectory"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("dotnet_code_quality.CA5393.unsafe_DllImportSearchPath_bits = 2 | 256 | 512")]
        [InlineData("dotnet_code_quality.CA5393.unsafe_DllImportSearchPath_bits = 770")]
        public async Task EditorConfigConfiguration_UnsafeDllImportSearchPathBits_DefaultValue_DiagnosticAsync(
            string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;
using System.Runtime.InteropServices;

class TestClass
{
    [LibraryImport(""user32.dll"")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

    public void TestMethod()
    {
        MessageBox(new IntPtr(0), ""Hello World!"", ""Hello Dialog"", 0);
    }
}" + LibraryImportAttribute
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(9, 30,
                            UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule,
                            "AssemblyDirectory, ApplicationDirectory"),
                    },
                    AnalyzerConfigFiles =
                    {
                        ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
")
                    }
                },
            }.RunAsync();
        }

        [Theory]
        [InlineData("dotnet_code_quality.CA5393.unsafe_DllImportSearchPath_bits = 2048")]
        public async Task EditorConfigConfiguration_UnsafeDllImportSearchPathBits_NonDefaultValue_DiagnosticAsync(
            string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;
using System.Runtime.InteropServices;

class TestClass
{
    [LibraryImport(""user32.dll"")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

    public void TestMethod()
    {
        MessageBox(new IntPtr(0), ""Hello World!"", ""Hello Dialog"", 0);
    }
}" + LibraryImportAttribute
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(9, 30,
                            UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule,
                            "System32"),
                    },
                    AnalyzerConfigFiles =
                    {
                        ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
")
                    }
                },
            }.RunAsync();
        }

        [Theory]
        [InlineData("dotnet_code_quality.CA5393.unsafe_DllImportSearchPath_bits = 1026")]
        public async Task EditorConfigConfiguration_UnsafeDllImportSearchPathBits_BitwiseCombination_DiagnosticAsync(
            string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;
using System.Runtime.InteropServices;

class TestClass
{
    [LibraryImport(""user32.dll"")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.UserDirectories)]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

    public void TestMethod()
    {
        MessageBox(new IntPtr(0), ""Hello World!"", ""Hello Dialog"", 0);
    }
}" + LibraryImportAttribute
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(9, 30,
                            UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule,
                            "UserDirectories"),
                    },
                    AnalyzerConfigFiles =
                    {
                        ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
")
                    }
                },
            }.RunAsync();
        }

        [Fact]
        public async Task Test_NoAttribute_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.InteropServices;

class TestClass
{
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

    public void TestMethod()
    {
        MessageBox(new IntPtr(0), ""Hello World!"", ""Hello Dialog"", 0);
    }
}" + LibraryImportAttribute);
        }

        // user32.dll will be searched in UserDirectories, which is specified by DllImportSearchPath and is good.
        [Fact]
        public async Task Test_LibraryImportAndDefaultDllImportSearchPathsAttributes_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.InteropServices;

class TestClass
{
    [LibraryImport(""user32.dll"")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.UserDirectories)]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

    public void TestMethod()
    {
        MessageBox(new IntPtr(0), ""Hello World!"", ""Hello Dialog"", 0);
    }
}" + LibraryImportAttribute);
        }

        [Theory]
        [InlineData("dotnet_code_quality.CA5392.unsafe_DllImportSearchPath_bits = 2 | 1024")]
        [InlineData(
            "dotnet_code_quality.CA5392.unsafe_DllImportSearchPath_bits = DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.UserDirectories")]
        public async Task EditorConfigConfiguration_UnsafeDllImportSearchPathBits_BitwiseCombination_NoDiagnosticAsync(
            string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;
using System.Runtime.InteropServices;

class TestClass
{
    [LibraryImport(""user32.dll"")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.UserDirectories)]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

    public void TestMethod()
    {
        MessageBox(new IntPtr(0), ""Hello World!"", ""Hello Dialog"", 0);
    }
}" + LibraryImportAttribute
                    },
                    AnalyzerConfigFiles =
                    {
                        ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
")
                    }
                },
            }.RunAsync();
        }

        [Theory]
        [InlineData("dotnet_code_quality.CA5393.unsafe_DllImportSearchPath_bits = 2048")]
        public async Task EditorConfigConfiguration_UnsafeDllImportSearchPathBits_NonDefaultValue_NoDiagnosticAsync(
            string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;
using System.Runtime.InteropServices;

class TestClass
{
    [LibraryImport(""user32.dll"")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

    public void TestMethod()
    {
        MessageBox(new IntPtr(0), ""Hello World!"", ""Hello Dialog"", 0);
    }
}" + LibraryImportAttribute
                    },
                    AnalyzerConfigFiles =
                    {
                        ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
")
                    }
                },
            }.RunAsync();
        }

        // In this case, [DefaultDllImportSearchPaths] is applied to the assembly.
        // So, this attribute specifies the paths that are used by default to search for any DLL that provides a function for a platform invoke, in any code in the assembly.
        [Fact]
        public async Task Test_LibraryImportAndAssemblyDefaultDllImportSearchPathsAttributes_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.InteropServices;

[assembly:DefaultDllImportSearchPaths(DllImportSearchPath.UserDirectories)]

class TestClass
{
    [LibraryImport(""user32.dll"")]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

    public void TestMethod()
    {
        MessageBox(new IntPtr(0), ""Hello World!"", ""Hello Dialog"", 0);
    }
}" + LibraryImportAttribute);
        }

        // It will have a compiler warning and recommend to use [LibraryImport] also.
        [Fact]
        public async Task Test_DefaultDllImportSearchPaths_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.InteropServices;

class TestClass
{
    [DefaultDllImportSearchPaths(DllImportSearchPath.UserDirectories)]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

    public void TestMethod()
    {
    }
}" + LibraryImportAttribute);
        }

        // It will have a compiler warning and recommend to use [LibraryImport] also.
        [Fact]
        public async Task Test_AssemblyDefaultDllImportSearchPaths_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.InteropServices;

[assembly:DefaultDllImportSearchPaths(DllImportSearchPath.UserDirectories)]

class TestClass
{
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

    public void TestMethod()
    {
    }
}" + LibraryImportAttribute);
        }

        // Local methods with LibraryImport and no DllImportSearchPaths should warn
        [Fact]
        public async Task Test_LocalMethodWithoutSearchPathsWarns()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;
using System.Runtime.InteropServices;


class TestClass
{

    public void TestMethod()
    {
        var x = MessageBox((IntPtr)null, ""asdf"", ""asdf"", 0);
        return;

        [LibraryImport(""user32.dll"")]
        static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);
    }
}" + LibraryImportAttribute
                    },
                    // // Bug - Should warn on the local method
                    //ExpectedDiagnostics =
                    //{
                    //    GetCSharpResultAt(9, 30,
                    //        UseDefaultDllImportSearchPathsAttribute.UseDefaultDllImportSearchPathsAttributeRule,
                    //        "UserDirectories"),
                    //},
                },
                LanguageVersion = LanguageVersion.CSharp9,
            }.RunAsync();
        }

        [Fact]
        public async Task Test_DllImportAndLibraryImportWarnsOnLibraryImport()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;
using System.Runtime.InteropServices;

internal partial class TestClass
{
    [LibraryImport(""user32.dll"")]
    public static partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);
}
internal partial class TestClass
{
    [DllImport(""user32.dll"")]
    public static extern partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);
}" + LibraryImportAttribute
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(8, 31,
                            UseDefaultDllImportSearchPathsAttribute.UseDefaultDllImportSearchPathsAttributeRule, "MessageBox"),
                    },
                },
                LanguageVersion = LanguageVersion.CSharp9,
            }.RunAsync();
        }

        // [LibraryImport] is set with an absolute path, which will let the [DefaultDllImportSearchPaths] be ignored.
        [WindowsOnlyFact]
        public async Task Test_LibraryImportAttributeWithAbsolutePath_DefaultDllImportSearchPaths_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.InteropServices;

class TestClass
{
    [LibraryImport(""C:\\Windows\\System32\\user32.dll"")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.UserDirectories)]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

    public void TestMethod()
    {
        MessageBox(new IntPtr(0), ""Hello World!"", ""Hello Dialog"", 0);
    }
}" + LibraryImportAttribute);
        }

        // [LibraryImport] is set with an absolute path.
        [WindowsOnlyFact]
        public async Task Test_LibraryImportAttributeWithAbsolutePath_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.InteropServices;

class TestClass
{
    [LibraryImport(""C:\\Windows\\System32\\user32.dll"")]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

    public void TestMethod()
    {
        MessageBox(new IntPtr(0), ""Hello World!"", ""Hello Dialog"", 0);
    }
}" + LibraryImportAttribute);
        }

        [WindowsOnlyFact]
        public async Task Test_UsingNonexistentAbsolutePath_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.InteropServices;

class TestClass
{
    [LibraryImport(""C:\\Nonexistent\\user32.dll"")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.UserDirectories)]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

    public void TestMethod()
    {
        MessageBox(new IntPtr(0), ""Hello World!"", ""Hello Dialog"", 0);
    }
}" + LibraryImportAttribute);
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, DiagnosticDescriptor rule,
            params string[] arguments)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyCS.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arguments);
    }
}