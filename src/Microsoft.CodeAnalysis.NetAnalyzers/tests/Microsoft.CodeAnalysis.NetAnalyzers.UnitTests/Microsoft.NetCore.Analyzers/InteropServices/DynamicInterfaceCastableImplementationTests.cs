// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.DynamicInterfaceCastableImplementationAnalyzer,
    Microsoft.NetCore.Analyzers.InteropServices.DynamicInterfaceCastableImplementationFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.DynamicInterfaceCastableImplementationAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeAnalysis.NetAnalyzers.UnitTests.Microsoft.NetCore.Analyzers.InteropServices
{
    public class DynamicInterfaceCastableImplementationTests
    {
        [Fact]
        public async Task DynamicInterfaceCastableImplementationAttribute_VB_Diagnostic()
        {
            string source = @"
Imports System.Runtime.InteropServices

Interface I
    Sub Foo
End Interface

<DynamicInterfaceCastableImplementation>
Interface {|CA2250:I2|} : Inherits I
End Interface";

            await VerifyVBAnalyzerAsync(source);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_AllMethodsImplementated_CS_NoDiagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;

interface I
{
    void Method();
}

[DynamicInterfaceCastableImplementation]
interface I2 : I
{
    void I.Method() {}
}";

            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_AllMethodsImplementated_ParentAndTarget_CS_NoDiagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;

interface I
{
    void Method();
    void MethodWithDefaultImplementation() {}
}

[DynamicInterfaceCastableImplementation]
interface I2 : I
{
    void I.Method() {}
}";

            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_SomeMethodsImplementated_CS_Diagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

interface I
{
    void Method();
    void Method2();
}

[DynamicInterfaceCastableImplementation]
interface {|CA2251:I2|} : I
{
    void I.Method() {}
}";
            string codeFix = @"
using System;
using System.Runtime.InteropServices;

interface I
{
    void Method();
    void Method2();
}

[DynamicInterfaceCastableImplementation]
interface I2 : I
{
    void I.Method() {}

    void I.Method2()
    {
        throw new NotImplementedException();
    }
}";

            await VerifyCSCodeFixAsync(source, codeFix);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_GrandparentInterfaceMethodsNotImplementated_CS_Diagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

interface I
{
    void Method();
}

interface I2 : I
{
    void Method2();
}

[DynamicInterfaceCastableImplementation]
interface {|CA2251:I3|} : I2
{
    void I2.Method2() {}
}";
            string codeFix = @"
using System;
using System.Runtime.InteropServices;

interface I
{
    void Method();
}

interface I2 : I
{
    void Method2();
}

[DynamicInterfaceCastableImplementation]
interface I3 : I2
{
    void I2.Method2() {}

    void I.Method()
    {
        throw new NotImplementedException();
    }
}";

            await VerifyCSCodeFixAsync(source, codeFix);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_SameMethodNameMultipleParentsUnimplemented_CS_Diagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

interface I
{
    void Method();
}

interface I2
{
    void Method();
}

[DynamicInterfaceCastableImplementation]
interface {|CA2251:I3|} : I, I2
{
}";
            string codeFix = @"
using System;
using System.Runtime.InteropServices;

interface I
{
    void Method();
}

interface I2
{
    void Method();
}

[DynamicInterfaceCastableImplementation]
interface I3 : I, I2
{
    void I.Method() 
    {
        throw new NotImplementedException();
    }

    void I2.Method()
    {
        throw new NotImplementedException();
    }
}";

            await VerifyCSCodeFixAsync(source, codeFix);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_DefinedMethodsSealed_CS_NoDiagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;

interface I
{
    void Method();
}

[DynamicInterfaceCastableImplementation]
interface I2 : I
{
    void I.Method() {}

    sealed void Method2() {}
}";

            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_DefinedMethodsPrivate_CS_NoDiagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;

interface I
{
    void Method();
}

[DynamicInterfaceCastableImplementation]
interface I2 : I
{
    void I.Method() {}

    private void Method2() {}
}";

            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_DefinedMethodsImplicitPublicVirtual_CS_CodeFix()
        {
            string source = @"
using System.Runtime.InteropServices;

interface I
{
    void Method();
}

[DynamicInterfaceCastableImplementation]
interface I2 : I
{
    void I.Method() {}

    void {|CA2252:Method2|}() {}
}";

            string codeFix = @"
using System.Runtime.InteropServices;

interface I
{
    void Method();
}

[DynamicInterfaceCastableImplementation]
interface I2 : I
{
    void I.Method() {}

    sealed void Method2() {}
}";

            await VerifyCSCodeFixAsync(source, codeFix);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_DefinedMethodsImplicitPublicVirtual_NoBody_CS_CodeFix()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

interface I
{
    void Method();
}

[DynamicInterfaceCastableImplementation]
interface I2 : I
{
    void I.Method() {}

    void {|CA2252:Method2|}();
}";

            string codeFix = @"
using System;
using System.Runtime.InteropServices;

interface I
{
    void Method();
}

[DynamicInterfaceCastableImplementation]
interface I2 : I
{
    void I.Method() {}

    sealed void Method2()
    {
        throw new NotImplementedException();
    }
}";

            await VerifyCSCodeFixAsync(source, codeFix);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_DefinedMethodsImplicitPublicExplicitVirtual_CS_CodeFix()
        {
            string source = @"
using System.Runtime.InteropServices;

interface I
{
    void Method();
}

[DynamicInterfaceCastableImplementation]
interface I2 : I
{
    void I.Method() {}

    virtual void {|CA2252:Method2|}() {}
}";

            string codeFix = @"
using System.Runtime.InteropServices;

interface I
{
    void Method();
}

[DynamicInterfaceCastableImplementation]
interface I2 : I
{
    void I.Method() {}

    sealed void Method2() {}
}";

            await VerifyCSCodeFixAsync(source, codeFix);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_DefinedMethodsExplicitPublicImplicitVirtual_CS_CodeFix()
        {
            string source = @"
using System.Runtime.InteropServices;

interface I
{
    void Method();
}

[DynamicInterfaceCastableImplementation]
interface I2 : I
{
    void I.Method() {}

    public void {|CA2252:Method2|}() {}
}";

            string codeFix = @"
using System.Runtime.InteropServices;

interface I
{
    void Method();
}

[DynamicInterfaceCastableImplementation]
interface I2 : I
{
    void I.Method() {}

    public sealed void Method2() {}
}";

            await VerifyCSCodeFixAsync(source, codeFix);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_DefinedMethodsExplicitPublicVirtual_CS_CodeFix()
        {
            string source = @"
using System.Runtime.InteropServices;

interface I
{
    void Method();
}

[DynamicInterfaceCastableImplementation]
interface I2 : I
{
    void I.Method() {}

    public virtual void {|CA2252:Method2|}() {}
}";

            string codeFix = @"
using System.Runtime.InteropServices;

interface I
{
    void Method();
}

[DynamicInterfaceCastableImplementation]
interface I2 : I
{
    void I.Method() {}

    public sealed void Method2() {}
}";

            await VerifyCSCodeFixAsync(source, codeFix);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_DefinedMethodsNonPublic_CS_CodeFix()
        {
            string source = @"
using System.Runtime.InteropServices;

interface I
{
    void Method();
}

[DynamicInterfaceCastableImplementation]
interface I2 : I
{
    void I.Method() {}

    protected void {|CA2252:Method2|}() {}
}";

            string codeFix = @"
using System.Runtime.InteropServices;

interface I
{
    void Method();
}

[DynamicInterfaceCastableImplementation]
interface I2 : I
{
    void I.Method() {}

    protected sealed void Method2() {}
}";

            await VerifyCSCodeFixAsync(source, codeFix);
        }

        private static async Task VerifyCSAnalyzerAsync(string source)
        {
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = source
            }.RunAsync();
        }

        private static async Task VerifyVBAnalyzerAsync(string source)
        {
            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = source
            }.RunAsync();
        }

        private static async Task VerifyCSCodeFixAsync(string source, string codeFix)
        {
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = source,
                FixedCode = codeFix
            }.RunAsync();
        }
    }
}
