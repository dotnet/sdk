// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class DoNotDeclareVisibleInstanceFieldsTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new DoNotDeclareVisibleInstanceFieldsAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new DoNotDeclareVisibleInstanceFieldsAnalyzer();
        }

        [Fact]
        public void CSharp_PublicVariable_PublicContainingType()
        {
            VerifyCSharp(@"
public class A
{
    public string field; 
}", GetCSharpResultAt(4, 19, DoNotDeclareVisibleInstanceFieldsAnalyzer.Rule));
        }

        [Fact]
        public void VisualBasic_PublicVariable_PublicContainingType()
        {
            VerifyBasic(@"
Public Class A
    Public field As System.String
End Class", GetBasicResultAt(3, 12, DoNotDeclareVisibleInstanceFieldsAnalyzer.Rule));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void CSharp_PublicVariable_InternalContainingType()
        {
            VerifyCSharp(@"
internal class A
{
    public string field; 

    public class B
    {
        public string field; 
    }
}");
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void VisualBasic_PublicVariable_InternalContainingType()
        {
            VerifyBasic(@"
Friend Class A
    Public field As System.String

    Public Class B
        Public field As System.String
    End Class
End Class
");
        }

        [Fact]
        public void CSharp_DefaultVisibility()
        {
            VerifyCSharp(@"
public class A
{
    string field; 
}");
        }

        [Fact]
        public void VisualBasic_DefaultVisibility()
        {
            VerifyBasic(@"
Public Class A
    Dim field As System.String
End Class");
        }

        [Fact]
        public void CSharp_PublicStaticVariable()
        {
            VerifyCSharp(@"
public class A
{
    public static string field; 
}");
        }

        [Fact]
        public void VisualBasic_PublicStaticVariable()
        {
            VerifyBasic(@"
Public Class A
    Public Shared field as System.String
End Class");
        }

        [Fact]
        public void CSharp_PublicStaticReadonlyVariable()
        {
            VerifyCSharp(@"
public class A
{
    public static readonly string field; 
}");
        }

        [Fact]
        public void VisualBasic_PublicStaticReadonlyVariable()
        {
            VerifyBasic(@"
Public Class A
    Public Shared ReadOnly field as System.String
End Class");
        }

        [Fact]
        public void CSharp_PublicConstVariable()
        {
            VerifyCSharp(@"
public class A
{
    public const string field = ""X""; 
}");
        }

        [Fact]
        public void VisualBasic_PublicConstVariable()
        {
            VerifyBasic(@"
Public Class A
    Public Const field as System.String = ""X""
End Class");
        }

        [Fact]
        public void CSharp_ProtectedVariable_PublicContainingType()
        {
            VerifyCSharp(@"
public class A
{
    protected string field;
}", GetCSharpResultAt(4, 22, DoNotDeclareVisibleInstanceFieldsAnalyzer.Rule));
        }

        [Fact]
        public void VisualBasic_ProtectedVariable_PublicContainingType()
        {
            VerifyBasic(@"
Public Class A
    Protected field As System.String
End Class", GetBasicResultAt(3, 15, DoNotDeclareVisibleInstanceFieldsAnalyzer.Rule));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void CSharp_ProtectedVariable_InternalContainingType()
        {
            VerifyCSharp(@"
        internal class A
        {
            protected string field; 

            public class B
            {
                protected string field; 
            }
        }");
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void VisualBasic_ProtectedVariable_InternalContainingType()
        {
            VerifyBasic(@"
        Friend Class A
            Protected field As System.String

            Public Class B
                Protected field As System.String
            End Class
        End Class
        ");
        }

        [Fact]
        public void CSharp_ProtectedStaticVariable()
        {
            VerifyCSharp(@"
        public class A
        {
            protected static string field; 
        }");
        }

        [Fact]
        public void VisualBasic_ProtectedStaticVariable()
        {
            VerifyBasic(@"
        Public Class A
            Protected Shared field as System.String
        End Class");
        }

        [Fact]
        public void CSharp_ProtectedStaticReadonlyVariable()
        {
            VerifyCSharp(@"
        public class A
        {
            protected static readonly string field; 
        }");
        }

        [Fact]
        public void VisualBasic_ProtectedStaticReadonlyVariable()
        {
            VerifyBasic(@"
        Public Class A
            Protected Shared ReadOnly field as System.String
        End Class");
        }

        [Fact]
        public void CSharp_ProtectedConstVariable()
        {
            VerifyCSharp(@"
        public class A
        {
            protected const string field = ""X""; 
        }");
        }

        [Fact]
        public void VisualBasic_ProtectedConstVariable()
        {
            VerifyBasic(@"
        Public Class A
            Protected Const field as System.String = ""X""
        End Class");
        }

        [Fact]
        public void CSharp_ProtectedInternalVariable_PublicContainingType()
        {
            VerifyCSharp(@"
public class A
{
    protected internal string field;
}", GetCSharpResultAt(4, 31, DoNotDeclareVisibleInstanceFieldsAnalyzer.Rule));
        }

        [Fact]
        public void VisualBasic_ProtectedFriendVariable_PublicContainingType()
        {
            VerifyBasic(@"
Public Class A
    Protected Friend field As System.String
End Class", GetBasicResultAt(3, 22, DoNotDeclareVisibleInstanceFieldsAnalyzer.Rule));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void CSharp_ProtectedInternalVariable_InternalContainingType()
        {
            VerifyCSharp(@"
        internal class A
        {
            protected internal string field; 

            public class B
            {
                protected internal string field; 
            }
        }");
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void VisualBasic_ProtectedFriendVariable_InternalContainingType()
        {
            VerifyBasic(@"
        Friend Class A
            Protected Friend field As System.String

            Public Class B
                Protected Friend field As System.String
            End Class
        End Class
        ");
        }

        [Fact]
        public void CSharp_ProtectedInternalStaticVariable()
        {
            VerifyCSharp(@"
        public class A
        {
            protected internal static string field; 
        }");
        }

        [Fact]
        public void VisualBasic_ProtectedFriendStaticVariable()
        {
            VerifyBasic(@"
        Public Class A
            Protected Friend Shared field as System.String
        End Class");
        }

        [Fact]
        public void CSharp_ProtectedInternalStaticReadonlyVariable()
        {
            VerifyCSharp(@"
        public class A
        {
            protected internal static readonly string field; 
        }");
        }

        [Fact]
        public void VisualBasic_ProtectedFriendStaticReadonlyVariable()
        {
            VerifyBasic(@"
        Public Class A
            Protected Friend Shared ReadOnly field as System.String
        End Class");
        }

        [Fact]
        public void CSharp_ProtectedInternalConstVariable()
        {
            VerifyCSharp(@"
        public class A
        {
            protected internal const string field = ""X""; 
        }");
        }

        [Fact]
        public void VisualBasic_ProtectedFriendConstVariable()
        {
            VerifyBasic(@"
        Public Class A
            Protected Friend Const field as System.String = ""X""
        End Class");
        }
    }
}