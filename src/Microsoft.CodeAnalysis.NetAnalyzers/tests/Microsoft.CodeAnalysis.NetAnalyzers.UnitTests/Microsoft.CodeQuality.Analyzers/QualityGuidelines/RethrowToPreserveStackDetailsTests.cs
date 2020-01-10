// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeQuality.CSharp.Analyzers.QualityGuidelines;
using Microsoft.CodeQuality.VisualBasic.Analyzers.QualityGuidelines;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.UnitTests
{
    public partial class RethrowToPreserveStackDetailsTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new BasicRethrowToPreserveStackDetailsAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new CSharpRethrowToPreserveStackDetailsAnalyzer();
        }

        [Fact]
        public void CA2200_NoDiagnosticsForRethrow()
        {
            VerifyCSharp(@"
using System;

class Program
{
    void CatchAndRethrowImplicitly()
    {
        try
        {
            throw new ArithmeticException();
        }
        catch (ArithmeticException e)
        { 
            throw;
        }
    }
}
");

            VerifyBasic(@"
Imports System
Class Program
    Sub CatchAndRethrowExplicitly()
        Try
            Throw New ArithmeticException()
        Catch ex As Exception
            Throw
        End Try
    End Sub
End Class
");
        }

        [Fact]
        public void CA2200_NoDiagnosticsForThrowAnotherException()
        {
            VerifyCSharp(@"
using System;

class Program
{
    void CatchAndRethrowExplicitly()
    {
        try
        {
            throw new ArithmeticException();
            throw new Exception();
        }
        catch (ArithmeticException e)
        {
            var i = new Exception();
            throw i;
        }
    }
}
");

            VerifyBasic(@"
Imports System
Class Program
    Sub CatchAndRethrowExplicitly()
        Try
            Throw New ArithmeticException()
            Throw New Exception()
        Catch ex As Exception
            Dim i As New Exception()
            Throw i
        End Try
    End Sub
End Class
");
        }

        [Fact]
        public void CA2200_DiagnosticForThrowCaughtException()
        {
            VerifyCSharp(@"
using System;

class Program
{
    void CatchAndRethrowExplicitly()
    {
        try
        {
            ThrowException();
        }
        catch (ArithmeticException e)
        {
            throw e;
        }
    }

    void ThrowException()
    {
        throw new ArithmeticException();
    }
}
",
           GetCA2200CSharpResultAt(14, 13));

            VerifyBasic(@"
Imports System
Class Program
    Sub CatchAndRethrowExplicitly()

        Try
            Throw New ArithmeticException()
        Catch e As ArithmeticException
            Throw e
        End Try
    End Sub
End Class
",
            GetCA2200BasicResultAt(9, 13));
        }

        [Fact]
        public void CA2200_NoDiagnosticsForThrowCaughtReassignedException()
        {
            VerifyCSharp(@"
using System;

class Program
{
    void CatchAndRethrowExplicitlyReassigned()
    {
        try
        {
            ThrowException();
        }
        catch (SystemException e)
        { 
            e = new ArithmeticException();
            throw e;
        }
    }

    void ThrowException()
    {
        throw new SystemException();
    }
}
");
            VerifyBasic(@"
Imports System
Class Program
    Sub CatchAndRethrowExplicitly()

        Try
            Throw New Exception()
        Catch e As Exception
            e = New ArithmeticException()
            Throw e
        End Try
    End Sub
End Class
");
        }

        [Fact]
        public void CA2200_NoDiagnosticsForEmptyBlock()
        {
            VerifyCSharp(@"
using System;

class Program
{
    void CatchAndRethrowExplicitlyReassigned()
    {
        try
        {
            ThrowException();
        }
        catch (SystemException e)
        { 

        }
    }

    void ThrowException()
    {
        throw new SystemException();
    }
}
");
            VerifyBasic(@"
Imports System
Class Program
    Sub CatchAndRethrowExplicitly()

        Try
            Throw New Exception()
        Catch e As Exception

        End Try
    End Sub
End Class
");
        }

        [Fact]
        public void CA2200_NoDiagnosticsForThrowCaughtExceptionInAnotherScope()
        {
            VerifyCSharp(@"
using System;

class Program
{
    void CatchAndRethrowExplicitly()
    {
        try
        {
            ThrowException();
        }
        catch (ArithmeticException e)
        {
            throw e;
        }
    }

    [|void ThrowException()
    {
        throw new ArithmeticException();
    }|]
}
");
        }

        [Fact]
        public void CA2200_SingleDiagnosticForThrowCaughtExceptionInSpecificScope()
        {
            VerifyBasic(@"
Imports System
Class Program
    Sub CatchAndRethrowExplicitly()

        Try
            Throw New ArithmeticException()
        Catch e As ArithmeticException
            Throw e
        [|Catch e As Exception
            Throw e
        End Try|]
    End Sub
End Class
",
            GetCA2200BasicResultAt(11, 13));
        }

        [Fact]
        public void CA2200_MultipleDiagnosticsForThrowCaughtExceptionAtMultiplePlaces()
        {
            VerifyCSharp(@"
using System;

class Program
{
    void CatchAndRethrowExplicitly()
    {
        try
        {
            ThrowException();
        }
        catch (ArithmeticException e)
        {
            throw e;
        }
        catch (Exception e)
        {
            throw e;
        }
    }

    void ThrowException()
    {
        throw new ArithmeticException();
    }
}
",
            GetCA2200CSharpResultAt(14, 13),
            GetCA2200CSharpResultAt(18, 13));

            VerifyBasic(@"
Imports System
Class Program
    Sub CatchAndRethrowExplicitly()

        Try
            Throw New ArithmeticException()
        Catch e As ArithmeticException
            Throw e
        Catch e As Exception
            Throw e
        End Try
    End Sub
End Class
",
            GetCA2200BasicResultAt(9, 13),
            GetCA2200BasicResultAt(11, 13));
        }

        [Fact]
        public void CA2200_DiagnosticForThrowOuterCaughtException()
        {
            VerifyCSharp(@"
using System;

class Program
{
    void CatchAndRethrowExplicitly()
    {
        try
        {
            throw new ArithmeticException();
        }
        catch (ArithmeticException e)
        {
            try
            {
                throw new ArithmeticException();
            }
            catch (ArithmeticException i)
            {
                throw e;
            }
        }
    }
}
",
            GetCA2200CSharpResultAt(20, 17));

            VerifyBasic(@"
Imports System
Class Program
    Sub CatchAndRethrowExplicitly()

        Try
            Throw New ArithmeticException()
        Catch e As ArithmeticException
            Try
                Throw New ArithmeticException()
            Catch ex As Exception
                Throw e
            End Try
        End Try
    End Sub
End Class
",
            GetCA2200BasicResultAt(12, 17));
        }

        [Fact]
        public void CA2200_NoDiagnosticsForNestingWithCompileErrors()
        {
            VerifyCSharp(@"
using System;

class Program
{
    void CatchAndRethrowExplicitly()
    {   
        try
        {
            try
            {
                throw new ArithmeticException();
            }
            catch (ArithmeticException e)
            {
                throw;
            }
            catch (ArithmeticException)   // error CS0160: A previous catch clause already catches all exceptions of this or of a super type ('ArithmeticException')
            {
                try
                {
                    throw new ArithmeticException();
                }
                catch (ArithmeticException i)
                {
                    throw e;   // error CS0103: The name 'e' does not exist in the current context
                }
            }
        }
        catch (Exception e)
        {
            throw;
        }
    }
}
", TestValidationMode.AllowCompileErrors);

            VerifyBasic(@"
Imports System
Class Program
    Sub CatchAndRethrowExplicitly()
        Try
            Try
                Throw New ArithmeticException()
            Catch ex As ArithmeticException
                Throw
            Catch i As ArithmeticException
                Try
                    Throw New ArithmeticException()
                Catch e As Exception
                    Throw ex   ' error BC30451: 'ex' is not declared. It may be inaccessible due to its protection level.
                End Try
            End Try
        Catch ex As Exception
            Throw
        End Try
    End Sub
End Class
", TestValidationMode.AllowCompileErrors);
        }

        [Fact]
        public void CA2200_NoDiagnosticsForCatchWithoutIdentifier()
        {
            VerifyCSharp(@"
using System;

class Program
{
    void CatchAndRethrow(Exception exception)
    {
        try
        {            
        }
        catch (Exception)
        { 
            var finalException = new InvalidOperationException(""barf"", exception);
            throw finalException;
        }
    }
}
");
        }

        [Fact]
        [WorkItem(2167, "https://github.com/dotnet/roslyn-analyzers/issues/2167")]
        public void CA2200_NoDiagnosticsForCatchWithoutArgument()
        {
            VerifyCSharp(@"
using System;

class Program
{
    void CatchAndRethrow(Exception exception)
    {
        try
        {            
        }
        catch
        { 
            var finalException = new InvalidOperationException(""barf"", exception);
            throw finalException;
        }
    }
}
");

            VerifyBasic(@"
Imports System
Class Program
    Sub CatchAndRethrow(exception As Exception)
        Try
            
        Catch
            Dim finalException = new InvalidOperationException(""barf"", exception)
            Throw finalException
        End Try
    End Sub
End Class
");
        }

        private static DiagnosticResult GetCA2200BasicResultAt(int line, int column)
        {
            return GetBasicResultAt(line, column, RethrowToPreserveStackDetailsAnalyzer.Rule);
        }

        private static DiagnosticResult GetCA2200CSharpResultAt(int line, int column)
        {
            return GetCSharpResultAt(line, column, RethrowToPreserveStackDetailsAnalyzer.Rule);
        }
    }
}
