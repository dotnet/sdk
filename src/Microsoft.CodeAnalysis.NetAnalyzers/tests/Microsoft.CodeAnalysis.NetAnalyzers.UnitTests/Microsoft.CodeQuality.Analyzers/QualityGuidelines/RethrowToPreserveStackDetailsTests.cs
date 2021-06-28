// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.RethrowToPreserveStackDetailsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.RethrowToPreserveStackDetailsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.UnitTests
{
    public class RethrowToPreserveStackDetailsTests
    {
        [Fact]
        public async Task CA2200_NoDiagnosticsForRethrow()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task CA2200_NoDiagnosticsForThrowAnotherException()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        [WorkItem(4280, "https://github.com/dotnet/roslyn-analyzers/issues/4280")]
        public async Task CA2200_NoDiagnosticsForThrowAnotherExceptionInWhenClause()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
public abstract class C
{
    public void M()
    {
        try
        {
        }
        catch (Exception ex) when (Map(ex, out Exception mappedEx))
        {
            throw mappedEx;
        }
    }
    protected abstract bool Map(Exception ex, out Exception ex2);
}
");
        }

        [Fact]
        [WorkItem(4280, "https://github.com/dotnet/roslyn-analyzers/issues/4280")]
        public async Task CA2200_NoDiagnosticsForThrowAnotherExceptionInWhenClauseWithoutVariableDeclarator()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public abstract class C
{
    public void M()
    {
        try
        {
        }
        catch (Exception) when (Map(out Exception ex))
        {
            throw ex;
        }
    }

    protected abstract bool Map(out Exception ex);
}
");
        }

        [Fact]
        public async Task CA2200_DiagnosticForThrowCaughtException()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
            [|throw e;|]
        }
    }

    void ThrowException()
    {
        throw new ArithmeticException();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Class Program
    Sub CatchAndRethrowExplicitly()

        Try
            Throw New ArithmeticException()
        Catch e As ArithmeticException
            [|Throw e|]
        End Try
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA2200_NoDiagnosticsForThrowCaughtReassignedException()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task CA2200_NoDiagnosticsForEmptyBlock()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task CA2200_NoDiagnosticsForThrowCaughtExceptionInAnotherScope()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
            [|throw e;|]
        }
    }

    void ThrowException()
    {
        throw new ArithmeticException();
    }
}
");
        }

        [Fact]
        public async Task CA2200_SingleDiagnosticForThrowCaughtExceptionInSpecificScope()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Class Program
    Sub CatchAndRethrowExplicitly()

        Try
            Throw New ArithmeticException()
        Catch e As ArithmeticException
            [|Throw e|]
        Catch e As Exception
            [|Throw e|]
        End Try
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA2200_MultipleDiagnosticsForThrowCaughtExceptionAtMultiplePlaces()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
            [|throw e;|]
        }
        catch (Exception e)
        {
            [|throw e;|]
        }
    }

    void ThrowException()
    {
        throw new ArithmeticException();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Class Program
    Sub CatchAndRethrowExplicitly()

        Try
            Throw New ArithmeticException()
        Catch e As ArithmeticException
            [|Throw e|]
        Catch e As Exception
            [|Throw e|]
        End Try
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA2200_NoDiagnosticForThrowOuterCaughtException()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
");

            await VerifyVB.VerifyAnalyzerAsync(@"
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
");
        }

        [Fact]
        public async Task CA2200_NoDiagnosticsForNestingWithCompileErrors()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
            catch ({|CS0160:ArithmeticException|})
            {
                try
                {
                    throw new ArithmeticException();
                }
                catch (ArithmeticException i)
                {
                    throw {|CS0103:e|};
                }
            }
        }
        catch (Exception e)
        {
            throw;
        }
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
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
                    Throw {|BC30451:ex|}
                End Try
            End Try
        Catch ex As Exception
            Throw
        End Try
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA2200_NoDiagnosticsForCatchWithoutIdentifier()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
            var finalException = new InvalidOperationException(""aaa"", exception);
            throw finalException;
        }
    }
}
");
        }

        [Fact]
        [WorkItem(2167, "https://github.com/dotnet/roslyn-analyzers/issues/2167")]
        public async Task CA2200_NoDiagnosticsForCatchWithoutArgument()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
            var finalException = new InvalidOperationException(""aaa"", exception);
            throw finalException;
        }
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Class Program
    Sub CatchAndRethrow(exception As Exception)
        Try
            
        Catch
            Dim finalException = new InvalidOperationException(""aaa"", exception)
            Throw finalException
        End Try
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA2200_DiagnosticsForThrowCaughtExceptionInLocalMethod()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Program
{
    void CatchAndRethrowExplicitly()
    {
        try
        {
        }
        catch (Exception e)
        {
            DoThrow();

            void DoThrow()
            {
                throw e;
            }
        }
    }
}
");
        }

        [Fact]
        public async Task CA2200_NoDiagnosticsForThrowCaughtExceptionInLocalMethodAfterReassignment()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Program
{
    void CatchAndRethrowExplicitly()
    {
        try
        {
        }
        catch (Exception e)
        {
            e = new ArgumentException();
            DoThrow();

            void DoThrow()
            {
                throw e; // This should not fire.
            }
        }
    }
}
");
        }

        [Fact]
        public async Task CA2200_NoDiagnosticsForThrowCaughtExceptionInActionOrFunc()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Program
{
    void CatchAndRethrowExplicitly()
    {
        try
        {
        }
        catch (Exception e)
        {
            Action DoThrowAction = () =>
            {
                throw e;
            };

            Func<int> DoThrowFunc = () =>
            {
                throw e;
            };


            DoThrowAction();
            var result = DoThrowFunc();
        }
    }
}
");
        }

        [Fact]
        public async Task CA2200_NoDiagnosticsForThrowVariable()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Program
{
    void M()
    {
        var e = new ArithmeticException();
        throw e;
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Class Program
    Sub M()
        Dim e = New ArithmeticException()
        Throw e
    End Sub
End Class
");
        }
    }
}
