﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
        public async Task UsingByteArrayAsync(string array)
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
        public async Task UsingSbyteArrayAsync(string array)
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
        public async Task UsingBoolArrayAsync(string array)
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Program
{
    static void Main()
    {
        bool[] src = new bool[] { true, true, true, true };
        bool[] dst = new bool[] { false, false, false, false };
        
        Buffer.BlockCopy(src, 0, dst, 0, " + array + @".Length);
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Module Program
    Sub Main(args As String())
        Dim src = New Boolean() {True, True, True, True}
        Dim dst = New Boolean() {False, False, False, False}

        Buffer.BlockCopy(src, 0, dst, 0, " + array + @".Length)
    End Sub
End Module
");
        }

        [Theory]
        [InlineData("src")]
        [InlineData("dst")]
        public async Task UsingIntArrayAsync(string array)
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
        public async Task OperandIsNotSrcOrDstAsync()
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
        public async Task SrcNumOfBytesAsReferenceAsync(string array)
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
        public async Task NumOfBytesAsLiteralConstAsync()
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
        public async Task NumOfBytesAsMultipleDeclarationsAsync()
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
        public async Task NumOfBytesAsConstLiteralAsync()
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
        public async Task NumOfBytesAsClassConstFieldAsync()
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
        public async Task NumOfBytesAsClassPropertyGetterAsync()
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
        public async Task NumOfBytesAsMethodInvocationAsync()
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
        public async Task SrcAndDstAsLiteralArraysAsync()
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
        public async Task NamedArgumentsNotInOrderAsync()
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
        public async Task NonLocalArraysAsync()
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
        public async Task SrcArrayAsFunctionReturnAsync()
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
        public async Task SrcAndDstAsGlobalArraysAsync()
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
        public async Task SrcAndDstAsArrayCreateInstanceAsync()
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