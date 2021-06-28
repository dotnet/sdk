// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.CSharp.Analyzers.Performance;
using Microsoft.NetCore.VisualBasic.Analyzers.Performance;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.UseCountProperlyAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Performance.CSharpUsePropertyInsteadOfCountMethodWhenAvailableFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.UseCountProperlyAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Performance.BasicUsePropertyInsteadOfCountMethodWhenAvailableFixer>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public static partial class UsePropertyInsteadOfCountMethodWhenAvailableTests
    {
        [Fact]
        public static Task CSharp_AsMethodArgument_Tests()
            => new VerifyCS.Test
            {
                TestState =
                {
                    Sources=
                    {
                $@"using System;
using System.Linq;
public static class C
{{
    public static System.Collections.Immutable.ImmutableArray<int> GetData() => default;
    public static void M()
    {{
        var a = 1.Equals({{|{UseCountProperlyAnalyzer.CA1829}:GetData().Count()|}});
        var b = 1.Equals({{|{UseCountProperlyAnalyzer.CA1829}:(GetData()).Count()|}});
        var c = 1.Equals(({{|{UseCountProperlyAnalyzer.CA1829}:GetData().Count()|}}));
    }}
}}
",
                    },
                },
                FixedState =
                {
                    Sources=
                    {
                $@"using System;
using System.Linq;
public static class C
{{
    public static System.Collections.Immutable.ImmutableArray<int> GetData() => default;
    public static void M()
    {{
        var a = 1.Equals(GetData().Length);
        var b = 1.Equals((GetData()).Length);
        var c = 1.Equals((GetData().Length));
    }}
}}
" ,
                    },
                },
            }.RunAsync();

        [Fact]
        public static Task Basic_AsMethodArgument_Tests()
            => new VerifyVB.Test
            {
                TestState =
                {
                    Sources=
                    {
                $@"Imports System
Imports System.Linq
Public Class Program
    Public Function GetData() As System.Collections.Immutable.ImmutableArray(Of Integer)
        Return Nothing
    End Function
    Public Sub M()
        Dim a = 1.Equals({{|{UseCountProperlyAnalyzer.CA1829}:GetData().Count()|}})
        Dim b = 1.Equals({{|{UseCountProperlyAnalyzer.CA1829}:(GetData()).Count()|}})
        Dim c = 1.Equals(({{|{UseCountProperlyAnalyzer.CA1829}:GetData().Count()|}}))
    End Sub
End Class
",
                    },
                },
                FixedState =
                {
                    Sources=
                    {
                $@"Imports System
Imports System.Linq
Public Class Program
    Public Function GetData() As System.Collections.Immutable.ImmutableArray(Of Integer)
        Return Nothing
    End Function
    Public Sub M()
        Dim a = 1.Equals(GetData().Length)
        Dim b = 1.Equals((GetData()).Length)
        Dim c = 1.Equals((GetData().Length))
    End Sub
End Class
" ,
                    },
                },
            }.RunAsync();

        [Fact]
        public static Task CSharp_ImmutableArray_Tests()
            => new VerifyCS.Test
            {
                TestState =
                {
                    Sources=
                    {
                $@"using System;
using System.Linq;
public static class C
{{
    public static System.Collections.Immutable.ImmutableArray<int> GetData() => default;
    public static int M() => {{|{UseCountProperlyAnalyzer.CA1829}:GetData().Count()|}};
    public static int N() => {{|{UseCountProperlyAnalyzer.CA1829}:GetData().Count()|}};
}}
",
                    },
                },
                FixedState =
                {
                    Sources=
                    {
                $@"using System;
using System.Linq;
public static class C
{{
    public static System.Collections.Immutable.ImmutableArray<int> GetData() => default;
    public static int M() => GetData().Length;
    public static int N() => GetData().Length;
}}
" ,
                    },
                },
            }.RunAsync();

        [Fact]
        public static Task Basic_ImmutableArray_Tests()
            => new VerifyVB.Test
            {
                TestState =
                {
                    Sources=
                    {
                $@"Imports System
Imports System.Linq
Public Module C
    Public Function GetData() As System.Collections.Immutable.ImmutableArray(Of Integer)
        Return Nothing
    End Function
    Public Function F() As Integer
        Return {{|{UseCountProperlyAnalyzer.CA1829}:GetData().Count()|}}
    End Function
    Public Function G() As Integer
        Return {{|{UseCountProperlyAnalyzer.CA1829}:GetData().Count()|}}
    End Function
End Module
",
                    },
                },
                FixedState =
                {
                    Sources=
                    {
                $@"Imports System
Imports System.Linq
Public Module C
    Public Function GetData() As System.Collections.Immutable.ImmutableArray(Of Integer)
        Return Nothing
    End Function
    Public Function F() As Integer
        Return GetData().Length
    End Function
    Public Function G() As Integer
        Return GetData().Length
    End Function
End Module
" ,
                    },
                },
            }.RunAsync();

        [Theory]
        [InlineData("string[]", nameof(Array.Length))]
        [InlineData("System.Collections.Immutable.ImmutableArray<int>", nameof(ImmutableArray<int>.Length))]
        [InlineData("System.Collections.Generic.List<int>", nameof(List<int>.Count))]
        [InlineData("System.Collections.Generic.IList<int>", nameof(IList<int>.Count))]
        [InlineData("System.Collections.Generic.ICollection<int>", nameof(ICollection<int>.Count))]
        public static Task CSharp_Fixed(string type, string propertyName)
            => VerifyCS.VerifyCodeFixAsync(
                $@"using System;
using System.Linq;
public static class C
{{
    public static {type} GetData() => default;
    public static int M() => GetData().Count();
}}
",
#pragma warning disable RS0030 // Do not used banned APIs
                VerifyCS.Diagnostic(UseCountProperlyAnalyzer.CA1829)
                    .WithLocation(6, 30)
#pragma warning restore RS0030 // Do not used banned APIs
                    .WithArguments(propertyName),
                $@"using System;
using System.Linq;
public static class C
{{
    public static {type} GetData() => default;
    public static int M() => GetData().{propertyName};
}}
");

        [Theory]
        [InlineData("string[]", nameof(Array.Length))]
        [InlineData("System.Collections.Immutable.ImmutableArray<int>?", nameof(ImmutableArray<int>.Length))]
        [InlineData("System.Collections.Generic.List<int>", nameof(List<int>.Count))]
        [InlineData("System.Collections.Generic.IList<int>", nameof(IList<int>.Count))]
        [InlineData("System.Collections.Generic.ICollection<int>", nameof(ICollection<int>.Count))]
        public static Task CSharp_Conditional_Fixed(string type, string propertyName)
            => VerifyCS.VerifyCodeFixAsync(
                $@"using System;
using System.Linq;
public static class C
{{
    public static {type} GetData() => default;
    public static int? M() => GetData()?.Count();
}}
",
#pragma warning disable RS0030 // Do not used banned APIs
                VerifyCS.Diagnostic(UseCountProperlyAnalyzer.CA1829)
                    .WithLocation(6, 41)
#pragma warning restore RS0030 // Do not used banned APIs
                    .WithArguments(propertyName),
                $@"using System;
using System.Linq;
public static class C
{{
    public static {type} GetData() => default;
    public static int? M() => GetData()?.{propertyName};
}}
");

        [Theory]
        [InlineData("string()", nameof(Array.Length))]
        [InlineData("System.Collections.Immutable.ImmutableArray(Of Integer)", nameof(ImmutableArray<int>.Length))]
        public static Task Basic_Fixed(string type, string propertyName)
            => VerifyVB.VerifyCodeFixAsync(
                $@"Imports System
Imports System.Linq
Public Module M
    Public Function GetData() As {type}
        Return Nothing
    End Function
    Public Function F() As Integer
        Return GetData().Count()
    End Function
End Module
",
#pragma warning disable RS0030 // Do not used banned APIs
                VerifyCS.Diagnostic(UseCountProperlyAnalyzer.CA1829)
                    .WithLocation(8, 16)
#pragma warning restore RS0030 // Do not used banned APIs
                    .WithArguments(propertyName),
                $@"Imports System
Imports System.Linq
Public Module M
    Public Function GetData() As {type}
        Return Nothing
    End Function
    Public Function F() As Integer
        Return GetData().{propertyName}
    End Function
End Module
");

        [Theory]
        [InlineData("string()", nameof(Array.Length))]
        [InlineData("System.Collections.Immutable.ImmutableArray(Of Integer)?", nameof(ImmutableArray<int>.Length))]
        public static Task Basic_Conditional_Fixed(string type, string propertyName)
            => VerifyVB.VerifyCodeFixAsync(
                $@"Imports System
Imports System.Linq
Public Module M
    Public Function GetData() As {type}
        Return Nothing
    End Function
    Public Function F() As Integer
        Return GetData()?.Count()
    End Function
End Module
",
#pragma warning disable RS0030 // Do not used banned APIs
                VerifyCS.Diagnostic(UseCountProperlyAnalyzer.CA1829)
                    .WithLocation(8, 26)
#pragma warning restore RS0030 // Do not used banned APIs
                    .WithArguments(propertyName),
                $@"Imports System
Imports System.Linq
Public Module M
    Public Function GetData() As {type}
        Return Nothing
    End Function
    Public Function F() As Integer
        Return GetData()?.{propertyName}
    End Function
End Module
");

        [Theory]
        [InlineData("System.Collections.Generic.IEnumerable<int>")]
        public static Task CSharp_NoDiagnostic(string type)
            => VerifyCS.VerifyAnalyzerAsync(
                $@"using System;
using System.Linq;
public static class C
{{
    public static {type} GetData() => default;
    public static int M() => GetData().Count();
}}
");

        [Theory]
        [InlineData("System.Collections.Generic.IEnumerable(Of Integer)")]
        public static Task Basic_NoDiagnostic(string type)
            => VerifyVB.VerifyAnalyzerAsync(
                $@"Imports System
Imports System.Linq
Public Module M
    Public Function GetData() As {type}
        Return Nothing
    End Function
    Public Function F() As Integer
        Return GetData().Count()
    End Function
End Module
");

        [Theory]
        [InlineData("System.Collections.Generic.List(Of Integer)")]
        [InlineData("System.Collections.Generic.IList(Of Integer)")]
        [InlineData("System.Collections.Generic.ICollection(Of Integer)")]
        public static Task Basic_PropertyInvocationWithParenthesis_NoDiagnostic(string type)
            => VerifyVB.VerifyAnalyzerAsync(
                $@"Imports System
Imports System.Linq
Public Module M
    Public Function GetData() As {type}
        Return Nothing
    End Function
    Public Function F() As Integer
        Return GetData().Count()
    End Function
End Module
");

        [Fact]
        public static Task CSharp_ICollectionOfTImplementerWithImplicitCount_Fixed()
            => VerifyCS.VerifyCodeFixAsync(
                $@"using System;
using System.Linq;
public class T : global::System.Collections.Generic.ICollection<string>
{{
    public int Count => throw new NotImplementedException();
    public bool IsReadOnly => throw new NotImplementedException();
    public void Add(string item) => throw new NotImplementedException();
    public void Clear() => throw new NotImplementedException();
    public bool Contains(string item) => throw new NotImplementedException();
    public void CopyTo(string[] array, int arrayIndex) => throw new NotImplementedException();
    public global::System.Collections.Generic.IEnumerator<string> GetEnumerator() => throw new NotImplementedException();
    public bool Remove(string item) => throw new NotImplementedException();
    global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => throw new NotImplementedException();
}}
public static class C
{{
    public static T GetData() => default;
    public static int M() => GetData().Count();
}}
",
#pragma warning disable RS0030 // Do not used banned APIs
                VerifyCS.Diagnostic(UseCountProperlyAnalyzer.CA1829)
                    .WithLocation(18, 30)
#pragma warning restore RS0030 // Do not used banned APIs
                    .WithArguments("Count"),
                $@"using System;
using System.Linq;
public class T : global::System.Collections.Generic.ICollection<string>
{{
    public int Count => throw new NotImplementedException();
    public bool IsReadOnly => throw new NotImplementedException();
    public void Add(string item) => throw new NotImplementedException();
    public void Clear() => throw new NotImplementedException();
    public bool Contains(string item) => throw new NotImplementedException();
    public void CopyTo(string[] array, int arrayIndex) => throw new NotImplementedException();
    public global::System.Collections.Generic.IEnumerator<string> GetEnumerator() => throw new NotImplementedException();
    public bool Remove(string item) => throw new NotImplementedException();
    global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => throw new NotImplementedException();
}}
public static class C
{{
    public static T GetData() => default;
    public static int M() => GetData().Count;
}}
");

        [Fact]
        public static Task CSharp_ICollectionImplementerWithImplicitCount_Fixed()
            => VerifyCS.VerifyCodeFixAsync(
                $@"using System;
using System.Linq;
public class T :
    global::System.Collections.Generic.IEnumerable<string>,
    global::System.Collections.ICollection
{{
    public int Count => throw new NotImplementedException();
    bool global::System.Collections.ICollection.IsSynchronized => throw new NotImplementedException();
    object global::System.Collections.ICollection.SyncRoot => throw new NotImplementedException();
    void global::System.Collections.ICollection.CopyTo(global::System.Array array, int arrayIndex) => throw new NotImplementedException();
    public global::System.Collections.Generic.IEnumerator<string> GetEnumerator() => throw new NotImplementedException();
    global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => throw new NotImplementedException();
}}
public static class C
{{
    public static T GetData() => default;
    public static int M() => GetData().Count();
}}
",
#pragma warning disable RS0030 // Do not used banned APIs
                VerifyCS.Diagnostic(UseCountProperlyAnalyzer.CA1829)
                    .WithLocation(17, 30)
#pragma warning restore RS0030 // Do not used banned APIs
                    .WithArguments("Count"),
                $@"using System;
using System.Linq;
public class T :
    global::System.Collections.Generic.IEnumerable<string>,
    global::System.Collections.ICollection
{{
    public int Count => throw new NotImplementedException();
    bool global::System.Collections.ICollection.IsSynchronized => throw new NotImplementedException();
    object global::System.Collections.ICollection.SyncRoot => throw new NotImplementedException();
    void global::System.Collections.ICollection.CopyTo(global::System.Array array, int arrayIndex) => throw new NotImplementedException();
    public global::System.Collections.Generic.IEnumerator<string> GetEnumerator() => throw new NotImplementedException();
    global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => throw new NotImplementedException();
}}
public static class C
{{
    public static T GetData() => default;
    public static int M() => GetData().Count;
}}
");

        [Fact]
        public static Task CSharp_ICollectionOfTImplementerWithExplicitCount_NoDiagnostic()
            => VerifyCS.VerifyAnalyzerAsync(
                $@"using System;
using System.Linq;
public class T : global::System.Collections.Generic.ICollection<string>
{{
    int global::System.Collections.Generic.ICollection<string>.Count => throw new NotImplementedException();
    bool global::System.Collections.Generic.ICollection<string>.IsReadOnly => throw new NotImplementedException();
    void global::System.Collections.Generic.ICollection<string>.Add(string item) => throw new NotImplementedException();
    void global::System.Collections.Generic.ICollection<string>.Clear() => throw new NotImplementedException();
    bool global::System.Collections.Generic.ICollection<string>.Contains(string item) => throw new NotImplementedException();
    void global::System.Collections.Generic.ICollection<string>.CopyTo(string[] array, int arrayIndex) => throw new NotImplementedException();
    public global::System.Collections.Generic.IEnumerator<string> GetEnumerator() => throw new NotImplementedException();
    global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => throw new NotImplementedException();
    bool global::System.Collections.Generic.ICollection<string>.Remove(string item) => throw new NotImplementedException();
}}
public static class C
{{
    public static T GetData() => default;
    public static int M() => GetData().Count();
}}
");

        [Fact]
        public static Task CSharp_InterfaceShadowingICollectionOfT_Fixed()
            => VerifyCS.VerifyCodeFixAsync(
                @"using System;
using System.Linq;
public interface I : global::System.Collections.Generic.ICollection<string>
{
    new int Count { get; }
}
public static class C
{
    public static I GetData() => default;
    public static int M() => GetData().Count();
}
",
#pragma warning disable RS0030 // Do not used banned APIs
                VerifyCS.Diagnostic(UseCountProperlyAnalyzer.CA1829)
                    .WithLocation(10, 30)
#pragma warning restore RS0030 // Do not used banned APIs
                    .WithArguments("Count"),
@"using System;
using System.Linq;
public interface I : global::System.Collections.Generic.ICollection<string>
{
    new int Count { get; }
}
public static class C
{
    public static I GetData() => default;
    public static int M() => GetData().Count;
}
");

        [Fact]
        public static Task CSharp_InterfaceShadowingICollection_Fixed()
            => VerifyCS.VerifyCodeFixAsync(
                @"using System;
using System.Linq;
public interface I :
    global::System.Collections.Generic.IEnumerable<string>,
    global::System.Collections.ICollection
{
    new int Count { get; }
}
public static class C
{
    public static I GetData() => default;
    public static int M() => GetData().Count();
}
",
#pragma warning disable RS0030 // Do not used banned APIs
                VerifyCS.Diagnostic(UseCountProperlyAnalyzer.CA1829)
                    .WithLocation(12, 30)
#pragma warning restore RS0030 // Do not used banned APIs
                    .WithArguments("Count"),
@"using System;
using System.Linq;
public interface I :
    global::System.Collections.Generic.IEnumerable<string>,
    global::System.Collections.ICollection
{
    new int Count { get; }
}
public static class C
{
    public static I GetData() => default;
    public static int M() => GetData().Count;
}
");

        [Fact]
        public static Task CSharp_ClassShadowingICollectionOfT_Fixed()
            => VerifyCS.VerifyCodeFixAsync(
                $@"using System;
using System.Linq;
public class T : global::System.Collections.Generic.ICollection<string>
{{
    public int Count => throw new NotImplementedException();
    int global::System.Collections.Generic.ICollection<string>.Count => throw new NotImplementedException();
    bool global::System.Collections.Generic.ICollection<string>.IsReadOnly => throw new NotImplementedException();
    void global::System.Collections.Generic.ICollection<string>.Add(string item) => throw new NotImplementedException();
    void global::System.Collections.Generic.ICollection<string>.Clear() => throw new NotImplementedException();
    bool global::System.Collections.Generic.ICollection<string>.Contains(string item) => throw new NotImplementedException();
    void global::System.Collections.Generic.ICollection<string>.CopyTo(string[] array, int arrayIndex) => throw new NotImplementedException();
    bool global::System.Collections.Generic.ICollection<string>.Remove(string item) => throw new NotImplementedException();
    public global::System.Collections.Generic.IEnumerator<string> GetEnumerator() => throw new NotImplementedException();
    global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => throw new NotImplementedException();
}}
public static class C
{{
    public static T GetData() => default;
    public static int M() => GetData().Count();
}}
",
#pragma warning disable RS0030 // Do not used banned APIs
                VerifyCS.Diagnostic(UseCountProperlyAnalyzer.CA1829)
                    .WithLocation(19, 30)
#pragma warning restore RS0030 // Do not used banned APIs
                    .WithArguments("Count"),
                $@"using System;
using System.Linq;
public class T : global::System.Collections.Generic.ICollection<string>
{{
    public int Count => throw new NotImplementedException();
    int global::System.Collections.Generic.ICollection<string>.Count => throw new NotImplementedException();
    bool global::System.Collections.Generic.ICollection<string>.IsReadOnly => throw new NotImplementedException();
    void global::System.Collections.Generic.ICollection<string>.Add(string item) => throw new NotImplementedException();
    void global::System.Collections.Generic.ICollection<string>.Clear() => throw new NotImplementedException();
    bool global::System.Collections.Generic.ICollection<string>.Contains(string item) => throw new NotImplementedException();
    void global::System.Collections.Generic.ICollection<string>.CopyTo(string[] array, int arrayIndex) => throw new NotImplementedException();
    bool global::System.Collections.Generic.ICollection<string>.Remove(string item) => throw new NotImplementedException();
    public global::System.Collections.Generic.IEnumerator<string> GetEnumerator() => throw new NotImplementedException();
    global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => throw new NotImplementedException();
}}
public static class C
{{
    public static T GetData() => default;
    public static int M() => GetData().Count;
}}
");

        [Fact]
        public static Task CSharp_ClassShadowingICollection_Fixed()
            => VerifyCS.VerifyCodeFixAsync(
                $@"using System;
using System.Linq;
public class T :
    global::System.Collections.Generic.IEnumerable<string>,
    global::System.Collections.ICollection
{{
    public int Count => throw new NotImplementedException();
    int global::System.Collections.ICollection.Count => throw new NotImplementedException();
    bool global::System.Collections.ICollection.IsSynchronized => throw new NotImplementedException();
    object global::System.Collections.ICollection.SyncRoot => throw new NotImplementedException();
    void global::System.Collections.ICollection.CopyTo(global::System.Array array, int arrayIndex) => throw new NotImplementedException();
    public global::System.Collections.Generic.IEnumerator<string> GetEnumerator() => throw new NotImplementedException();
    global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => throw new NotImplementedException();
}}
public static class C
{{
    public static T GetData() => default;
    public static int M() => GetData().Count();
}}
",
#pragma warning disable RS0030 // Do not used banned APIs
                VerifyCS.Diagnostic(UseCountProperlyAnalyzer.CA1829)
                    .WithLocation(18, 30)
#pragma warning restore RS0030 // Do not used banned APIs
                    .WithArguments("Count"),
                $@"using System;
using System.Linq;
public class T :
    global::System.Collections.Generic.IEnumerable<string>,
    global::System.Collections.ICollection
{{
    public int Count => throw new NotImplementedException();
    int global::System.Collections.ICollection.Count => throw new NotImplementedException();
    bool global::System.Collections.ICollection.IsSynchronized => throw new NotImplementedException();
    object global::System.Collections.ICollection.SyncRoot => throw new NotImplementedException();
    void global::System.Collections.ICollection.CopyTo(global::System.Array array, int arrayIndex) => throw new NotImplementedException();
    public global::System.Collections.Generic.IEnumerator<string> GetEnumerator() => throw new NotImplementedException();
    global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => throw new NotImplementedException();
}}
public static class C
{{
    public static T GetData() => default;
    public static int M() => GetData().Count;
}}
");

        [Fact, WorkItem(2974, "https://github.com/dotnet/roslyn-analyzers/issues/2974")]
        public static async Task CA1829_IReadOnlyCollection()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections.Generic;
using System.Linq;

public class SomeClass
{
    public IReadOnlyCollection<int> GetData() => null;
    public int M() => GetData().Count();
}",
#pragma warning disable RS0030 // Do not used banned APIs
                VerifyCS.Diagnostic(UseCountProperlyAnalyzer.CA1829)
                    .WithLocation(8, 23)
#pragma warning restore RS0030 // Do not used banned APIs
                    .WithArguments(nameof(IReadOnlyCollection<int>.Count)));
        }

        [Fact, WorkItem(3724, "https://github.com/dotnet/roslyn-analyzers/issues/3724")]
        public static async Task PropertyAccessParentIsNotAlwaysDirectlyTheInvocation()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
using System.Collections.Generic;
using System.Linq;

public class C
{
    public static bool IsChildPath(string parentPath, string childPath)
    {
        return (IsDirectorySeparator(childPath[parentPath.Length]) ||
            IsDirectorySeparator(childPath[{|CA1829:parentPath.Count()|}]));
    }

    public static bool IsDirectorySeparator(char c) => false;
}",
                FixedCode = @"
using System.Collections.Generic;
using System.Linq;

public class C
{
    public static bool IsChildPath(string parentPath, string childPath)
    {
        return (IsDirectorySeparator(childPath[parentPath.Length]) ||
            IsDirectorySeparator(childPath[parentPath.Length]));
    }

    public static bool IsDirectorySeparator(char c) => false;
}",
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestCode = @"
Imports System.Collections.Generic
Imports System.Linq

Public Class C
    Public Shared Function IsChildPath(parentPath As String, childPath As String) As Boolean
        Return (IsDirectorySeparator(childPath(parentPath.Length)) OrElse IsDirectorySeparator(childPath({|CA1829:parentPath.Count()|})))
    End Function

    Public Shared Function IsDirectorySeparator(c As Char) As Boolean
        Return False
    End Function
End Class
",
                FixedCode = @"
Imports System.Collections.Generic
Imports System.Linq

Public Class C
    Public Shared Function IsChildPath(parentPath As String, childPath As String) As Boolean
        Return (IsDirectorySeparator(childPath(parentPath.Length)) OrElse IsDirectorySeparator(childPath(parentPath.Length)))
    End Function

    Public Shared Function IsDirectorySeparator(c As Char) As Boolean
        Return False
    End Function
End Class
",
            }.RunAsync();
        }
    }

    public abstract class UsePropertyInsteadOfCountMethodWhenAvailableOverlapTests
        : DoNotUseCountWhenAnyCanBeUsedTestsBase
    {
        protected UsePropertyInsteadOfCountMethodWhenAvailableOverlapTests(TestsSourceCodeProvider sourceProvider, VerifierBase verifier)
            : base(sourceProvider, verifier) { }

        [Fact]
        public Task CountEqualsNonZero_WithoutPredicate_Fixed()
            => VerifyAsync(
                methodName: SourceProvider.MemberName,
                testSource: SourceProvider.GetCodeWithExpression(
                    SourceProvider.GetTargetExpressionEqualsInvocationCode(1, withPredicate: false, "Count"),
                    SourceProvider.ExtensionsNamespace),
                fixedSource: SourceProvider.GetCodeWithExpression(
                    SourceProvider.GetTargetPropertyEqualsInvocationCode(1, SourceProvider.MemberName),
                    SourceProvider.ExtensionsNamespace),
                extensionsSource: null);

        [Fact(Skip = "https://github.com/dotnet/roslyn-analyzers/issues/3700"), WorkItem(3700, "https://github.com/dotnet/roslyn-analyzers/issues/3700")]
        public Task NonZeroEqualsCount_WithoutPredicate_Fixed()
            => VerifyAsync(
                methodName: SourceProvider.MemberName,
                testSource: SourceProvider.GetCodeWithExpression(
                    SourceProvider.GetEqualsTargetExpressionInvocationCode(1, withPredicate: false, "Count"),
                    SourceProvider.ExtensionsNamespace),
                fixedSource: SourceProvider.GetCodeWithExpression(
                    SourceProvider.GetEqualsTargetPropertyInvocationCode(1, SourceProvider.MemberName),
                    SourceProvider.ExtensionsNamespace),
                extensionsSource: null,
                line: 10, column: 30);

        public static readonly IEnumerable<object[]> NoDiagnosisOnlyTestData = new BinaryExpressionTestData()
            .Where(x => (bool)x[0])
            .Select(x => new object[] { x[1], x[2], x[3] });

        [Theory]
        // Scenarios that are not diagnosed with CA1836 should fallback in CA1829.
        [MemberData(nameof(NoDiagnosisOnlyTestData))]
        public Task PropertyOnBinaryOperation(int literal, BinaryOperatorKind @operator, bool isRightSideExpression)
        {
            string testSource;
            string fixedSource;
            if (isRightSideExpression)
            {
                testSource = SourceProvider.GetTargetExpressionBinaryExpressionCode(literal, @operator, withPredicate: false, "Count");
                fixedSource = SourceProvider.GetTargetPropertyBinaryExpressionCode(literal, @operator, SourceProvider.MemberName);
            }
            else
            {
                testSource = SourceProvider.GetTargetExpressionBinaryExpressionCode(@operator, literal, withPredicate: false, "Count");
                fixedSource = SourceProvider.GetTargetPropertyBinaryExpressionCode(@operator, literal, SourceProvider.MemberName);
            }

            testSource = SourceProvider.GetCodeWithExpression(
                testSource, additionalNamspaces: SourceProvider.ExtensionsNamespace);

            fixedSource = SourceProvider.GetCodeWithExpression(
                fixedSource, additionalNamspaces: SourceProvider.ExtensionsNamespace);

            int line = VerifierBase.GetNumberOfLines(testSource) - 3;
            int column = isRightSideExpression ?
                21 + 3 + GetOperatorLength(SourceProvider, @operator) :
                21;

            return VerifyAsync(SourceProvider.MemberName, testSource, fixedSource, extensionsSource: null, line, column);
        }

        private static int GetOperatorLength(TestsSourceCodeProvider sourceProvider, BinaryOperatorKind @operator)
        {
            switch (@operator)
            {
                case BinaryOperatorKind.GreaterThan:
                case BinaryOperatorKind.LessThan:
                    return 1;
                case BinaryOperatorKind.NotEquals:
                case BinaryOperatorKind.GreaterThanOrEqual:
                case BinaryOperatorKind.LessThanOrEqual:
                    return 2;
                case BinaryOperatorKind.Equals:
                    if (sourceProvider is CSharpTestsSourceCodeProvider)
                    {
                        return 2;
                    }
                    else
                    {
                        return 1;
                    }
                default:
                    return 0;
            }
        }
    }

    public class CSharpUsePropertyInsteadOfCountMethodWhenAvailableOverlapTests_Concurrent
        : UsePropertyInsteadOfCountMethodWhenAvailableOverlapTests
    {
        public CSharpUsePropertyInsteadOfCountMethodWhenAvailableOverlapTests_Concurrent()
            : base(
                  new CSharpTestsSourceCodeProvider(
                      "Count",
                      "global::System.Collections.Concurrent.ConcurrentBag<int>",
                      "System.Linq",
                      "Enumerable",
                      false),
                  new CSharpVerifier<UseCountProperlyAnalyzer, CSharpUsePropertyInsteadOfCountMethodWhenAvailableFixer>(UseCountProperlyAnalyzer.CA1829))
        { }
    }

    public class CSharpUsePropertyInsteadOfCountMethodWhenAvailableOverlapTests_Immutable
        : UsePropertyInsteadOfCountMethodWhenAvailableOverlapTests
    {
        public CSharpUsePropertyInsteadOfCountMethodWhenAvailableOverlapTests_Immutable()
            : base(
                  new CSharpTestsSourceCodeProvider(
                      "Length",
                      "global::System.Collections.Immutable.ImmutableArray<int>",
                      "System.Linq",
                      "Enumerable",
                      false),
                  new CSharpVerifier<UseCountProperlyAnalyzer, CSharpUsePropertyInsteadOfCountMethodWhenAvailableFixer>(UseCountProperlyAnalyzer.CA1829))
        { }
    }

    public class BasicUsePropertyInsteadOfCountMethodWhenAvailableOverlapTests_Immutable
        : UsePropertyInsteadOfCountMethodWhenAvailableOverlapTests
    {
        public BasicUsePropertyInsteadOfCountMethodWhenAvailableOverlapTests_Immutable()
            : base(
                  new BasicTestsSourceCodeProvider(
                      "Length",
                      "Global.System.Collections.Immutable.ImmutableArray(Of Integer)",
                      "System.Linq",
                      "Enumerable",
                      false),
                  new BasicVerifier<UseCountProperlyAnalyzer, BasicUsePropertyInsteadOfCountMethodWhenAvailableFixer>(UseCountProperlyAnalyzer.CA1829))
        { }
    }
}
