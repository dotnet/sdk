// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.UseConcreteTypeAnalyzer,
    Microsoft.NetCore.Analyzers.Performance.UseConcreteTypeFixer>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public partial class UseConcreteTypeTests
    {
        [TestMethod]
        public async Task CodeFix_CSharp_UpdatesSupportedDeclarations()
        {
            var source = """
                interface I
                {
                    void M();
                }

                class Impl : I
                {
                    public void M() { }
                }

                class C
                {
                    private I {|CA1859:_field|} = new Impl();
                    private I {|CA1859:Property|} { get; } = new Impl();

                    private I {|CA1859:Get|}()
                    {
                        return new Impl();
                    }

                    private void Use(I {|CA1859:value|})
                    {
                        value.M();
                    }

                    private void M()
                    {
                        I {|CA1859:local|} = new Impl();
                        _field.M();
                        Property.M();
                        local.M();
                        Use(new Impl());
                    }
                }
                """;
            var fixedSource = """
                interface I
                {
                    void M();
                }

                class Impl : I
                {
                    public void M() { }
                }

                class C
                {
                    private Impl _field = new Impl();
                    private Impl Property { get; } = new Impl();

                    private Impl Get()
                    {
                        return new Impl();
                    }

                    private void Use(Impl value)
                    {
                        value.M();
                    }

                    private void M()
                    {
                        Impl local = new Impl();
                        _field.M();
                        Property.M();
                        local.M();
                        Use(new Impl());
                    }
                }
                """;

            var test = new VerifyCS.Test
            {
                TestCode = source,
                FixedCode = fixedSource,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net70,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.Preview,
                MarkupOptions = MarkupOptions.UseFirstDescriptor,
                NumberOfFixAllIterations = 1,
            };

            await test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task CodeFix_PreservesNullableAnnotations()
        {
            var source = """
                #nullable enable

                interface I<T> { }
                class Impl<T> : I<T> { }

                class C
                {
                    private Impl<string?>? _value;

                    private I<string?>? {|CA1859:GetValue|}()
                    {
                        return _value;
                    }
                }
                """;
            var fixedSource = """
                #nullable enable

                interface I<T> { }
                class Impl<T> : I<T> { }

                class C
                {
                    private Impl<string?>? _value;

                    private Impl<string?>? GetValue()
                    {
                        return _value;
                    }
                }
                """;

            var test = new VerifyCS.Test
            {
                TestCode = source,
                FixedCode = fixedSource,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net70,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.Preview,
                MarkupOptions = MarkupOptions.UseFirstDescriptor,
            };

            await test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task CodeFix_NotOfferedForMultiVariableDeclaration()
        {
            var source = """
                interface I
                {
                    void M();
                }

                class Impl : I
                {
                    public void M() { }
                }

                class C
                {
                    private void M()
                    {
                        I {|CA1859:first|} = new Impl(), {|CA1859:second|} = new Impl();
                        first.M();
                        second.M();
                    }
                }
                """;

            var test = new VerifyCS.Test
            {
                TestCode = source,
                FixedCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net70,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.Preview,
                MarkupOptions = MarkupOptions.UseFirstDescriptor,
            };

            await test.RunAsync(CancellationToken.None);
        }
    }
}
