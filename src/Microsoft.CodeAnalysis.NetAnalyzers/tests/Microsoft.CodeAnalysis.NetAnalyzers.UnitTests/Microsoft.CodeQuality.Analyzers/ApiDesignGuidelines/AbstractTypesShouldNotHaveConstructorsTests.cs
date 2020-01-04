// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public partial class CA1012Tests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new AbstractTypesShouldNotHaveConstructorsAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new AbstractTypesShouldNotHaveConstructorsAnalyzer();
        }

        [Fact]
        public void TestCSPublicAbstractClass()
        {
            var code = @"
public abstract class C
{
    public C()
    {
    }
}
";
            VerifyCSharp(code, GetCA1012CSharpResultAt(2, 23, "C"));
        }

        [Fact]
        public void TestVBPublicAbstractClass()
        {
            var code = @"
Public MustInherit Class C
    Public Sub New()
    End Sub
End Class
";
            VerifyBasic(code, GetCA1012BasicResultAt(2, 26, "C"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void TestCSInternalAbstractClass()
        {
            var code = @"
abstract class C
{
    public C()
    {
    }
}
";
            VerifyCSharp(code);
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void TestVBInternalAbstractClass()
        {
            var code = @"
MustInherit Class C
    Public Sub New()
    End Sub
End Class
";
            VerifyBasic(code);
        }

        [Fact]
        public void TestCSAbstractClassWithProtectedConstructor()
        {
            var code = @"
public abstract class C
{
    protected C()
    {
    }
}
";
            VerifyCSharp(code);
        }

        [Fact]
        public void TestVBAbstractClassWithProtectedConstructor()
        {
            var code = @"
Public MustInherit Class C
    Protected Sub New()
    End Sub
End Class
";
            VerifyBasic(code);
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void TestCSNestedAbstractClassWithPublicConstructor1()
        {
            var code = @"
public struct C
{
    abstract class D
    {
        public D() { }
    }
}
";
            VerifyCSharp(code);
        }

        [Fact]
        public void TestVBNestedAbstractClassWithPublicConstructor1()
        {
            var code = @"
Public Structure C
    MustInherit Class D
        Public Sub New()
        End Sub
    End Class
End Structure
";
            VerifyBasic(code, GetCA1012BasicResultAt(3, 23, "D"));
        }

        [Fact]
        public void TestNestedAbstractClassWithPublicConstructor2()
        {
            var code = @"
public abstract class C
{
    public abstract class D
    {
        public D() { }
    }
}
";
            VerifyCSharp(code, GetCA1012CSharpResultAt(4, 27, "D"));
        }

        [Fact]
        public void TestVBNestedAbstractClassWithPublicConstructor2()
        {
            var code = @"
Public MustInherit Class C
   Protected Friend MustInherit Class D
        Sub New()
        End Sub
    End Class
End Class
";
            VerifyBasic(code, GetCA1012BasicResultAt(3, 39, "D"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void TestNestedAbstractClassWithPublicConstructor3()
        {
            var code = @"
internal abstract class C
{
    public abstract class D
    {
        public D() { }
    }
}
";
            VerifyCSharp(code);
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void TestVBNestedAbstractClassWithPublicConstructor3()
        {
            var code = @"
MustInherit Class C
   Public MustInherit Class D
        Sub New()
        End Sub
    End Class
End Class
";
            VerifyBasic(code);
        }

        internal static readonly string CA1012Message = MicrosoftCodeQualityAnalyzersResources.AbstractTypesShouldNotHaveConstructorsMessage;

        private static DiagnosticResult GetCA1012CSharpResultAt(int line, int column, string objectName)
        {
            return GetCSharpResultAt(line, column, AbstractTypesShouldNotHaveConstructorsAnalyzer.RuleId, string.Format(CA1012Message, objectName));
        }

        private static DiagnosticResult GetCA1012BasicResultAt(int line, int column, string objectName)
        {
            return GetBasicResultAt(line, column, AbstractTypesShouldNotHaveConstructorsAnalyzer.RuleId, string.Format(CA1012Message, objectName));
        }
    }
}
