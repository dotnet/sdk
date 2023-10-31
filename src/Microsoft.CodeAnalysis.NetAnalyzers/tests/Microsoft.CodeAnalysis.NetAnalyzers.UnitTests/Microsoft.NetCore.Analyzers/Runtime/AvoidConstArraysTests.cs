// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.AvoidConstArraysAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.AvoidConstArraysFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.AvoidConstArraysAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.AvoidConstArraysFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class AvoidConstArraysTests
    {
        [Fact]
        public async Task IdentifyConstArrays_ImplicitInitialization()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

namespace Z
{
    public class A
    {
        public void B()
        {
            Console.WriteLine({|CA1861:new[]{ 1, 2, 3 }|});
        }
    }
}
", @"
using System;

namespace Z
{
    public class A
    {
        private static readonly int[] value = new[]{ 1, 2, 3 };

        public void B()
        {
            Console.WriteLine(value);
        }
    }
}
");

            await VerifyVB.VerifyCodeFixAsync(@"
Imports System

Namespace Z
    Public Class A
        Public Sub B()
            Console.WriteLine({|CA1861:{1, 2, 3}|})
        End Sub
    End Class
End Namespace
", @"
Imports System

Namespace Z
    Public Class A
        Private Shared ReadOnly value As Integer() = {1, 2, 3}
        Public Sub B()
            Console.WriteLine(value)
        End Sub
    End Class
End Namespace
");
        }

        [Fact]
        public async Task IdentifyConstArrays_ExplicitInitialization()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

namespace Z
{
    public class A
    {
        public void B()
        {
            Console.WriteLine({|CA1861:new int[]{ 1, 2, 3 }|});
        }
    }
}
", @"
using System;

namespace Z
{
    public class A
    {
        private static readonly int[] value = new int[]{ 1, 2, 3 };

        public void B()
        {
            Console.WriteLine(value);
        }
    }
}
");

            await VerifyCS.VerifyCodeFixAsync(@"
using System;

namespace Z
{
    public class A
    {
        public void B()
        {
            Console.WriteLine({|CA1861:new int[]{ 1, 2, 3 }|});
        }
    }
}
", @"
using System;

namespace Z
{
    public class A
    {
        private static readonly int[] value = new int[]{ 1, 2, 3 };

        public void B()
        {
            Console.WriteLine(value);
        }
    }
}
");

            await VerifyVB.VerifyCodeFixAsync(@"
Imports System

Namespace Z
    Public Class A
        Public Sub B()
            Console.WriteLine({|CA1861:New Integer() {1, 2, 3}|})
        End Sub
    End Class
End Namespace
", @"
Imports System

Namespace Z
    Public Class A
        Private Shared ReadOnly value As Integer() = New Integer() {1, 2, 3}
        Public Sub B()
            Console.WriteLine(value)
        End Sub
    End Class
End Namespace
");
        }

        [Fact]
        public async Task IdentifyConstArrays_NestedArgs()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

namespace Z
{
    public class A
    {
        public void B()
        {
            Console.WriteLine(string.Join("" "", {|CA1861:new[] { ""Cake"", ""is"", ""good"" }|}));
        }
    }
}
", @"
using System;

namespace Z
{
    public class A
    {
        private static readonly string[] value = new[] { ""Cake"", ""is"", ""good"" };

        public void B()
        {
            Console.WriteLine(string.Join("" "", value));
        }
    }
}
");

            await VerifyVB.VerifyCodeFixAsync(@"
Imports System

Namespace Z
    Public Class A
        Public Sub B()
            Console.WriteLine(String.Join("" ""c, {|CA1861:{""Cake"", ""is"", ""good""}|}))
        End Sub
    End Class
End Namespace
", @"
Imports System

Namespace Z
    Public Class A
        Private Shared ReadOnly value As String() = {""Cake"", ""is"", ""good""}
        Public Sub B()
            Console.WriteLine(String.Join("" ""c, value))
        End Sub
    End Class
End Namespace
");
        }

        [Fact]
        public async Task IdentifyConstArrays_TriviaTest()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

namespace Z
{
    public class A
    {
        public void B()
        {
            Console.WriteLine(string.Join(
                ""a"",
                {|CA1861:new[] { ""b"", ""c"" }|}, /* test comment */
                ""d""
            ));
        }
    }
}
", @"
using System;

namespace Z
{
    public class A
    {
        private static readonly string[] values = new[] { ""b"", ""c"" };

        public void B()
        {
            Console.WriteLine(string.Join(
                ""a"",
                values, /* test comment */
                ""d""
            ));
        }
    }
}
");
        }

        [Fact]
        public async Task IdentifyConstArrays_LambdaArrayCreation()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
using System.Linq;

namespace Z
{
    public class A
    {
        public void B()
        {
            var x = new string[] { ""a"", ""b"" };
            var y = x.Select(z => {|CA1861:new[] { ""c"" }|});
        }
    }
}
", @"
using System;
using System.Linq;

namespace Z
{
    public class A
    {
        private static readonly string[] stringArray = new[] { ""c"" };

        public void B()
        {
            var x = new string[] { ""a"", ""b"" };
            var y = x.Select(z => stringArray);
        }
    }
}
");
        }

        [Fact]
        public async Task IdentifyConstArrays_LambdaArrayCreationTwoParams()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
