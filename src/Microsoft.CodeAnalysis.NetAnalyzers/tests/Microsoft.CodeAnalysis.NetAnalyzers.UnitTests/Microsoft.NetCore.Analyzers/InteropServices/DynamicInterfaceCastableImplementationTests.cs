﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.DynamicInterfaceCastableImplementationAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.InteropServices.CSharpDynamicInterfaceCastableImplementationFixer>;
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
Interface {|CA2258:I2|} : Inherits I
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
        public async Task DynamicInterfaceCastableImplementation_ParentMethodsPrivate_CS_NoDiagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;

interface I
{
    private void Method() {}
}

[DynamicInterfaceCastableImplementation]
interface I2 : I
{
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
interface {|CA2256:I2|} : I
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
interface {|CA2256:I3|} : I2
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
interface {|CA2256:I3|} : I, I2
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
        public async Task DynamicInterfaceCastableImplementation_NoPropertiesImplementated_CS_Diagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

interface I
{
    int Property { get; set; }
}

[DynamicInterfaceCastableImplementation]
interface {|CA2256:I2|} : I
{
}";
            string codeFix = @"
using System;
using System.Runtime.InteropServices;

interface I
{
    int Property { get; set; }
}

[DynamicInterfaceCastableImplementation]
interface I2 : I
{
    int I.Property
    {
        get
        {
            throw new NotImplementedException();
        }

        set
        {
            throw new NotImplementedException();
        }
    }
}";

            await VerifyCSCodeFixAsync(source, codeFix);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_NoIndexerImplementated_CS_Diagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

interface I
{
    int this[int i] { get; set; }
}

[DynamicInterfaceCastableImplementation]
interface {|CA2256:I2|} : I
{
}";
            string codeFix = @"
using System;
using System.Runtime.InteropServices;

interface I
{
    int this[int i] { get; set; }
}

[DynamicInterfaceCastableImplementation]
interface I2 : I
{
    int I.this[int i]
    {
        get
        {
            throw new NotImplementedException();
        }

        set
        {
            throw new NotImplementedException();
        }
    }
}";

            await VerifyCSCodeFixAsync(source, codeFix);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_NoPropertiesImplementated_GetAccessor_CS_Diagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

interface I
{
    int Property { get; }
}

[DynamicInterfaceCastableImplementation]
interface {|CA2256:I2|} : I
{
}";
            string codeFix = @"
using System;
using System.Runtime.InteropServices;

interface I
{
    int Property { get; }
}

[DynamicInterfaceCastableImplementation]
interface I2 : I
{
    int I.Property
    {
        get
        {
            throw new NotImplementedException();
        }
    }
}";

            await VerifyCSCodeFixAsync(source, codeFix);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_NoPropertiesImplementated_SetAccessor_CS_Diagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

interface I
{
    int Property { set; }
}

[DynamicInterfaceCastableImplementation]
interface {|CA2256:I2|} : I
{
}";
            string codeFix = @"
using System;
using System.Runtime.InteropServices;

interface I
{
    int Property { set; }
}

[DynamicInterfaceCastableImplementation]
interface I2 : I
{
    int I.Property
    {
        set
        {
            throw new NotImplementedException();
        }
    }
}";

            await VerifyCSCodeFixAsync(source, codeFix);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_NoPropertiesImplementated_InitAccessor_CS_Diagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

interface I
{
    int Property { init; }
}

[DynamicInterfaceCastableImplementation]
interface {|CA2256:I2|} : I
{
}";
            string codeFix = @"
using System;
using System.Runtime.InteropServices;

interface I
{
    int Property { init; }
}

[DynamicInterfaceCastableImplementation]
interface I2 : I
{
    int I.Property
    {
        init
        {
            throw new NotImplementedException();
        }
    }
}";

            await VerifyCSCodeFixAsync(source, codeFix);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_NoEventsImplemented_CS_Diagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

interface I
{
    event Action Event;
}

[DynamicInterfaceCastableImplementation]
interface {|CA2256:I2|} : I
{
}";

