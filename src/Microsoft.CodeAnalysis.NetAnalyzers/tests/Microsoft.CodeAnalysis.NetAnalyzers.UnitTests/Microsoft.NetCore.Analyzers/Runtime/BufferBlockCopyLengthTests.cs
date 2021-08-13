// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information. 

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.BufferBlockCopyLengthAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.BufferBlockCopyLengthAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class BufferBlockCopyLengthTests
    {
        [Theory]
        [InlineData("src")]
        [InlineData("dst")]
        public async Task UsingByteArray(string array)
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Program
{
    static void Main()
    {
        byte[] src = new byte[] {1, 2, 3, 4};
        byte[] dst = new byte[] {0, 0, 0, 0};
        
        Buffer.BlockCopy(src, 0, dst, 0, " + array + @".Length);
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Module Program
    Sub Main(args As String())
        Dim src = New Byte() {1, 2, 3, 4}
        Dim dst = New Byte() {0, 0, 0, 0}

        Buffer.BlockCopy(src, 0, dst, 0, " + array + @".Length)
    End Sub
End Module
");
        }

        [Theory]
        [InlineData("src")]
        [InlineData("dst")]
        public async Task UsingSbyteArray(string array)
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Program
{
    static void Main()
    {
        sbyte[] src = new sbyte[] {1, 2, 3, 4};
        sbyte[] dst = new sbyte[] {0, 0, 0, 0};
        
        Buffer.BlockCopy(src, 0, dst, 0, " + array + @".Length);
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Module Program
    Sub Main(args As String())
        Dim src = New SByte() {1, 2, 3, 4}
        Dim dst = New SByte() {0, 0, 0, 0}

        Buffer.BlockCopy(src, 0, dst, 0, " + array + @".Length)
    End Sub
End Module
");
        }

        [Theory]
        [InlineData("src")]
        [InlineData("dst")]
        public async Task UsingIntArray(string array)
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Program
{
    static void Main()
    {
        int[] src = new int[] {1, 2, 3, 4};
        int[] dst = new int[] {0, 0, 0, 0};
        
        Buffer.BlockCopy(src, 0, dst, 0, [|" + array + @".Length|]);
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Module Program
    Sub Main(args As String())
        Dim src = New Integer() {1, 2, 3, 4}
        Dim dst = New Integer() {0, 0, 0, 0}

        Buffer.BlockCopy(src, 0, dst, 0, [|" + array + @".Length|])
    End Sub
End Module
");
        }

        [Fact]
        public async Task OperandIsNotSrcOrDst()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Program
{
    static void Main()
    {
        int[] src = new int[] {1, 2, 3, 4};
        int[] dst = new int[] {0, 0, 0, 0};
        int[] test = new int[] {5, 6, 7, 8};
        
        Buffer.BlockCopy(src, 0, dst, 0, test.Length);
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Module Program
    Sub Main(args As String())
        Dim src = New Integer() {1, 2, 3, 4}
        Dim dst = New Integer() {0, 0, 0, 0}
        Dim test = New Integer() {5, 6, 7, 8}

        Buffer.BlockCopy(src, 0, dst, 0, test.Length)
    End Sub
End Module
");
        }

        [Theory]
        [InlineData("src")]
        [InlineData("dst")]
        public async Task SrcNumOfBytesAsReference(string array)
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Program
{
    static void Main()
    {
        int[] src = new int[] {1, 2, 3, 4};
        int[] dst = new int[] {0, 0, 0, 0};
        int numOfBytes = " + array + @".Length;
        
        Buffer.BlockCopy(src, 0, dst, 0, [|numOfBytes|]);
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Module Program
    Sub Main(args As String())
        Dim src = New Integer() {1, 2, 3, 4}
        Dim dst = New Integer() {0, 0, 0, 0}
        Dim numOfBytes = " + array + @".Length

        Buffer.BlockCopy(src, 0, dst, 0, [|numOfBytes|])
    End Sub
End Module
");
        }

        [Fact]
        public async Task NumOfBytesAsLiteralConst()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Program
{
    static void Main()
    {
        int[] src = new int[] {1, 2, 3, 4};
        int[] dst = new int[] {0, 0, 0, 0};
        
        Buffer.BlockCopy(src, 0, dst, 0, 8);
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Module Program
    Sub Main(args As String())
        Dim src = New Integer() {1, 2, 3, 4}
        Dim dst = New Integer() {0, 0, 0, 0}

        Buffer.BlockCopy(src, 0, dst, 0, 8)
    End Sub
End Module
");
        }

        [Fact]
        public async Task NumOfBytesAsMultipleDeclarations()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Program
{
    static void Main()
    {
        int[] src = new int[] {1, 2, 3, 4};
        int[] dst = new int[] {0, 0, 0, 0};
        int test = 4, numOfBytes = src.Length;

        Buffer.BlockCopy(src, 0, dst, 0, [|numOfBytes|]);
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Module Program
    Sub Main(args As String())
        Dim src = New Integer() {1, 2, 3, 4}
        Dim dst = New Integer() {0, 0, 0, 0}
        Dim test = 4, numOfBytes = src.Length

        Buffer.BlockCopy(src, 0, dst, 0, [|numOfBytes|])
    End Sub
End Module
");
        }

        [Fact]
        public async Task NumOfBytesAsConstLiteral()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Program
{
    static void Main()
    {
        int[] src = new int[] {1, 2, 3, 4};
        int[] dst = new int[] {0, 0, 0, 0};
        int numOfBytes = 8;

        Buffer.BlockCopy(src, 0, dst, 0, numOfBytes);
    }
}
");
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Module Program
    Sub Main(args As String())
        Dim src = New Integer() {1, 2, 3, 4}
        Dim dst = New Integer() {0, 0, 0, 0}
        Dim numOfBytes = 8

        Buffer.BlockCopy(src, 0, dst, 0, numOfBytes)
    End Sub
End Module
");
        }

        [Fact]
        public async Task NumOfBytesAsClassConstField()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Program
{
    private const int field = 3;
    static void Main()
    {
        int[] src = new int[] {1, 2, 3, 4};
        int[] dst = new int[] {0, 0, 0, 0};

        Buffer.BlockCopy(src, 0, dst, 0, Program.field);
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Module Program
    Private Const field As Integer = 8
    Sub Main(args As String())
        Dim src = New Integer() {1, 2, 3, 4}
        Dim dst = New Integer() {0, 0, 0, 0}

        Buffer.BlockCopy(src, 0, dst, 0, Program.field)
    End Sub
End Module
");
        }

        [Fact]
        public async Task NumOfBytesAsClassPropertyGetter()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Program
{
    private const int field = 8;
    public int Field => field;
    static void Main()
    {
        int[] src = new int[] {1, 2, 3, 4};
        int[] dst = new int[] {0, 0, 0, 0};
        Program p = new Program();

        Buffer.BlockCopy(src, 0, dst, 0, p.Field);
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class Program
    Private Property Field As Integer = 8
    Public Shared Sub Main(args As String())
        Dim src = New Integer() {1, 2, 3, 4}
        Dim dst = New Integer() {0, 0, 0, 0}
        Dim p As Program = New Program()

        Buffer.BlockCopy(src, 0, dst, 0, p.Field)
    End Sub
End Class

");
        }

        [Fact]
        public async Task NumOfBytesAsMethodInvocation()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Program
{
    static void Main()
    {
        int[] src = new int[] {1, 2, 3, 4};
        int[] dst = new int[] {0, 0, 0, 0};
        
        Buffer.BlockCopy(src, 0, dst, 0, GetNumOfBytes());
    }
    
    static int GetNumOfBytes()
    {
        return 8;
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Module Program
    Sub Main(args As String())
        Dim src = New Integer() {1, 2, 3, 4}
        Dim dst = New Integer() {0, 0, 0, 0}

        Buffer.BlockCopy(src, 0, dst, 0, GetNumOfBytes())
    End Sub

    Function GetNumOfBytes()
        Return 8
    End Function
End Module
");
        }

        [Fact]
        public async Task SrcAndDstAsLiteralArrays()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Program
{
    static void Main()
    {
        Buffer.BlockCopy(new int[] {1, 2, 3, 4}, 0, new int[] {0, 0, 0, 0}, 0, 8);
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Module Program
    Sub Main(args As String())
        Buffer.Blockcopy(new Integer() {1, 2, 3, 4}, 0, new Integer() {0, 0, 0, 0}, 0, 8)
    End Sub
End Module
");
        }

        [Fact]
        public async Task NamedArgumentsNotInOrder()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Program
{
    static void Main()
    {
        int[] src = new int[] {1, 2, 3, 4};
        int[] dst = new int[] {0, 0, 0, 0};
        
        Buffer.BlockCopy(srcOffset: 0, src: src, count: [|src.Length|], dstOffset: 0, dst: dst);
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Module Program
    Sub Main(args As String())
        Dim src = New Integer() {1, 2, 3, 4}
        Dim dst = New Integer() {0, 0, 0, 0}

        Buffer.BlockCopy(srcOffset:=0, src:=src, count:=[|src.Length|], dstOffset:=0, dst:=dst)
    End Sub
End Module
");
        }

        [Fact]
        public async Task NonLocalArrays()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Program
{
    static void Main()
    {
        int[] src = new int[] {1, 2, 3, 4};
        int[] dst = new int[] {0, 0, 0, 0};
        
        SomeFunction(src, dst);
    }

    static void SomeFunction(int[] src, int[] dst)
    {
        Buffer.BlockCopy(src, 0, dst, 0, [|src.Length|]);
    }
}
");
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Module Program
    Sub Main(args As String())
        Dim src = New Integer() {1, 2, 3, 4}
        Dim dst = New Integer() {0, 0, 0, 0}

        SomeFunction(src, dst)
    End Sub

    Sub SomeFunction(ByRef src As Integer(), ByRef dst As Integer())
        Buffer.BlockCopy(src, 0, dst, 0, [|src.Length|])
    End Sub
End Module
");
        }

        [Fact]
        public async Task SrcArrayAsFunctionReturn()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Program
{
    static void Main()
    {
        int[] dst = new int[] {0, 0, 0, 0};
        
        Buffer.BlockCopy(GetSrcArray(), 0, dst, 0, [|dst.Length|]);
    }

    static int[] GetSrcArray()
    {
        return new int[] { 1, 2, 3, 4 };
    }
}
");
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Module Program
    Sub Main(args As String())
        Dim dst = New Integer() {0, 0, 0, 0}

        Buffer.BlockCopy(GetSrcArray(), 0, dst, 0, [|dst.Length|])
    End Sub

    Function GetSrcArray()
        Return New Integer() {1, 2, 3, 4}
    End Function
End Module
");
        }

        [Fact]
        public async Task SrcAndDstAsGlobalArrays()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Program
{
    private int[] src;
    private int[] dst;

    static void Main()
    {
        Program program = new Program();
        Buffer.BlockCopy(program.src, 0, program.dst, 0, [|program.src.Length|]);
    }
}
");
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class Program
    Private src As Integer()
    Private dst As Integer()
    Sub Main(args As String())
        Dim program = New Program()
        Buffer.BlockCopy(program.src, 0, program.dst, 0, [|program.src.Length|])
    End Sub
End Class
");
        }

        [Fact(Skip = "https://github.com/dotnet/roslyn-analyzers/issues/5371")]
        public async Task SrcAndDstAsArrayCreateInstance()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Program
{
    static void Main()
    {
        Array src = Array.CreateInstance(typeof(int), 4);
        Array dst = Array.CreateInstance(typeof(int), 4);

        Buffer.BlockCopy(src, 0, dst, 0, [|src.Length|]);
    }
}
");
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Module Program
    Sub Main(args As String())
        Dim src = Array.CreateInstance(GetType(Integer), 4)
        Dim dst = Array.CreateInstance(GetType(Integer), 4)

        Buffer.BlockCopy(src, 0, dst, 0, [|src.Length|])
    End Sub
End Module
");
        }
    }
}