using System.Linq;

namespace Z
{
    public class A
    {
        public void B()
        {
            var x = new string[] { ""a"", ""b"" };
            var y = x.Select((z1, z2) => {|CA1861:new[] { ""c"" }|});
        }
    }
}
", @"
using System;
using System.Linq;

namespace Z
{
    public class A
    {
        private static readonly string[] selector = new[] { ""c"" };

        public void B()
        {
            var x = new string[] { ""a"", ""b"" };
            var y = x.Select((z1, z2) => selector);
        }
    }
}
");
        }

        [Fact]
        public async Task IdentifyConstArrays_LambdaInvokedArrayCreation()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
using System.Linq;

namespace Z
{
    public class A
    {
        public void B()
        {
            var x = new string[] { ""a"", ""b"" };
            var y = x.Select(z => {|CA1861:new[] { ""c"" }|}.First());
        }
    }
}
", @"
using System;
using System.Linq;

namespace Z
{
    public class A
    {
        private static readonly string[] sourceArray = new[] { ""c"" };

        public void B()
        {
            var x = new string[] { ""a"", ""b"" };
            var y = x.Select(z => sourceArray.First());
        }
    }
}
");
        }

        [Fact]
        public async Task IdentifyConstArrays_ExtensionMethod()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
using System.Linq;

namespace Z
{
    public class A
    {
        public void B()
        {
            string y = {|CA1861:new[] { ""a"", ""b"", ""c"" }|}.First();
            Console.WriteLine(y);
        }
    }
}
", @"
using System;
using System.Linq;

namespace Z
{
    public class A
    {
        private static readonly string[] sourceArray = new[] { ""a"", ""b"", ""c"" };

