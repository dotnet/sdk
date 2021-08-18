// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.ModuleInitializerAttributeShouldNotBeUsedInLibraries,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class ModuleInitializerAttributeShouldNotBeUsedInLibrariesTests
    {
        [Theory]
        [InlineData("public", "public", false)]
        [InlineData("public", "internal", false)]
        [InlineData("internal", "public", false)]
        [InlineData("internal", "internal", false)]
        [InlineData("public", "public", true)]
        [InlineData("public", "internal", true)]
        [InlineData("internal", "public", true)]
        [InlineData("internal", "internal", true)]
        public async Task CA2255ModuleInitializerOnMethodAsync(string classModifier, string methodModifier, bool useAsync)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    Sources = {
                        @$"
                        {classModifier} class AccessibleClass
                        {{
                            [[|System.Runtime.CompilerServices.ModuleInitializer|]]
                            {methodModifier} static {(useAsync ? "async" : "")} void AccessibleInitializer() {{ }}
                        }}
                        "
                    }
                },
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9
            }.RunAsync();
        }

        [Theory]
        [InlineData("public", "public", false)]
        [InlineData("public", "internal", false)]
        [InlineData("internal", "public", false)]
        [InlineData("internal", "internal", false)]
        [InlineData("public", "public", true)]
        [InlineData("public", "internal", true)]
        [InlineData("internal", "public", true)]
        [InlineData("internal", "internal", true)]
        public async Task CA2255ModuleInitializerOnMethod_WithParensAsync(string classModifier, string methodModifier, bool useAsync)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    Sources = {
                        @$"
                        {classModifier} class AccessibleClass
                        {{
                            [[|System.Runtime.CompilerServices.ModuleInitializer()|]]
                            {methodModifier} static {(useAsync ? "async" : "")} void AccessibleInitializer() {{ }}
                        }}
                        "
                    }
                },
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9
            }.RunAsync();
        }

        [Theory]
        [InlineData("public", "public", false)]
        [InlineData("public", "internal", false)]
        [InlineData("internal", "public", false)]
        [InlineData("internal", "internal", false)]
        [InlineData("public", "public", true)]
        [InlineData("public", "internal", true)]
        [InlineData("internal", "public", true)]
        [InlineData("internal", "internal", true)]
        public async Task CA2255ModuleInitializerOnMethod_SuppressedAsync(string classModifier, string methodModifier, bool useAsync)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    Sources = {
                        @$"
                        {classModifier} class AccessibleClass
                        {{
                        #pragma warning disable CA2255
                            [System.Runtime.CompilerServices.ModuleInitializer]
                        #pragma warning restore CA2255
                            {methodModifier} static {(useAsync ? "async" : "")} void AccessibleInitializer() {{ }}
                        }}
                        "
                    }
                },
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9
            }.RunAsync();
        }

        [Fact]
        public async Task CA2255DoesNotApply_ToPrivateMethodsAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    Sources = {
                        @"
                        public class PublicClass
                        {
                            [System.Runtime.CompilerServices.ModuleInitializer]
                            private static void PrivateInitializer() { }
                        }
                        "
                    }
                },
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                // CS8814 is reported on inaccessible initializers
                CompilerDiagnostics = CompilerDiagnostics.None
            }.RunAsync();
        }

        [Fact]
        public async Task CA2255DoesNotApply_ToPrivateNestedClassesAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    Sources = {
                        @"
                        public class PublicClass
                        {
                            private class PrivateNestedClass
                            {
                                [System.Runtime.CompilerServices.ModuleInitializer]
                                public static void PublicInitializer() { }
                            }
                        }
                        "
                    }
                },
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                // CS8814 is reported on inaccessible initializers 
                CompilerDiagnostics = CompilerDiagnostics.None
            }.RunAsync();
        }

        [Fact]
        public async Task CA2255DoesNotApply_ToNonVoidMethodsAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    Sources = {
                        @"
                        public class PublicClass
                        {
                            [System.Runtime.CompilerServices.ModuleInitializer]
                            public static bool NonVoidInitializer() { return true; }
                        }
                        "
                    }
                },
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                // CS8815 is reported on non-void initializers
                CompilerDiagnostics = CompilerDiagnostics.None
            }.RunAsync();
        }

        [Fact]
        public async Task CA2255DoesNotApply_ToInstanceMethodsAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    Sources = {
                        @"
                        public class PublicClass
                        {
                            [System.Runtime.CompilerServices.ModuleInitializer]
                            public void InstanceInitializer() { }
                        }
                        "
                    }
                },
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                // CS8815 is reported on instance initializers
                CompilerDiagnostics = CompilerDiagnostics.None
            }.RunAsync();
        }

        [Fact]
        public async Task CA2255DoesNotApply_ToGenericTypesAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    Sources = {
                        @"
                        public class GenericClass<T>
                        {
                            [System.Runtime.CompilerServices.ModuleInitializer]
                            public static void Initializer() { }
                        }
                        "
                    }
                },
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                // CS8816 is reported on Generic type initializers
                CompilerDiagnostics = CompilerDiagnostics.None
            }.RunAsync();
        }

        [Fact]
        public async Task CA2255DoesNotApply_ToGenericMethodsAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    Sources = {
                        @"
                        public class PublicClass<T>
                        {
                            [System.Runtime.CompilerServices.ModuleInitializer]
                            public static void GenericInitializer<T>() { }
                        }
                        "
                    }
                },
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                // CS8816 is reported on Generic initializers
                CompilerDiagnostics = CompilerDiagnostics.None
            }.RunAsync();
        }
    }
}
