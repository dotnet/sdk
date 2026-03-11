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
    public class UseDefaultDllImportSearchPathsAttributeWithLibraryImportTests
    {
        private async Task RunAnalyzerAsync(string source, string generatedSource, params DiagnosticResult[] diagnostics)
        {
            await RunAnalyzerWithConfigAsync(source, generatedSource, null, diagnostics);
        }
        private async Task RunAnalyzerWithConfigAsync(string source, string generatedSource, (string filename, string content)? editorConfig, params DiagnosticResult[] diagnostics)
        {
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = {source + generatedSource },
					//GeneratedSources = { ("Generated.cs", generatedSource) },
				},
                LanguageVersion = LanguageVersion.CSharp9,
            };
            test.ExpectedDiagnostics.AddRange(diagnostics);
            if (editorConfig is not null)
            {
                test.TestState.AnalyzerConfigFiles.Add(editorConfig.Value);
            }

            await test.RunAsync();
        }

        private const string LibraryImportAttribute = """

            namespace System.Runtime.InteropServices
            {
                [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple=false, Inherited=false)]
                public sealed class LibraryImportAttribute : Attribute
                {
                    public LibraryImportAttribute(string libraryName) { }
                }
            }

            """;

        private const string MessageBoxImplementation_NoDllImport = """

            partial class TestClass
            {
                public static partial int MessageBox(IntPtr hWnd, String text, String caption, uint type) => 0;
            }

            """;

        private const string MessageBoxImplementation_DllImport = """

            partial class TestClass
            {
            	[DllImport("user32.dll")]
                public static extern partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);
            }

            """;

        // It will try to retrieve the MessageBox from user32.dll, which will be searched in a default order.
        [Fact]
        public async Task Test_LibraryImportAttribute_DiagnosticAsync()
        {
            string source = """

                using System;
                using System.Runtime.InteropServices;

                partial class TestClass
                {
                    [LibraryImport("user32.dll")]
                    public static partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);

                    public void TestMethod()
                    {
                        MessageBox(new IntPtr(0), "Hello World!", "Hello Dialog", 0);
                    }
                }
                """ + LibraryImportAttribute;

            await RunAnalyzerAsync(source, MessageBoxImplementation_DllImport,
                GetCSharpResultAt(8, 31, UseDefaultDllImportSearchPathsAttribute.UseDefaultDllImportSearchPathsAttributeRule, "MessageBox"));

            await RunAnalyzerAsync(source, MessageBoxImplementation_NoDllImport,
                GetCSharpResultAt(8, 31, UseDefaultDllImportSearchPathsAttribute.UseDefaultDllImportSearchPathsAttributeRule, "MessageBox"));
        }

        [Fact]
        public async Task Test_DllInUpperCase_DiagnosticAsync()
        {
            var source = """

                using System;
                using System.Runtime.InteropServices;

                partial class TestClass
                {
                    [LibraryImport("user32.DLL")]
                    public static partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);

                    public void TestMethod()
                    {
                        MessageBox(new IntPtr(0), "Hello World!", "Hello Dialog", 0);
                    }
                }
                """ + LibraryImportAttribute;

            await RunAnalyzerAsync(source, MessageBoxImplementation_DllImport,
                GetCSharpResultAt(8, 31, UseDefaultDllImportSearchPathsAttribute.UseDefaultDllImportSearchPathsAttributeRule, "MessageBox"));

            await RunAnalyzerAsync(source, MessageBoxImplementation_NoDllImport,
                GetCSharpResultAt(8, 31, UseDefaultDllImportSearchPathsAttribute.UseDefaultDllImportSearchPathsAttributeRule, "MessageBox"));

        }

        [Fact]
        public async Task Test_WithoutDllExtension_DiagnosticAsync()
        {
            var source = """

                using System;
                using System.Runtime.InteropServices;

                partial class TestClass
                {
                    [LibraryImport("user32")]
                    public static partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);

                    public void TestMethod()
                    {
                        MessageBox(new IntPtr(0), "Hello World!", "Hello Dialog", 0);
                    }
                }
                """ + LibraryImportAttribute;

            await RunAnalyzerAsync(source, MessageBoxImplementation_DllImport,
                GetCSharpResultAt(8, 31, UseDefaultDllImportSearchPathsAttribute.UseDefaultDllImportSearchPathsAttributeRule, "MessageBox"));

            await RunAnalyzerAsync(source, MessageBoxImplementation_NoDllImport,
                GetCSharpResultAt(8, 31, UseDefaultDllImportSearchPathsAttribute.UseDefaultDllImportSearchPathsAttributeRule, "MessageBox"));
        }

        [Fact]
        public async Task Test_DllImportSearchPathAssemblyDirectory_DiagnosticAsync()
        {
            var source = """

                using System;
                using System.Runtime.InteropServices;

                partial class TestClass
                {
                    [LibraryImport("user32.dll")]
                    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]
                    public static partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);

                    public void TestMethod()
                    {
                        MessageBox(new IntPtr(0), "Hello World!", "Hello Dialog", 0);
                    }
                }
                """ + LibraryImportAttribute;

            await RunAnalyzerAsync(source, MessageBoxImplementation_DllImport,
                GetCSharpResultAt(9, 31, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule, "AssemblyDirectory"));

            await RunAnalyzerAsync(source, MessageBoxImplementation_NoDllImport,
                GetCSharpResultAt(9, 31, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule, "AssemblyDirectory"));
        }

        [Fact]
        public async Task Test_UnsafeDllImportSearchPathBits_BitwiseCombination_OneValueIsBad_DiagnosticAsync()
        {
            var source = """

                using System;
                using System.Runtime.InteropServices;

                partial class TestClass
                {
                    [LibraryImport("user32.dll")]
                    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.UserDirectories)]
                    public static partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);

                    public void TestMethod()
                    {
                        MessageBox(new IntPtr(0), "Hello World!", "Hello Dialog", 0);
                    }
                }
                """ + LibraryImportAttribute;

            await RunAnalyzerAsync(source, MessageBoxImplementation_DllImport, GetCSharpResultAt(9, 31, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule, "AssemblyDirectory"));
            await RunAnalyzerAsync(source, MessageBoxImplementation_NoDllImport, GetCSharpResultAt(9, 31, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule, "AssemblyDirectory"));
        }

        [Fact]
        public async Task Test_UnsafeDllImportSearchPathBits_BitwiseCombination_BothIsBad_DiagnosticAsync()
        {
            var source = """

                using System;
                using System.Runtime.InteropServices;

                partial class TestClass
                {
                    [LibraryImport("user32.dll")]
                    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
                    public static partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);

                    public void TestMethod()
                    {
                        MessageBox(new IntPtr(0), "Hello World!", "Hello Dialog", 0);
                    }
                }
                """ + LibraryImportAttribute;
            await RunAnalyzerAsync(source, MessageBoxImplementation_DllImport, GetCSharpResultAt(9, 31, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule,
                "AssemblyDirectory, ApplicationDirectory"));
            await RunAnalyzerAsync(source, MessageBoxImplementation_NoDllImport, GetCSharpResultAt(9, 31, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule,
                "AssemblyDirectory, ApplicationDirectory"));

        }

        [Fact]
        public async Task Test_DllImportSearchPathLegacyBehavior_DiagnosticAsync()
        {
            var source = """

                using System;
                using System.Runtime.InteropServices;

                partial class TestClass
                {
                    [LibraryImport("user32.dll")]
                    [DefaultDllImportSearchPaths(DllImportSearchPath.LegacyBehavior)]
                    public static partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);

                    public void TestMethod()
                    {
                        MessageBox(new IntPtr(0), "Hello World!", "Hello Dialog", 0);
                    }
                }
                """ + LibraryImportAttribute;

            await RunAnalyzerAsync(source, MessageBoxImplementation_DllImport, GetCSharpResultAt(9, 31, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule,
                    "LegacyBehavior"));
            await RunAnalyzerAsync(source, MessageBoxImplementation_NoDllImport, GetCSharpResultAt(9, 31, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule,
                    "LegacyBehavior"));

        }

        [Fact]
        public async Task Test_DllImportSearchPathUseDllDirectoryForDependencies_DiagnosticAsync()
        {
            var source = """

                using System;
                using System.Runtime.InteropServices;

                partial class TestClass
                {
                    [LibraryImport("user32.dll")]
                    [DefaultDllImportSearchPaths(DllImportSearchPath.UseDllDirectoryForDependencies)]
                    public static partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);

                    public void TestMethod()
                    {
                        MessageBox(new IntPtr(0), "Hello World!", "Hello Dialog", 0);
                    }
                }
                """ + LibraryImportAttribute;

            await RunAnalyzerAsync(source, MessageBoxImplementation_DllImport,
                GetCSharpResultAt(9, 31, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule, "UseDllDirectoryForDependencies"));

            await RunAnalyzerAsync(source, MessageBoxImplementation_NoDllImport,
                GetCSharpResultAt(9, 31, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule, "UseDllDirectoryForDependencies"));
        }

        [Fact]
        public async Task Test_DllImportSearchPathAssemblyDirectory_Assembly_DiagnosticAsync()
        {
            var source = """

                using System;
                using System.Runtime.InteropServices;

                [assembly:DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]

                partial class TestClass
                {
                    [LibraryImport("user32.dll")]
                    public static partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);

                    public void TestMethod()
                    {
                        MessageBox(new IntPtr(0), "Hello World!", "Hello Dialog", 0);
                    }
                }
                """ + LibraryImportAttribute;

            await RunAnalyzerAsync(source, MessageBoxImplementation_DllImport,
                GetCSharpResultAt(10, 31, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule, "AssemblyDirectory"));

            await RunAnalyzerAsync(source, MessageBoxImplementation_NoDllImport,
                GetCSharpResultAt(10, 31, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule, "AssemblyDirectory"));
        }

        [Fact]
        public async Task Test_AssemblyDirectory_ApplicationDirectory_DiagnosticAsync()
        {
            var source = """

                using System;
                using System.Runtime.InteropServices;

                [assembly:DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]

                partial class TestClass
                {
                    [LibraryImport("user32.dll")]
                    [DefaultDllImportSearchPaths(DllImportSearchPath.ApplicationDirectory)]
                    public static partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);

                    public void TestMethod()
                    {
                        MessageBox(new IntPtr(0), "Hello World!", "Hello Dialog", 0);
                    }
                }
                """ + LibraryImportAttribute;

            await RunAnalyzerAsync(source, MessageBoxImplementation_DllImport,
                GetCSharpResultAt(11, 31, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule, "ApplicationDirectory"));

            await RunAnalyzerAsync(source, MessageBoxImplementation_NoDllImport,
                GetCSharpResultAt(11, 31, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule, "ApplicationDirectory"));
        }

        [Fact]
        public async Task Test_ApplicationDirectory_AssemblyDirectory_DiagnosticAsync()
        {
            var source = """

                using System;
                using System.Runtime.InteropServices;

                [assembly:DefaultDllImportSearchPaths(DllImportSearchPath.ApplicationDirectory)]

                partial class TestClass
                {
                    [LibraryImport("user32.dll")]
                    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]
                    public static partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);

                    public void TestMethod()
                    {
                        MessageBox(new IntPtr(0), "Hello World!", "Hello Dialog", 0);
                    }
                }
                """ + LibraryImportAttribute;

            await RunAnalyzerAsync(source, MessageBoxImplementation_DllImport, GetCSharpResultAt(11, 31, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule,
                    "AssemblyDirectory"));
            await RunAnalyzerAsync(source, MessageBoxImplementation_NoDllImport, GetCSharpResultAt(11, 31, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule,
                    "AssemblyDirectory"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("dotnet_code_quality.CA5393.unsafe_DllImportSearchPath_bits = 2 | 256 | 512")]
        [InlineData("dotnet_code_quality.CA5393.unsafe_DllImportSearchPath_bits = 770")]
        public async Task EditorConfigConfiguration_UnsafeDllImportSearchPathBits_DefaultValue_DiagnosticAsync(
            string editorConfigText)
        {
            var source = """

                using System;
                using System.Runtime.InteropServices;

                partial class TestClass
                {
                    [LibraryImport("user32.dll")]
                    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
                    public static partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);

                    public void TestMethod()
                    {
                        MessageBox(new IntPtr(0), "Hello World!", "Hello Dialog", 0);
                    }
                }
                """ + LibraryImportAttribute;

            var config = ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
");
            await RunAnalyzerWithConfigAsync(source, MessageBoxImplementation_DllImport, config,
                GetCSharpResultAt(9, 31, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule, "AssemblyDirectory, ApplicationDirectory"));
            await RunAnalyzerWithConfigAsync(source, MessageBoxImplementation_NoDllImport, config,
                GetCSharpResultAt(9, 31, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule, "AssemblyDirectory, ApplicationDirectory"));
        }

        [Theory]
        [InlineData("dotnet_code_quality.CA5393.unsafe_DllImportSearchPath_bits = 2048")]
        public async Task EditorConfigConfiguration_UnsafeDllImportSearchPathBits_NonDefaultValue_DiagnosticAsync(
            string editorConfigText)
        {
            var source = """

                using System;
                using System.Runtime.InteropServices;

                partial class TestClass
                {
                    [LibraryImport("user32.dll")]
                    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
                    public static partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);

                    public void TestMethod()
                    {
                        MessageBox(new IntPtr(0), "Hello World!", "Hello Dialog", 0);
                    }
                }
                """ + LibraryImportAttribute;

            var config = ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
");
            await RunAnalyzerWithConfigAsync(source + MessageBoxImplementation_DllImport, "", config, GetCSharpResultAt(9, 31,
                    UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule, "System32"));
            await RunAnalyzerWithConfigAsync(source + MessageBoxImplementation_NoDllImport, "", config, GetCSharpResultAt(9, 31,
                    UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule, "System32"));
        }

        [Theory]
        [InlineData("dotnet_code_quality.CA5393.unsafe_DllImportSearchPath_bits = 1026")]
        public async Task EditorConfigConfiguration_UnsafeDllImportSearchPathBits_BitwiseCombination_DiagnosticAsync(
            string editorConfigText)
        {
            var source = """

                using System;
                using System.Runtime.InteropServices;

                partial class TestClass
                {
                    [LibraryImport("user32.dll")]
                    [DefaultDllImportSearchPaths(DllImportSearchPath.UserDirectories)]
                    public static partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);

                    public void TestMethod()
                    {
                        MessageBox(new IntPtr(0), "Hello World!", "Hello Dialog", 0);
                    }
                }
                """ + LibraryImportAttribute;

            var config = ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
");
            await RunAnalyzerWithConfigAsync(source, MessageBoxImplementation_DllImport, config,
                GetCSharpResultAt(9, 31, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule, "UserDirectories"));
            await RunAnalyzerWithConfigAsync(source, MessageBoxImplementation_NoDllImport, config,
                GetCSharpResultAt(9, 31, UseDefaultDllImportSearchPathsAttribute.DoNotUseUnsafeDllImportSearchPathRule, "UserDirectories"));
        }

        // user32.dll will be searched in UserDirectories, which is specified by DllImportSearchPath and is good.
        [Fact]
        public async Task Test_LibraryImportAndDefaultDllImportSearchPathsAttributes_NoDiagnosticAsync()
        {
            var source = """

                using System;
                using System.Runtime.InteropServices;

                partial class TestClass
                {
                    [LibraryImport("user32.dll")]
                    [DefaultDllImportSearchPaths(DllImportSearchPath.UserDirectories)]
                    public static partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);

                    public void TestMethod()
                    {
                        MessageBox(new IntPtr(0), "Hello World!", "Hello Dialog", 0);
                    }
                }
                """ + LibraryImportAttribute;

            await RunAnalyzerAsync(source, MessageBoxImplementation_DllImport);
            await RunAnalyzerAsync(source, MessageBoxImplementation_NoDllImport);
        }

        [Theory]
        [InlineData("dotnet_code_quality.CA5392.unsafe_DllImportSearchPath_bits = 2 | 1024")]
        [InlineData(
            "dotnet_code_quality.CA5392.unsafe_DllImportSearchPath_bits = DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.UserDirectories")]
        public async Task EditorConfigConfiguration_UnsafeDllImportSearchPathBits_BitwiseCombination_NoDiagnosticAsync(
            string editorConfigText)
        {
            var source = """

                using System;
                using System.Runtime.InteropServices;

                partial class TestClass
                {
                    [LibraryImport("user32.dll")]
                    [DefaultDllImportSearchPaths(DllImportSearchPath.UserDirectories)]
                    public static partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);

                    public void TestMethod()
                    {
                        MessageBox(new IntPtr(0), "Hello World!", "Hello Dialog", 0);
                    }
                }
                """ + LibraryImportAttribute;

            var config = ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
");
            await RunAnalyzerWithConfigAsync(source, MessageBoxImplementation_DllImport, config);
            await RunAnalyzerWithConfigAsync(source, MessageBoxImplementation_NoDllImport, config);
        }

        [Theory]
        [InlineData("dotnet_code_quality.CA5393.unsafe_DllImportSearchPath_bits = 2048")]
        public async Task EditorConfigConfiguration_UnsafeDllImportSearchPathBits_NonDefaultValue_NoDiagnosticAsync(
            string editorConfigText)
        {
            var source = """

                using System;
                using System.Runtime.InteropServices;

                partial class TestClass
                {
                    [LibraryImport("user32.dll")]
                    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
                    public static partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);

                    public void TestMethod()
                    {
                        MessageBox(new IntPtr(0), "Hello World!", "Hello Dialog", 0);
                    }
                }
                """ + LibraryImportAttribute;

            var config = ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
");
            await RunAnalyzerWithConfigAsync(source, MessageBoxImplementation_DllImport, config);
            await RunAnalyzerWithConfigAsync(source, MessageBoxImplementation_NoDllImport, config);
        }

        [Fact]
        public async Task Test_NoAttribute_NoDiagnosticAsync()
        {
            var source = """

                using System;
                using System.Runtime.InteropServices;

                partial class TestClass
                {
                    public static partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);

                    public void TestMethod()
                    {
                        MessageBox(new IntPtr(0), "Hello World!", "Hello Dialog", 0);
                    }
                }
                """ + LibraryImportAttribute;
            await RunAnalyzerAsync(source, MessageBoxImplementation_NoDllImport);
            // Doesn't make sense to run with a implementation with [DllImport]
        }

        // In this case, [DefaultDllImportSearchPaths] is applied to the assembly.
        // So, this attribute specifies the paths that are used by default to search for any DLL that provides a function for a platform invoke, in any code in the assembly.
        [Fact]
        public async Task Test_LibraryImportAndAssemblyDefaultDllImportSearchPathsAttributes_NoDiagnosticAsync()
        {
            var source = """

                using System;
                using System.Runtime.InteropServices;

                [assembly:DefaultDllImportSearchPaths(DllImportSearchPath.UserDirectories)]

                partial class TestClass
                {
                    [LibraryImport("user32.dll")]
                    public static partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);

                    public void TestMethod()
                    {
                        MessageBox(new IntPtr(0), "Hello World!", "Hello Dialog", 0);
                    }
                }
                """ + LibraryImportAttribute;

            await RunAnalyzerAsync(source, MessageBoxImplementation_DllImport);

            await RunAnalyzerAsync(source, MessageBoxImplementation_NoDllImport);
        }

        // It will have a compiler warning and recommend to use [LibraryImport] also.
        [Fact]
        public async Task Test_DefaultDllImportSearchPaths_NoDiagnosticAsync()
        {
            var source = """

                using System;
                using System.Runtime.InteropServices;

                partial class TestClass
                {
                    [DefaultDllImportSearchPaths(DllImportSearchPath.UserDirectories)]
                    public static partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);

                    public void TestMethod()
                    {
                    }
                }
                """ + LibraryImportAttribute;

            await RunAnalyzerAsync(source, MessageBoxImplementation_DllImport);
            await RunAnalyzerAsync(source, MessageBoxImplementation_NoDllImport);
        }

        // It will have a compiler warning and recommend to use [LibraryImport] also.
        [Fact]
        public async Task Test_AssemblyDefaultDllImportSearchPaths_NoDiagnosticAsync()
        {
            var source = """

                using System;
                using System.Runtime.InteropServices;

                [assembly:DefaultDllImportSearchPaths(DllImportSearchPath.UserDirectories)]

                partial class TestClass
                {
                    public static partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);

                    public void TestMethod()
                    {
                    }
                }
                """ + LibraryImportAttribute;

            await RunAnalyzerAsync(source, MessageBoxImplementation_DllImport);
            await RunAnalyzerAsync(source, MessageBoxImplementation_NoDllImport);
        }

        [Fact]
        public async Task Test_DllImportAndLibraryImportWarnsOnLibraryImport()
        {
            var source = """

                using System;
                using System.Runtime.InteropServices;

                internal partial class TestClass
                {
                    [LibraryImport("user32.dll")]
                    public static partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);
                } 
                """ + LibraryImportAttribute;

            await RunAnalyzerAsync(source, MessageBoxImplementation_DllImport, GetCSharpResultAt(8, 31,
                UseDefaultDllImportSearchPathsAttribute.UseDefaultDllImportSearchPathsAttributeRule, "MessageBox"));
            await RunAnalyzerAsync(source, MessageBoxImplementation_NoDllImport, GetCSharpResultAt(8, 31,
                UseDefaultDllImportSearchPathsAttribute.UseDefaultDllImportSearchPathsAttributeRule, "MessageBox"));
        }

        // [LibraryImport] is set with an absolute path, which will let the [DefaultDllImportSearchPaths] be ignored.
        [WindowsOnlyFact]
        public async Task Test_LibraryImportAttributeWithAbsolutePath_DefaultDllImportSearchPaths_NoDiagnosticAsync()
        {
            var source = """

                using System;
                using System.Runtime.InteropServices;

                partial class TestClass
                {
                    [LibraryImport("C:\\Windows\\System32\\user32.dll")]
                    [DefaultDllImportSearchPaths(DllImportSearchPath.UserDirectories)]
                    public static partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);

                    public void TestMethod()
                    {
                        MessageBox(new IntPtr(0), "Hello World!", "Hello Dialog", 0);
                    }
                }
                """ + LibraryImportAttribute;

            await RunAnalyzerAsync(source, MessageBoxImplementation_DllImport);
            await RunAnalyzerAsync(source, MessageBoxImplementation_NoDllImport);
        }

        // [LibraryImport] is set with an absolute path.
        [WindowsOnlyFact]
        public async Task Test_LibraryImportAttributeWithAbsolutePath_NoDiagnosticAsync()
        {
            var source = """

                using System;
                using System.Runtime.InteropServices;

                partial class TestClass
                {
                    [LibraryImport("C:\\Windows\\System32\\user32.dll")]
                    public static partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);

                    public void TestMethod()
                    {
                        MessageBox(new IntPtr(0), "Hello World!", "Hello Dialog", 0);
                    }
                }
                """ + LibraryImportAttribute;
            await RunAnalyzerAsync(source, MessageBoxImplementation_DllImport);
            await RunAnalyzerAsync(source, MessageBoxImplementation_NoDllImport);
        }

        [WindowsOnlyFact]
        public async Task Test_UsingNonexistentAbsolutePath_NoDiagnosticAsync()
        {
            var source = """

                using System;
                using System.Runtime.InteropServices;

                partial class TestClass
                {
                    [LibraryImport("C:\\Nonexistent\\user32.dll")]
                    [DefaultDllImportSearchPaths(DllImportSearchPath.UserDirectories)]
                    public static partial int MessageBox(IntPtr hWnd, String text, String caption, uint type);

                    public void TestMethod()
                    {
                        MessageBox(new IntPtr(0), "Hello World!", "Hello Dialog", 0);
                    }
                }
                """ + LibraryImportAttribute;
            await RunAnalyzerAsync(source, MessageBoxImplementation_DllImport);
            await RunAnalyzerAsync(source, MessageBoxImplementation_NoDllImport);
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