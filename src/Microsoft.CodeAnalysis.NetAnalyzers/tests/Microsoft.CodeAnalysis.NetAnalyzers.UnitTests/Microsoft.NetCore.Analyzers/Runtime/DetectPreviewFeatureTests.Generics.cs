// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpDetectPreviewFeatureAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicDetectPreviewFeatureAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public partial class DetectPreviewFeatureUnitTests
    {
        [Fact]
        public async Task TestNonPreviewMethodWithGenericPreviewParameter()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    class Program
    {
        public bool GenericMethod<T>()
        {
            return true;
        }

        static void Main(string[] args)
        {
            Program program = new Program();
            {|#0:program.GenericMethod<Foo>()|};
        }
    }

    [RequiresPreviewFeatures(""Lib is in preview."", Url = ""https://aka.ms/aspnet/kestrel/http3reqs"")]
    public class Foo
    {
    }

}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRuleWithCustomMessage).WithLocation(0).WithArguments("Foo", "https://aka.ms/aspnet/kestrel/http3reqs", "Lib is in preview."));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestGenericMethodWithPreviewClass()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{
    class Program
    {
        public bool GenericMethod<T>()
            where T : {|#0:Foo|}, ICloneable
        {
            return true;
        }
    }

    [RequiresPreviewFeatures(""Lib is in preview."", Url = ""https://aka.ms/aspnet/kestrel/http3reqs"")]
    public class Foo
    {
    }
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.UsesPreviewTypeParameterRuleWithCustomMessage).WithLocation(0).WithArguments("GenericMethod", "Foo", "https://aka.ms/aspnet/kestrel/http3reqs", "Lib is in preview."));
            await test.RunAsync();

            var vbInput = @" 
Imports System.Runtime.Versioning
Imports System

Namespace Preview_Feature_Scratch
    Class Program
        Public Function GenericMethod(Of T As {{|#0:Foo|}, ICloneable})() As Boolean
            Return True
        End Function
    End Class

    <RequiresPreviewFeatures(""Lib is in preview."", Url:=""https://aka.ms/aspnet/kestrel/http3reqs"")>
    Public Class Foo
    End Class
End Namespace
";
            var vbTest = TestVB(vbInput);
            vbTest.ExpectedDiagnostics.Add(VerifyVB.Diagnostic(DetectPreviewFeatureAnalyzer.UsesPreviewTypeParameterRuleWithCustomMessage).WithLocation(0).WithArguments("GenericMethod", "Foo", "https://aka.ms/aspnet/kestrel/http3reqs", "Lib is in preview."));
            await vbTest.RunAsync();
        }

        [Fact]
        public async Task TestGenericMethodHavingConstraintsWithPreviewInterface()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{
    class Program
    {
        public bool GenericMethod<T>()
            where T : ICloneable, {|#0:IFoo|}
        {
            return true;
        }
    }

    [RequiresPreviewFeatures(""Lib is in preview."", Url = ""https://aka.ms/aspnet/kestrel/http3reqs"")]
    public interface IFoo
    {
    }
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.UsesPreviewTypeParameterRuleWithCustomMessage).WithLocation(0).WithArguments("GenericMethod", "IFoo", "https://aka.ms/aspnet/kestrel/http3reqs", "Lib is in preview."));
            await test.RunAsync();

            var vbInput = @" 
Imports System.Runtime.Versioning
Imports System

Namespace Preview_Feature_Scratch
    Class Program
        Public Function GenericMethod(Of T As {ICloneable, {|#0:IFoo|}})() As Boolean
            Return True
        End Function
    End Class

    <RequiresPreviewFeatures(""Lib is in preview."", Url:=""https://aka.ms/aspnet/kestrel/http3reqs"")>
    Public Interface IFoo
    End Interface
End Namespace
";
            var vbTest = TestVB(vbInput);
            vbTest.ExpectedDiagnostics.Add(VerifyVB.Diagnostic(DetectPreviewFeatureAnalyzer.UsesPreviewTypeParameterRuleWithCustomMessage).WithLocation(0).WithArguments("GenericMethod", "IFoo", "https://aka.ms/aspnet/kestrel/http3reqs", "Lib is in preview."));
            await vbTest.RunAsync();
        }

        [Fact]
        public async Task TestGenericMethodWithNullablePreviewClass()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    class Program
    {
#nullable enable
        public bool GenericMethod<T>()
            where T : {|#0:Foo?|}
        {
            return true;
        }
#nullable disable

        static void Main(string[] args)
        {
        }
    }

    [RequiresPreviewFeatures]
    public class Foo
    {
    }

}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.UsesPreviewTypeParameterRule).WithLocation(0).WithArguments("GenericMethod", "Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestGenericClassWithNullablePreviewClass()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

#nullable enable
    class Program<T>
        where T : {|#0:Foo?|}
    {
        static void Main(string[] args)
        {
        }
    }
#nullable disable

    [RequiresPreviewFeatures(""Lib is in preview."", Url = ""https://aka.ms/aspnet/kestrel/http3reqs"")]
    public class Foo
    {
    }
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.UsesPreviewTypeParameterRuleWithCustomMessage).WithLocation(0).WithArguments("Program", "Foo", "https://aka.ms/aspnet/kestrel/http3reqs", "Lib is in preview."));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestGenericMethodInsidePreviewClass()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    [RequiresPreviewFeatures]
    class Program
    {
        public bool GenericMethod<T>()
            where T : Foo
        {
            return true;
        }

        static void Main(string[] args)
        {
        }
    }

    [RequiresPreviewFeatures]
    public class Foo
    {
    }

}";

            var test = TestCS(csInput);
            await test.RunAsync();
        }

        [Fact]
        public async Task TestTwoLevelGenericMethodInsidePreviewClass()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    [RequiresPreviewFeatures]
    class Program
    {
        class NestedClass
        {
            public bool GenericMethod<T>()
                where T : Foo
            {
                return true;
            }
        }

        static void Main(string[] args)
        {
        }
    }

    [RequiresPreviewFeatures]
    public class Foo
    {
    }

}";

            var test = TestCS(csInput);
            await test.RunAsync();
        }

        [Fact]
        public async Task TestGenericClassWithoutPreviewInterface()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    class Program
    {
        static void Main(string[] args)
        {
            A<Foo> aFooInstance = {|#0:new A<Foo>()|};
        }
    }

class A<T> where T : IFoo, new()
{
    public A()
    {
        new T().Bar();
    }
}

[RequiresPreviewFeatures]
class Foo : IFoo
{
    public void Bar() { }
}

interface IFoo
{
    void Bar();
}
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestGenericClassWithCustomMessageAndUrl()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    class Program
    {
        static void Main(string[] args)
        {
            A<Foo> aFooInstance = new A<Foo>();
        }
    }

class A<T> where T : {|#1:IFoo|}, new()
{
    public A()
    {
        IFoo foo = new T();
        {|#0:foo.Bar()|};
    }
}

class Foo : {|#2:IFoo|}
{
    public void {|#3:Bar|}() { }
}

[RequiresPreviewFeatures(""Lib is in preview."", Url = ""https://aka.ms/aspnet/kestrel/http3reqs"")]
interface IFoo
{
    void Bar();
}
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("Bar", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.UsesPreviewTypeParameterRuleWithCustomMessage).WithLocation(1).WithArguments("A", "IFoo", "https://aka.ms/aspnet/kestrel/http3reqs", "Lib is in preview."));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRuleWithCustomMessage).WithLocation(2).WithArguments("Foo", "IFoo", "https://aka.ms/aspnet/kestrel/http3reqs", "Lib is in preview."));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewMethodRule).WithLocation(3).WithArguments("Bar", "IFoo.Bar", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestGenericClass()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    class Program
    {
        static void Main(string[] args)
        {
            A<Foo> aFooInstance = new A<Foo>();
        }
    }

class A<T> where T : {|#1:IFoo|}, new()
{
    public A()
    {
        IFoo foo = new T();
        {|#0:foo.Bar()|};
    }
}

class Foo : {|#2:IFoo|}
{
    public void {|#3:Bar|}() { }
}

[RequiresPreviewFeatures]
interface IFoo
{
    void Bar();
}
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("Bar", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.UsesPreviewTypeParameterRule).WithLocation(1).WithArguments("A", "IFoo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(2).WithArguments("Foo", "IFoo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewMethodRule).WithLocation(3).WithArguments("Bar", "IFoo.Bar", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestClassImplementsGenericInterface()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{
class A : {|#0:IFoo<PreviewClass>|}
{
    static void Main(string[] args)
    {
    }
}

[RequiresPreviewFeatures]
interface IFoo<T>
{
}

[RequiresPreviewFeatures]
class PreviewClass
{
}
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(0).WithArguments("A", "IFoo", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestClassExtendsGenericPreviewClass()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{
class A : {|#0:PreviewClass<int>|}
{
    static void Main(string[] args)
    {
    }
}

[RequiresPreviewFeatures]
class PreviewClass<T>
{
}
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.DerivesFromPreviewClassRule).WithLocation(0).WithArguments("A", "PreviewClass", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestGenericClassWithPreviewDependency()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
using Library;
namespace Preview_Feature_Scratch
{

    class Program
    {
        static void Main(string[] args)
        {
            A<Foo> aFooInstance = new A<Foo>();
        }
    }
class A<T> where T : {|#1:IFoo|}, new()
{
    public A()
    {
        IFoo foo = new T();
        {|#0:foo.Bar()|};
    }
}
}";
            string csDependencyCode = @"
using System.Runtime.Versioning; using System;
namespace Library
{
public class Foo : {|#2:IFoo|}
{
    public void {|#3:Bar|}() { }
}

[RequiresPreviewFeatures]
public interface IFoo
{
    void Bar();
}
}";

            var test = SetupDependencyAndTestCSWithOneSourceFile(csInput, csDependencyCode);

            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("Bar", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.UsesPreviewTypeParameterRule).WithLocation(1).WithArguments("A", "IFoo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(2).WithArguments("Foo", "IFoo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewMethodRule).WithLocation(3).WithArguments("Bar", "IFoo.Bar", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }
    }
}