            string codeFix = @"
using System;
using System.Runtime.InteropServices;

interface I
{
    event Action Event;
}

[DynamicInterfaceCastableImplementation]
interface I2 : I
{
    event Action I.Event
    {
        add
        {
            throw new NotImplementedException();
        }

        remove
        {
            throw new NotImplementedException();
        }
    }
}";

            await VerifyCSCodeFixAsync(source, codeFix);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_DefinedMethodsStatic_CS_NoDiagnostic()
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

    static void Method2(I2 @this)
    { }
}";

            await VerifyCSCodeFixAsync(source, source);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_DefinedMethodsSealed_CS_Diagnostic()
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

    sealed void {|CA2257:Method2|}() {}
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

    static void Method2(I2 @this)
    { }
}";

            await VerifyCSCodeFixAsync(source, codeFix);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_DefinedMethodsPrivate_CS_Diagnostic()
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

    private void {|CA2257:Method2|}() {}
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

    private static void Method2(I2 @this) { }
}";

            await VerifyCSCodeFixAsync(source, codeFix);
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

    void {|CA2257:Method2|}() {}
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

    static void Method2(I2 @this)
    { }
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

    void {|CA2257:Method2|}();
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

    static void Method2(I2 @this)
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

    virtual void {|CA2257:Method2|}() {}
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

    static void Method2(I2 @this)
    { }
}";

            await VerifyCSCodeFixAsync(source, codeFix);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_DefinedMethodsImplicitPublicExplicitAbstract_CS_CodeFix()
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

    abstract void {|CA2257:Method2|}();
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

    static void Method2(I2 @this)
    {
        throw new System.NotImplementedException();
    }
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

    public void {|CA2257:Method2|}() {}
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

    public static void Method2(I2 @this) { }
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

    public virtual void {|CA2257:Method2|}() {}
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

    public static void Method2(I2 @this) { }
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

    protected void {|CA2257:Method2|}() {}
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

    protected static void Method2(I2 @this) { }
}";

            await VerifyCSCodeFixAsync(source, codeFix);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_DefinedProperties_NoBody_CS_Diagnostic()
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

    int {|CA2257:Property|}
    {
        get;
        set;
    }
}";

            await VerifyCSCodeFixAsync(source, source);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_DefinedProperties_NoBody_GetOnly_CS_Diagnostic()
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

    int {|CA2257:Property|}
    {
        get;
    }
}";

            await VerifyCSCodeFixAsync(source, source);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_DefinedProperties_NoBody_SetOnly_CS_Diagnostic()
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

    int {|CA2257:Property|}
    {
        set;
    }
}";

            await VerifyCSCodeFixAsync(source, source);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_DefinedProperties_NoBody_InitOnly_CS_Diagnostic()
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

    int {|CA2257:Property|}
    {
        init;
    }
}";

            await VerifyCSCodeFixAsync(source, source);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_DefinedProperties_SetOnly_CS_Diagnostic()
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

    int {|CA2257:Property|} { set {} }
}";

            await VerifyCSCodeFixAsync(source, source);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_Events_NoBody_CS_Diagnostic()
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

    event Action {|CA2257:Event|};
}";

            await VerifyCSCodeFixAsync(source, source);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_Events_MultipleInOneDeclaration_NoBody_CS_Diagnostic()
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

    event Action {|CA2257:Event|}, {|CA2257:Event2|};
}";

            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_Events_CS_Diagnostic()
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

    event Action {|CA2257:Event|}
    {
        add
        {
        }
        remove
        {
        }
    }
}";

            await VerifyCSCodeFixAsync(source, source);
        }

        [Fact]
        public async Task DynamicInterfaceCastableImplementation_InstanceMethod_CS_UsageRewritten()
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
    void I.Method()
    {
        Method2(42);
        this.Method2(10);

        static void LocalFunction(I2 obj)
        {
            obj.Method2(30);
        }
    }

    void {|CA2257:Method2|}(int i)
    {
        _ = i;
    }
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
    void I.Method()
    {
        Method2(this, 42);
        Method2(this, 10);

        static void LocalFunction(I2 obj)
        {
            Method2(obj, 30);
        }
    }

    static void Method2(I2 @this, int i)
    {
        _ = i;
    }
}";
            await VerifyCSCodeFixAsync(source, codeFix);
        }

        [Fact]
        [WorkItem(5964, "https://github.com/dotnet/roslyn-analyzers/issues/5964")]
        public async Task DynamicInterfaceCastableImplementation_InterfaceContainingNamedType_NoDiagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;

