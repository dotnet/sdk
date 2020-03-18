// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotExposeGenericLists,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotExposeGenericLists,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class DoNotExposeGenericListsTests
    {
        [Fact]
        public async Task CA1002_Fields()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections.Generic;

namespace Namespace1
{
    public class Class1
    {
        public List<int> publicField;
        private List<int> privateField;
    }

    public struct Struct1
    {
        public List<int> publicField;
        private List<int> privateField;
    }
}",
                GetCSharpExpectedResult(8, 26, "List<int>", "Class1.publicField"),
                GetCSharpExpectedResult(14, 26, "List<int>", "Struct1.publicField"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Collections.Generic

Namespace Namespace1
    Public Class Class1
        Public publicField As List(Of Integer)
        Private privateField As List(Of Integer)
    End Class

    Public Structure Struct1
        Public publicField As List(Of Integer)
        Private privateField As List(Of Integer)
    End Structure
End Namespace",
                GetBasicExpectedResult(6, 16, "List(Of Integer)", "Class1.publicField"),
                GetBasicExpectedResult(11, 16, "List(Of Integer)", "Struct1.publicField"));
        }

        [Fact]
        public async Task CA1002_Methods()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections.Generic;

namespace Namespace1
{
    public interface Interface1
    {
        List<int> {|CA1002:ReturnsList|}();
    }

    public class BaseClass
    {
        public virtual void TakesList(List<string> {|CA1002:list|}) {}
    }

    public class Class1 : BaseClass, Interface1
    {
        public List<int> ReturnsList() => null; // no issue as this is an inteface implementation
        public override void TakesList(List<string> list) { } // no issue this is an override

        public List<int> {|CA1002:MultiIssues|}(List<string> {|CA1002:list|}, List<List<string>> {|CA1002:listOfList|}) => null;

        private List<int> PrivateReturnsList() => null;
    }

    public struct Struct1 : Interface1
    {
        public List<int> ReturnsList() => null;

        public List<double> {|CA1002:GetList|}() => null;
        private List<double> PrivateGetList() => null;
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Collections.Generic

Namespace Namespace1
    Public Interface Interface1
        Function {|CA1002:ReturnsList|}() As List(Of Integer)
    End Interface

    Public Class BaseClass
        Public Overridable Sub TakesList(ByVal {|CA1002:list|} As List(Of String))
        End Sub
    End Class

    Public Class Class1
        Inherits BaseClass
        Implements Interface1

        Public Function ReturnsList() As List(Of Integer) Implements Interface1.ReturnsList ' no issue as this is an inteface implementation
            Return Nothing
        End Function

        Public Overrides Sub TakesList(ByVal list As List(Of String)) ' no issue this is an override
        End Sub

        Public Function {|CA1002:MultiIssues|}(ByVal {|CA1002:list|} As List(Of String), ByVal {|CA1002:listOfList|} As List(Of List(Of String))) As List(Of Integer)
            Return Nothing
        End Function

        Private Function PrivateReturnsList() As List(Of Integer)
            Return Nothing
        End Function
    End Class

    Public Structure Struct1
        Implements Interface1

        Public Function ReturnsList() As List(Of Integer) Implements Interface1.ReturnsList
            Return Nothing
        End Function

        Public Function {|CA1002:GetList|}() As List(Of Double)
            Return Nothing
        End Function

        Private Function PrivateGetList() As List(Of Double)
            Return Nothing
        End Function
    End Structure
End Namespace
");
        }

        [Fact]
        public async Task CA1002_Properties()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections.Generic;

namespace Namespace1
{
    public interface Interface1
    {
        List<int> {|CA1002:InterfaceProperty|} { get; }
    }

    public class BaseClass
    {
        public virtual List<string> {|CA1002:VirtualProperty|} { get; }
    }

    public class Class1 : BaseClass, Interface1
    {
        public List<int> InterfaceProperty { get; } // no issue as this is an inteface implementation
        public override List<string> VirtualProperty { get; } // no issue this is an override

        public List<int> {|CA1002:AutoProperty|} { get; set; }
        public List<int> {|CA1002:ReadonlyAutoProperty|} { get; }
        public List<int> {|CA1002:ReadonlyArrowProperty|} => null;

        private List<int> PrivateAutoProperty { get; set; }
    }

    public struct Struct1 : Interface1
    {
        public List<int> InterfaceProperty { get; }

        public List<int> {|CA1002:ReadonlyArrowProperty|} => null;
        private List<int> PrivateAutoProperty { get; set; }
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Collections.Generic

Namespace Namespace1
    Public Interface Interface1
        ReadOnly Property {|CA1002:InterfaceProperty|} As List(Of Integer)
    End Interface

    Public Class BaseClass
        Public Overridable ReadOnly Property {|CA1002:VirtualProperty|} As List(Of String)
    End Class

    Public Class Class1
        Inherits BaseClass
        Implements Interface1

        Public ReadOnly Property InterfaceProperty As List(Of Integer) Implements Interface1.InterfaceProperty ' no issue as this is an inteface implementation
        Public Overrides ReadOnly Property VirtualProperty As List(Of String) ' no issue this is an override

        Public Property {|CA1002:AutoProperty|} As List(Of Integer)
        Public ReadOnly Property {|CA1002:ReadonlyAutoProperty|} As List(Of Integer)

        Private Property PrivateAutoProperty As List(Of Integer)
    End Class

    Public Structure Struct1
        Implements Interface1

        Public ReadOnly Property InterfaceProperty As List(Of Integer) Implements Interface1.InterfaceProperty

        Public ReadOnly Property {|CA1002:ReadonlyAutoProperty|} As List(Of Integer)

        Private Property PrivateAutoProperty As List(Of Integer)
    End Structure
End Namespace
");
        }

        [Fact]
        public async Task CA1002_ExtensionMethods()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections.Generic;

namespace Namespace1
{
    public static class Helper
    {
        public static bool Ext1(this List<int> list) => true;
        public static List<int> {|CA1002:Ext2|}(this string s) => null;
        public static int Ext3(this bool b, List<string> {|CA1002:l|}) => 42;
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Collections.Generic
Imports System.Runtime.CompilerServices

Namespace Namespace1
    Public Module Helper
        <Extension()>
        Public Sub Ext1(ByVal list As List(Of String))
        End Sub

        <Extension()>
        Public Function {|CA1002:Ext2|}(ByVal s As String) As List(Of Integer)
            Return Nothing
        End Function

        <Extension()>
        Public Function Ext2(ByVal s As String, ByVal {|CA1002:list|} As List(Of Integer)) As Integer
            Return 42
        End Function
    End Module
End Namespace
");
        }

        private static DiagnosticResult GetCSharpExpectedResult(int line, int col, string returnTypeName, string typeDotMemberName)
            => VerifyCS.Diagnostic()
                .WithLocation(line, col)
                .WithArguments(returnTypeName, typeDotMemberName);

        private static DiagnosticResult GetBasicExpectedResult(int line, int col, string returnTypeName, string typeDotMemberName)
            => VerifyVB.Diagnostic()
                .WithLocation(line, col)
                .WithArguments(returnTypeName, typeDotMemberName);
    }
}