        public void B()
        {
            string y = sourceArray.First();
            Console.WriteLine(y);
        }
    }
}
");

            await VerifyVB.VerifyCodeFixAsync(@"
Imports System
Imports System.Linq

Namespace Z
    Public Class A
        Public Sub B()
            Dim y As String = {|CA1861:{""a"", ""b"", ""c""}|}.First()
            Console.WriteLine(y)
        End Sub
    End Class
End Namespace
", @"
Imports System
Imports System.Linq

Namespace Z
    Public Class A
        Private Shared ReadOnly stringArray As String() = {""a"", ""b"", ""c""}
        Public Sub B()
            Dim y As String = stringArray.First()
            Console.WriteLine(y)
        End Sub
    End Class
End Namespace
");
        }

        [Fact]
        public async Task IdentifyConstArrays_ParamsArrayOfLiterals()
        {
            // A params argument passed as an array of literals
            await VerifyCS.VerifyCodeFixAsync(@"
namespace Z
{
    public class A
    {
        public void B()
        {
            C({|CA1861:new bool[] { true, false }|});
        }

        private void C(params bool[] booleans)
        {
        }
    }
}
", @"
namespace Z
{
    public class A
    {
        private static readonly bool[] booleanArray = new bool[] { true, false };

        public void B()
        {
            C(booleanArray);
        }

        private void C(params bool[] booleans)
        {
        }
    }
}
");
        }

        [Fact]
        public async Task IdentifyConstArrays_ParamsArrays()
        {
            // A params array of arrays
            // Doubles as test for batch fix and two or more errors on same line
            await new VerifyCS.Test()
            {
                TestCode = @"
namespace Z
{
    public class A
    {
        public void B()
        {
            C({|CA1861:new bool[] { true, false }|}, {|CA1861:new bool[] { false, true }|});
        }

        private void C(params bool[][] booleans)
        {
        }
    }
}
",
                NumberOfFixAllIterations = 2,
                FixedCode = @"
namespace Z
{
    public class A
    {
        private static readonly bool[] booleanArray = new bool[] { true, false };
        private static readonly bool[] booleanArray0 = new bool[] { false, true };

        public void B()
        {
            C(booleanArray, booleanArray0);
        }

        private void C(params bool[][] booleans)
        {
        }
    }
}
"
            }.RunAsync();
        }

        [Fact]
        public async Task IdentifyConstArrays_MemberExtractionTest()
        {
            // Member extraction tests
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

namespace Z
{
    public class A
    {
        private static readonly string value = ""hello"";
        private static readonly int[] valueArray = new[]{ -2, -1, 0 };
        private static readonly bool[] valueArray1 = new[]{ true, false, true };

        private static readonly int x = 1;

        public void B()
        {
            Console.WriteLine({|CA1861:new[]{ 1, 2, 3 }|});
        }
    }
}
", @"
using System;

namespace Z
{
    public class A
    {
        private static readonly string value = ""hello"";
        private static readonly int[] valueArray = new[]{ -2, -1, 0 };
        private static readonly bool[] valueArray1 = new[]{ true, false, true };

        private static readonly int x = 1;
        private static readonly int[] valueArray0 = new[]{ 1, 2, 3 };

        public void B()
        {
            Console.WriteLine(valueArray0);
        }
    }
}
");

            await VerifyVB.VerifyCodeFixAsync(@"
Imports System

Namespace Z
    Public Class A
        Private Shared ReadOnly value As String = ""hello""
        Private Shared ReadOnly valueArray As Integer() = {-2, -1, 0}
        Private Shared ReadOnly valueArray1 As Boolean() = {True, False, True}
        Private Shared ReadOnly x As Integer = 1

        Public Sub B()
            Console.WriteLine({|CA1861:{1, 2, 3}|})
        End Sub
    End Class
End Namespace
", @"
Imports System

Namespace Z
    Public Class A
        Private Shared ReadOnly value As String = ""hello""
        Private Shared ReadOnly valueArray As Integer() = {-2, -1, 0}
        Private Shared ReadOnly valueArray1 As Boolean() = {True, False, True}
        Private Shared ReadOnly x As Integer = 1
        Private Shared ReadOnly valueArray0 As Integer() = {1, 2, 3}

        Public Sub B()
            Console.WriteLine(valueArray0)
        End Sub
    End Class
End Namespace
");
        }

        [Fact]
        public async Task IgnoreOtherArgs_NoDiagnostic()
        {
            // A string
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

namespace Z
{
    public class A
    {
        public void B()
        {
            Console.WriteLine(""Lorem ipsum"");
        }
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Namespace Z
    Public Class A
        Public Sub B()
            Console.WriteLine(""Lorem ipsum"")
        End Sub
    End Class
End Namespace
");

            // Test another type to be extra safe
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

namespace Z
{
    public class A
    {
        public void B()
        {
            Console.WriteLine(123);
        }
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Namespace Z
    Public Class A
        Public Sub B()
            Console.WriteLine(123)
        End Sub
    End Class
End Namespace
");

            // Non-literal array
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

namespace Z
{
    public class A
    {
        public void B()
        {
            string str = ""Lorem ipsum"";
            Console.WriteLine(new[] { str });
        }
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Namespace Z
    Public Class A
        Public Sub B()
            Dim str As String = ""Lorem ipsum""
            Console.WriteLine({ str })
        End Sub
    End Class
End Namespace
");
        }

        [Fact]
        public async Task IgnoreReadonlySpan_NoDiagnostic()
        {
            // A ReadOnlySpan, which is already optimized
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

namespace Z
{
    public class A
    {
        public void B()
        {
            C(new bool[] { true, false });
        }

        private void C(ReadOnlySpan<bool> span)
        {
        }
    }
}
");
        }

        [Fact]
        public async Task IgnoreParams_NoDiagnostic()
        {
            // Params arguments
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace Z
{
    public class A
    {
        public void B()
        {
            C(true, false);
        }

        private void C(params bool[] booleans)
        {
        }
    }
}
");
        }

        [Fact]
        public async Task IgnoreReadonlyFieldAssignment_NoDiagnostic()
        {
            // Ignore when we're an argument used in a method/constructor that is assigned to a readonly field
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace Z
{
    public class A
    {
        private static readonly B s = new B(new string[] { ""a"" });
    }

    public class B
    {
        public B(string[] s)
        {
        }
    }
}
");
        }

        [Fact]
        public async Task IgnoreReadOnlyProperties_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Collections.Generic;

public class A
{
    public readonly List<string> Field1 = GetValues(new string[] { ""close"" });
    public static readonly A Field; 
    public static List<string> Property { get; } = GetValues(new string[] { ""close"" });
    public static string[] Property2 { get; } = new string[] { ""close"" };

    static A() // Exclude initialization in static constructors
    {
        Property = GetValues(new string[] { ""close"" });
        Field = new A(new string[] { ""close"" });
    }
    public A(string[] arr) { }
    private static List<string> GetValues(string[] arr) => null;
}");
        }

        [Fact, WorkItem(6629, "https://github.com/dotnet/roslyn-analyzers/issues/6629")]
        public Task StaticReadonlyCollection_NoDiagnostic()
        {
            return new VerifyCS.Test
            {
                TestCode = @"
#nullable enable
using System.Collections.ObjectModel;

public class Test
{
    private static ReadOnlyCollection<string>? s_errorPayloadNames;

    private void M(string eventName, string msg)
    {
        s_errorPayloadNames ??= new ReadOnlyCollection<string>(new string[] { ""message"" });
    }
}",
                LanguageVersion = LanguageVersion.CSharp8
            }.RunAsync();
        }

        [Fact, WorkItem(6686, "https://github.com/dotnet/roslyn-analyzers/issues/6686")]
        public Task ArrayWithoutInitializer_NoDiagnostic()
        {
            return new VerifyCS.Test
            {
                TestCode = @"using System.Collections.Generic;

public class MyClass
{
    public List<object> Cases => new() { new object[0] };
}",
                LanguageVersion = LanguageVersion.CSharp10
            }.RunAsync();
        }

        [Fact, WorkItem(6686, "https://github.com/dotnet/roslyn-analyzers/issues/6697")]
        public async Task ArrayWithoutInitializer_NoDiagnostic2()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class MyClass
{
    public void M1(Type[] types) { }
    public void M2(int length)
    {
         M1(new Type[length]);
    }
}");
        }

        [Fact, WorkItem(6981, "https://github.com/dotnet/roslyn-analyzers/issues/6981")]
        public Task UseUniqueIdentifier_Parameter()
        {
            const string source = """
                                  class Sample
                                  {
                                      void A(char separator, char separatorArray)
                                      {
                                          "".Split([|new char[] { 'a', 'b' }|]);
                                      }
                                  }
                                  """;
            const string fixedSource = """
                                       class Sample
                                       {
                                           internal static readonly char[] separatorArray0 = new char[] { 'a', 'b' };
                                       
                                           void A(char separator, char separatorArray)
                                           {
                                               "".Split(separatorArray0);
                                           }
                                       }
                                       """;

            return VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact, WorkItem(6981, "https://github.com/dotnet/roslyn-analyzers/issues/6981")]
        public Task UseUniqueIdentifier_Local()
        {
            const string source = """
                                  class Sample
                                  {
                                      void A()
                                      {
                                          object separator = null;
                                          object separatorArray = null;
                                          "".Split([|new char[] { 'a', 'b' }|]);
                                      }
                                  }
                                  """;
            const string fixedSource = """
                                       class Sample
                                       {
                                           internal static readonly char[] separatorArray0 = new char[] { 'a', 'b' };
                                       
                                           void A()
                                           {
                                               object separator = null;
                                               object separatorArray = null;
                                               "".Split(separatorArray0);
                                           }
                                       }
                                       """;

            return VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact, WorkItem(6981, "https://github.com/dotnet/roslyn-analyzers/issues/6981")]
        public Task UseUniqueIdentifier_Field()
        {
            const string source = """
                                  class Sample
                                  {
                                      private string separator;
                                      private string separatorArray;

                                      void A()
                                      {
                                          "".Split([|new char[] { 'a', 'b' }|]);
                                      }
                                  }
                                  """;
            const string fixedSource = """
                                       class Sample
                                       {
                                           private string separator;
                                           private string separatorArray;
                                           internal static readonly char[] separatorArray0 = new char[] { 'a', 'b' };

                                           void A()
                                           {
                                               "".Split(separatorArray0);
                                           }
                                       }
                                       """;

            return VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact, WorkItem(6981, "https://github.com/dotnet/roslyn-analyzers/issues/6981")]
        public Task UseUniqueIdentifier_FieldAndParameter()
        {
            const string source = """
                                  class Sample
                                  {
                                      private string separator;
                                      private string separatorArray;

                                      void A(char separatorArray0)
                                      {
                                          "".Split([|new char[] { 'a', 'b' }|]);
                                      }
                                  }
                                  """;
            const string fixedSource = """
                                       class Sample
                                       {
                                           private string separator;
                                           private string separatorArray;
                                           internal static readonly char[] separatorArray1 = new char[] { 'a', 'b' };
                                       
                                           void A(char separatorArray0)
                                           {
                                               "".Split(separatorArray1);
                                           }
                                       }
                                       """;

            return VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }
    }
}