public interface IMyInterface
{
    internal class C { }
    internal abstract class C2 { }

    [DynamicInterfaceCastableImplementation]
    internal interface INestedInterface : IMyInterface
    {
        void IMyInterface.M()
        {
        }
    }

    void M();
}
";
            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        [WorkItem(5964, "https://github.com/dotnet/roslyn-analyzers/issues/5964")]
        public async Task DynamicInterfaceCastableImplementation_InterfaceContainingNamedType_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;

public interface IMyInterface
{
    internal class C { }
    internal abstract class C2 { }

    [DynamicInterfaceCastableImplementation]
    internal interface {|CA2256:INestedInterface|} : IMyInterface
    {
    }

    void M();
}
";
            string codeFix = @"
using System.Runtime.InteropServices;

public interface IMyInterface
{
    internal class C { }
    internal abstract class C2 { }

    [DynamicInterfaceCastableImplementation]
    internal interface INestedInterface : IMyInterface
    {
        void IMyInterface.M()
        {
            throw new System.NotImplementedException();
        }
    }

    void M();
}
";
            await VerifyCSCodeFixAsync(source, codeFix);
        }

        [Fact]
        [WorkItem(5964, "https://github.com/dotnet/roslyn-analyzers/issues/5964")]
        public async Task DynamicInterfaceCastableImplementation_AbstractStaticInInterface()
        {
            string source = @"
using System.Runtime.InteropServices;

public interface I
{
    static abstract void M();
}

[DynamicInterfaceCastableImplementation]
public interface {|CA2256:I2|} : I
{
}
";
            string fixedSource = @"
using System.Runtime.InteropServices;

public interface I
{
    static abstract void M();
}

[DynamicInterfaceCastableImplementation]
public interface I2 : I
{
    static void I.M()
    {
        throw new System.NotImplementedException();
    }
}
";

            await VerifyCSCodeFixAsync(source, fixedSource, CSharp.LanguageVersion.Preview, ReferenceAssemblies.Net.Net60);
        }

        [Fact]
        [WorkItem(7106, "htpts://github.com/dotnet/roslyn-analyzers/issues/7106")]
        public async Task DynamicInterfaceCastableImplementation_NonStatic_NestedType()
        {
            string source = @"
using System.Runtime.InteropServices;

public interface IMyInterface
{
    void M();
}

[DynamicInterfaceCastableImplementation]
internal interface IImpl : IMyInterface
{
    internal class C { }

    void IMyInterface.M()
    {
    }
}
";
            await VerifyCSAnalyzerAsync(source);
        }

        private static Task VerifyCSAnalyzerAsync(string source)
        {
            return VerifyCSCodeFixAsync(source, source);
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
            await VerifyCSCodeFixAsync(source, codeFix, CSharp.LanguageVersion.CSharp9, ReferenceAssemblies.Net.Net50);
        }

        private static async Task VerifyCSCodeFixAsync(string source, string codeFix, CSharp.LanguageVersion languageVersion, ReferenceAssemblies referenceAssemblies)
        {
            await new VerifyCS.Test
            {
                LanguageVersion = languageVersion,
                ReferenceAssemblies = referenceAssemblies,
                TestCode = source,
                FixedCode = codeFix
            }.RunAsync();
        }
    }
}
