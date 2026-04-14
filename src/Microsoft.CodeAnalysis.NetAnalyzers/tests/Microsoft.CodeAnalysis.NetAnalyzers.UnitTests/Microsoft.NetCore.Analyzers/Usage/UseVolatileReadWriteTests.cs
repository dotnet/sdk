// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeAnalysis.Testing.EmptyDiagnosticAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Usage.CSharpUseVolatileReadWriteFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeAnalysis.Testing.EmptyDiagnosticAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Usage.BasicUseVolatileReadWriteFixer>;

namespace Microsoft.NetCore.Analyzers.Usage.UnitTests
{
    public sealed class UseVolatileReadWriteTests
    {
        private const string CsharpSystemThreadingThread = """
                                                           namespace System.Threading
                                                           {
                                                               public sealed class Thread
                                                               {
                                                                   private const string ObsoletionDiagnosticId = "SYSLIB0054";
                                                           
                                                                   [Obsolete(DiagnosticId = ObsoletionDiagnosticId)]
                                                                   public static byte VolatileRead(ref byte address) => Volatile.Read(ref address);
                                                                   [Obsolete(DiagnosticId = ObsoletionDiagnosticId)]
                                                                   public static double VolatileRead(ref double address) => Volatile.Read(ref address);
                                                                   [Obsolete(DiagnosticId = ObsoletionDiagnosticId)]
                                                                   public static short VolatileRead(ref short address) => Volatile.Read(ref address);
                                                                   [Obsolete(DiagnosticId = ObsoletionDiagnosticId)]
                                                                   public static int VolatileRead(ref int address) => Volatile.Read(ref address);
                                                                   [Obsolete(DiagnosticId = ObsoletionDiagnosticId)]
                                                                   public static long VolatileRead(ref long address) => Volatile.Read(ref address);
                                                                   [Obsolete(DiagnosticId = ObsoletionDiagnosticId)]
                                                                   public static IntPtr VolatileRead(ref IntPtr address) => Volatile.Read(ref address);
                                                                   [Obsolete(DiagnosticId = ObsoletionDiagnosticId)]
                                                                   public static object VolatileRead(ref object address) => Volatile.Read(ref address);
                                                                   [Obsolete(DiagnosticId = ObsoletionDiagnosticId)]
                                                                   public static sbyte VolatileRead(ref sbyte address) => Volatile.Read(ref address);
                                                                   [Obsolete(DiagnosticId = ObsoletionDiagnosticId)]
                                                                   public static float VolatileRead(ref float address) => Volatile.Read(ref address);
                                                                   [Obsolete(DiagnosticId = ObsoletionDiagnosticId)]
                                                                   public static ushort VolatileRead(ref ushort address) => Volatile.Read(ref address);
                                                                   [Obsolete(DiagnosticId = ObsoletionDiagnosticId)]
                                                                   public static uint VolatileRead(ref uint address) => Volatile.Read(ref address);
                                                                   [Obsolete(DiagnosticId = ObsoletionDiagnosticId)]
                                                                   public static ulong VolatileRead(ref ulong address) => Volatile.Read(ref address);
                                                                   [Obsolete(DiagnosticId = ObsoletionDiagnosticId)]
                                                                   public static UIntPtr VolatileRead(ref UIntPtr address) => Volatile.Read(ref address);
                                                                   [Obsolete(DiagnosticId = ObsoletionDiagnosticId)]
                                                                   public static void VolatileWrite(ref byte address, byte value) => Volatile.Write(ref address, value);
                                                                   [Obsolete(DiagnosticId = ObsoletionDiagnosticId)]
                                                                   public static void VolatileWrite(ref double address, double value) => Volatile.Write(ref address, value);
                                                                   [Obsolete(DiagnosticId = ObsoletionDiagnosticId)]
                                                                   public static void VolatileWrite(ref short address, short value) => Volatile.Write(ref address, value);
                                                                   [Obsolete(DiagnosticId = ObsoletionDiagnosticId)]
                                                                   public static void VolatileWrite(ref int address, int value) => Volatile.Write(ref address, value);
                                                                   [Obsolete(DiagnosticId = ObsoletionDiagnosticId)]
                                                                   public static void VolatileWrite(ref long address, long value) => Volatile.Write(ref address, value);
                                                                   [Obsolete(DiagnosticId = ObsoletionDiagnosticId)]
                                                                   public static void VolatileWrite(ref IntPtr address, IntPtr value) => Volatile.Write(ref address, value);
                                                                   [Obsolete(DiagnosticId = ObsoletionDiagnosticId)]
                                                                   public static void VolatileWrite(ref object address, object value) => Volatile.Write(ref address, value);
                                                                   [Obsolete(DiagnosticId = ObsoletionDiagnosticId)]
                                                                   public static void VolatileWrite(ref sbyte address, sbyte value) => Volatile.Write(ref address, value);
                                                                   [Obsolete(DiagnosticId = ObsoletionDiagnosticId)]
                                                                   public static void VolatileWrite(ref float address, float value) => Volatile.Write(ref address, value);
                                                                   [Obsolete(DiagnosticId = ObsoletionDiagnosticId)]
                                                                   public static void VolatileWrite(ref ushort address, ushort value) => Volatile.Write(ref address, value);
                                                                   [Obsolete(DiagnosticId = ObsoletionDiagnosticId)]
                                                                   public static void VolatileWrite(ref uint address, uint value) => Volatile.Write(ref address, value);
                                                                   [Obsolete(DiagnosticId = ObsoletionDiagnosticId)]
                                                                   public static void VolatileWrite(ref ulong address, ulong value) => Volatile.Write(ref address, value);
                                                                   [Obsolete(DiagnosticId = ObsoletionDiagnosticId)]
                                                                   public static void VolatileWrite(ref UIntPtr address, UIntPtr value) => Volatile.Write(ref address, value);
                                                               }
                                                           }
                                                           """;

        private const string VisualBasicSystemThreadingThread = """
                                                                Namespace System.Threading
                                                                    Public NotInheritable Class Thread
                                                                        Private Const ObsoletionDiagnosticId = "SYSLIB0054"
                                                                
                                                                        <Obsolete(DiagnosticId := ObsoletionDiagnosticId)>
                                                                        Public Shared Function VolatileRead(ByRef address As Byte) As Byte
                                                                            Volatile.Read(address)
                                                                        End Function
                                                                        <Obsolete(DiagnosticId := ObsoletionDiagnosticId)>
                                                                        Public Shared Function VolatileRead(ByRef address As Double) As Double
                                                                            Volatile.Read(address)
                                                                        End Function
                                                                        <Obsolete(DiagnosticId := ObsoletionDiagnosticId)>
                                                                        Public Shared Function VolatileRead(ByRef address As Short) As Short
                                                                            Volatile.Read(address)
                                                                        End Function
                                                                        <Obsolete(DiagnosticId := ObsoletionDiagnosticId)>
                                                                        Public Shared Function VolatileRead(ByRef address As Int32) As Int32
                                                                            Volatile.Read(address)
                                                                        End Function
                                                                        <Obsolete(DiagnosticId := ObsoletionDiagnosticId)>
                                                                        Public Shared Function VolatileRead(ByRef address As Int64) As Int64
                                                                            Volatile.Read(address)
                                                                        End Function
                                                                        <Obsolete(DiagnosticId := ObsoletionDiagnosticId)>
                                                                        Public Shared Function VolatileRead(ByRef address As IntPtr) As IntPtr
                                                                            Volatile.Read(address)
                                                                        End Function
                                                                        <Obsolete(DiagnosticId := ObsoletionDiagnosticId)>
                                                                        Public Shared Function VolatileRead(ByRef address As Object) As Object
                                                                            Volatile.Read(address)
                                                                        End Function
                                                                        <Obsolete(DiagnosticId := ObsoletionDiagnosticId)>
                                                                        Public Shared Function VolatileRead(ByRef address As SByte) As SByte
                                                                            Volatile.Read(address)
                                                                        End Function
                                                                        <Obsolete(DiagnosticId := ObsoletionDiagnosticId)>
                                                                        Public Shared Function VolatileRead(ByRef address As Single) As Single
                                                                            Volatile.Read(address)
                                                                        End Function
                                                                        <Obsolete(DiagnosticId := ObsoletionDiagnosticId)>
                                                                        Public Shared Function VolatileRead(ByRef address As UInt16) As UInt16
                                                                            Volatile.Read(address)
                                                                        End Function
                                                                        <Obsolete(DiagnosticId := ObsoletionDiagnosticId)>
                                                                        Public Shared Function VolatileRead(ByRef address As UInt32) As UInt32
                                                                            Volatile.Read(address)
                                                                        End Function
                                                                        <Obsolete(DiagnosticId := ObsoletionDiagnosticId)>
                                                                        Public Shared Function VolatileRead(ByRef address As UInt64) As UInt64
                                                                            Volatile.Read(address)
                                                                        End Function
                                                                        <Obsolete(DiagnosticId := ObsoletionDiagnosticId)>
                                                                        Public Shared Function VolatileRead(ByRef address As UIntPtr) As UIntPtr
                                                                            Volatile.Read(address)
                                                                        End Function
                                                                        <Obsolete(DiagnosticId := ObsoletionDiagnosticId)>
                                                                        Public Shared Sub VolatileWrite(ByRef address As Byte, value As Byte)
                                                                            Volatile.Write(address, value)
                                                                        End Sub
                                                                        <Obsolete(DiagnosticId := ObsoletionDiagnosticId)>
                                                                        Public Shared Sub VolatileWrite(ByRef address As Double, value As Double)
                                                                            Volatile.Write(address, value)
                                                                        End Sub
                                                                        <Obsolete(DiagnosticId := ObsoletionDiagnosticId)>
                                                                        Public Shared Sub VolatileWrite(ByRef address As Int16, value As Int16)
                                                                            Volatile.Write(address, value)
                                                                        End Sub
                                                                        <Obsolete(DiagnosticId := ObsoletionDiagnosticId)>
                                                                        Public Shared Sub VolatileWrite(ByRef address As Int32, value As Int32)
                                                                            Volatile.Write(address, value)
                                                                        End Sub
                                                                        <Obsolete(DiagnosticId := ObsoletionDiagnosticId)>
                                                                        Public Shared Sub VolatileWrite(ByRef address As Int64, value As Int64)
                                                                            Volatile.Write(address, value)
                                                                        End Sub
                                                                        <Obsolete(DiagnosticId := ObsoletionDiagnosticId)>
                                                                        Public Shared Sub VolatileWrite(ByRef address As IntPtr, value As IntPtr)
                                                                            Volatile.Write(address, value)
                                                                        End Sub
                                                                        <Obsolete(DiagnosticId := ObsoletionDiagnosticId)>
                                                                        Public Shared Sub VolatileWrite(ByRef address As Object, value As Object)
                                                                            Volatile.Write(address, value)
                                                                        End Sub
                                                                        <Obsolete(DiagnosticId := ObsoletionDiagnosticId)>
                                                                        Public Shared Sub VolatileWrite(ByRef address As SByte, value As SByte)
                                                                            Volatile.Write(address, value)
                                                                        End Sub
                                                                        <Obsolete(DiagnosticId := ObsoletionDiagnosticId)>
                                                                        Public Shared Sub VolatileWrite(ByRef address As Single, value As Single)
                                                                            Volatile.Write(address, value)
                                                                        End Sub
                                                                        <Obsolete(DiagnosticId := ObsoletionDiagnosticId)>
                                                                        Public Shared Sub VolatileWrite(ByRef address As UInt16, value As UInt16)
                                                                            Volatile.Write(address, value)
                                                                        End Sub
                                                                        <Obsolete(DiagnosticId := ObsoletionDiagnosticId)>
                                                                        Public Shared Sub VolatileWrite(ByRef address As UInt32, value As UInt32)
                                                                            Volatile.Write(address, value)
                                                                        End Sub
                                                                        <Obsolete(DiagnosticId := ObsoletionDiagnosticId)>
                                                                        Public Shared Sub VolatileWrite(ByRef address As Uint64, value As Uint64)
                                                                            Volatile.Write(address, value)
                                                                        End Sub
                                                                        <Obsolete(DiagnosticId := ObsoletionDiagnosticId)>
                                                                        Public Shared Sub VolatileWrite(ByRef address As UIntPtr, value As UIntPtr)
                                                                            Volatile.Write(address, value)
                                                                        End Sub
                                                                    End Class
                                                                End Namespace
                                                                """;

        public static readonly TheoryData<string> CSharpTypes = new()
        {
            "IntPtr",
            "UIntPtr",
            "byte",
            "double",
            "float",
            "int",
            "long",
            "sbyte",
            "short",
            "uint",
            "ulong",
            "ushort"
        };

        public static readonly TheoryData<string> VisualBasicTypes = new()
        {
            "IntPtr",
            "UIntPtr",
            "Byte",
            "Double",
            "Single",
            "Integer",
            "Long",
            "Object",
            "Sbyte",
            "Short",
            "UInteger",
            "ULong",
            "UShort"
        };

        [Theory]
        [MemberData(nameof(CSharpTypes))]
        public Task CS_UseVolatileRead(string type)
        {
            var code = $$"""
                         using System;
                         using System.Threading;

                         #nullable enable
                         class Test
                         {
                             void M({{type}} arg)
                             {
                                 {|#0:Thread.VolatileRead(ref arg)|};
                             }
                         }
                         """;
            var fixedCode = $$"""
                              using System;
                              using System.Threading;

                              #nullable enable
                              class Test
                              {
                                  void M({{type}} arg)
                                  {
                                      Volatile.Read(ref arg);
                                  }
                              }
                              """;

            return VerifyCsharpAsync(code, fixedCode);
        }

        [Theory]
        [MemberData(nameof(CSharpTypes))]
        public Task CS_UseVolatileRead_WithNamedArguments(string type)
        {
            var code = $$"""
                         using System;
                         using System.Threading;

                         #nullable enable
                         class Test
                         {
                             void M({{type}} arg)
                             {
                                 {|#0:Thread.VolatileRead(address: ref arg)|};
                             }
                         }
                         """;
            var fixedCode = $$"""
                              using System;
                              using System.Threading;

                              #nullable enable
                              class Test
                              {
                                  void M({{type}} arg)
                                  {
                                      Volatile.Read(location: ref arg);
                                  }
                              }
                              """;

            return VerifyCsharpAsync(code, fixedCode);
        }

        [Theory]
        [MemberData(nameof(CSharpTypes))]
        public Task CS_UseVolatileRead_WithTrivia(string type)
        {
            var code = $$"""
                         using System;
                         using System.Threading;

                         #nullable enable
                         class Test
                         {
                             void M({{type}} arg)
                             {
                                 // Trivia prefix
                                 {|#0:Thread.VolatileRead(ref arg)|}; // Trivia infix
                                 // Trivia suffix
                             }
                         }
                         """;
            var fixedCode = $$"""
                              using System;
                              using System.Threading;

                              #nullable enable
                              class Test
                              {
                                  void M({{type}} arg)
                                  {
                                      // Trivia prefix
                                      Volatile.Read(ref arg); // Trivia infix
                                      // Trivia suffix
                                  }
                              }
                              """;

            return VerifyCsharpAsync(code, fixedCode);
        }

        [Fact]
        public Task CS_UseVolatileRead_Nullable()
        {
            const string code = """
                                using System;
                                using System.Threading;

                                #nullable enable
                                class Test
                                {
                                    void M(object? arg)
                                    {
                                        {|#0:Thread.VolatileRead(ref arg)|};
                                    }
                                }
                                """;
            const string fixedCode = """
                                     using System;
                                     using System.Threading;

                                     #nullable enable
                                     class Test
                                     {
                                         void M(object? arg)
                                         {
                                             Volatile.Read(ref arg);
                                         }
                                     }
                                     """;

            return VerifyCsharpAsync(code, fixedCode);
        }

        [Fact]
        public Task CS_UseVolatileRead_NonNullable()
        {
            const string code = """
                                using System;
                                using System.Threading;

                                class Test
                                {
                                    void M(object arg)
                                    {
                                        {|#0:Thread.VolatileRead(ref arg)|};
                                    }
                                }
                                """;
            const string fixedCode = """
                                     using System;
                                     using System.Threading;

                                     class Test
                                     {
                                         void M(object arg)
                                         {
                                             Volatile.Read(ref arg);
                                         }
                                     }
                                     """;

            return VerifyCsharpAsync(code, fixedCode);
        }

        [Theory]
        [MemberData(nameof(CSharpTypes))]
        public Task CS_UseVolatileWrite(string type)
        {
            var code = $$"""
                         using System;
                         using System.Threading;

                         #nullable enable
                         class Test
                         {
                             void M({{type}} arg, {{type}} value)
                             {
                                 {|#0:Thread.VolatileWrite(ref arg, value)|};
                             }
                         }
                         """;
            var fixedCode = $$"""
                              using System;
                              using System.Threading;

                              #nullable enable
                              class Test
                              {
                                  void M({{type}} arg, {{type}} value)
                                  {
                                      Volatile.Write(ref arg, value);
                                  }
                              }
                              """;

            return VerifyCsharpAsync(code, fixedCode);
        }

        [Theory]
        [MemberData(nameof(CSharpTypes))]
        public Task CS_UseVolatileWrite_WithNamedArguments(string type)
        {
            var code = $$"""
                         using System;
                         using System.Threading;

                         #nullable enable
                         class Test
                         {
                             void M({{type}} arg, {{type}} value)
                             {
                                 {|#0:Thread.VolatileWrite(address: ref arg, value: value)|};
                             }
                         }
                         """;
            var fixedCode = $$"""
                              using System;
                              using System.Threading;

                              #nullable enable
                              class Test
                              {
                                  void M({{type}} arg, {{type}} value)
                                  {
                                      Volatile.Write(location: ref arg, value: value);
                                  }
                              }
                              """;

            return VerifyCsharpAsync(code, fixedCode);
        }

        [Theory]
        [MemberData(nameof(CSharpTypes))]
        public Task CS_UseVolatileWrite_WithReversedArguments(string type)
        {
            var code = $$"""
                         using System;
                         using System.Threading;

                         #nullable enable
                         class Test
                         {
                             void M({{type}} arg, {{type}} value)
                             {
                                 {|#0:Thread.VolatileWrite(value: value, address: ref arg)|};
                             }
                         }
                         """;
            var fixedCode = $$"""
                              using System;
                              using System.Threading;

                              #nullable enable
                              class Test
                              {
                                  void M({{type}} arg, {{type}} value)
                                  {
                                      Volatile.Write(value: value, location: ref arg);
                                  }
                              }
                              """;

            return VerifyCsharpAsync(code, fixedCode);
        }

        [Theory]
        [MemberData(nameof(CSharpTypes))]
        public Task CS_UseVolatileWrite_WithSingleNamedArgument(string type)
        {
            var code = $$"""
                         using System;
                         using System.Threading;

                         #nullable enable
                         class Test
                         {
                             void M({{type}} arg, {{type}} value)
                             {
                                 {|#0:Thread.VolatileWrite(address: ref arg, value)|};
                             }
                         }
                         """;
            var fixedCode = $$"""
                              using System;
                              using System.Threading;

                              #nullable enable
                              class Test
                              {
                                  void M({{type}} arg, {{type}} value)
                                  {
                                      Volatile.Write(location: ref arg, value);
                                  }
                              }
                              """;

            return VerifyCsharpAsync(code, fixedCode);
        }

        [Theory]
        [MemberData(nameof(CSharpTypes))]
        public Task CS_UseVolatileWrite_WithTrivia(string type)
        {
            var code = $$"""
                         using System;
                         using System.Threading;

                         #nullable enable
                         class Test
                         {
                             void M({{type}} arg, {{type}} value)
                             {
                                 // Trivia prefix
                                 {|#0:Thread.VolatileWrite(ref arg, value)|}; // Trivia infix
                                 // Trivia suffix
                             }
                         }
                         """;
            var fixedCode = $$"""
                              using System;
                              using System.Threading;

                              #nullable enable
                              class Test
                              {
                                  void M({{type}} arg, {{type}} value)
                                  {
                                      // Trivia prefix
                                      Volatile.Write(ref arg, value); // Trivia infix
                                      // Trivia suffix
                                  }
                              }
                              """;

            return VerifyCsharpAsync(code, fixedCode);
        }

        [Fact]
        public Task CS_UseVolatileWrite_Nullable()
        {
            const string code = """
                                using System;
                                using System.Threading;

                                #nullable enable
                                class Test
                                {
                                    void M(object? arg, object? value)
                                    {
                                        {|#0:Thread.VolatileWrite(ref arg, value)|};
                                    }
                                }
                                """;
            const string fixedCode = """
                                     using System;
                                     using System.Threading;

                                     #nullable enable
                                     class Test
                                     {
                                         void M(object? arg, object? value)
                                         {
                                             Volatile.Write(ref arg, value);
                                         }
                                     }
                                     """;

            return VerifyCsharpAsync(code, fixedCode);
        }

        [Fact]
        public Task CS_UseVolatileWrite_NonNullable()
        {
            const string code = """
                                using System;
                                using System.Threading;

                                class Test
                                {
                                    void M(object arg, object value)
                                    {
                                        {|#0:Thread.VolatileWrite(ref arg, value)|};
                                    }
                                }
                                """;
            const string fixedCode = """
                                     using System;
                                     using System.Threading;

                                     class Test
                                     {
                                         void M(object arg, object value)
                                         {
                                             Volatile.Write(ref arg, value);
                                         }
                                     }
                                     """;

            return VerifyCsharpAsync(code, fixedCode);
        }

        [Theory]
        [MemberData(nameof(VisualBasicTypes))]
        public Task VB_UseVolatileRead(string type)
        {
            var code = $$"""
                         Imports System
                         Imports System.Threading

                         Class Test
                             Sub M(arg As {{type}})
                                 {|#0:Thread.VolatileRead(arg)|}
                             End Sub
                         End Class
                         """;
            var fixedCode = $"""
                             Imports System
                             Imports System.Threading

                             Class Test
                                 Sub M(arg As {type})
                                     Volatile.Read(arg)
                                 End Sub
                             End Class
                             """;

            return VerifyVisualBasicAsync(code, fixedCode);
        }

        [Theory]
        [MemberData(nameof(VisualBasicTypes))]
        public Task VB_UseVolatileRead_WithNamedArguments(string type)
        {
            var code = $$"""
                         Imports System
                         Imports System.Threading

                         Class Test
                             Sub M(arg As {{type}})
                                 {|#0:Thread.VolatileRead(address:=arg)|}
                             End Sub
                         End Class
                         """;
            var fixedCode = $"""
                             Imports System
                             Imports System.Threading

                             Class Test
                                 Sub M(arg As {type})
                                     Volatile.Read(location:=arg)
                                 End Sub
                             End Class
                             """;

            return VerifyVisualBasicAsync(code, fixedCode);
        }

        [Theory]
        [MemberData(nameof(VisualBasicTypes))]
        public Task VB_UseVolatileRead_WithTrivia(string type)
        {
            var code = $$"""
                         Imports System
                         Imports System.Threading

                         Class Test
                             Sub M(arg As {{type}})
                                 ' Trivia prefix
                                 {|#0:Thread.VolatileRead(arg)|} ' Trivia infix
                                 ' Trivia suffix
                             End Sub
                         End Class
                         """;
            var fixedCode = $"""
                             Imports System
                             Imports System.Threading

                             Class Test
                                 Sub M(arg As {type})
                                     ' Trivia prefix
                                     Volatile.Read(arg) ' Trivia infix
                                     ' Trivia suffix
                                 End Sub
                             End Class
                             """;

            return VerifyVisualBasicAsync(code, fixedCode);
        }

        [Theory]
        [MemberData(nameof(VisualBasicTypes))]
        public Task VB_UseVolatileWrite(string type)
        {
            var code = $$"""
                         Imports System
                         Imports System.Threading

                         Class Test
                             Sub M(arg As {{type}}, value As {{type}})
                                 {|#0:Thread.VolatileWrite(arg, value)|}
                             End Sub
                         End Class
                         """;
            var fixedCode = $"""
                             Imports System
                             Imports System.Threading

                             Class Test
                                 Sub M(arg As {type}, value As {type})
                                     Volatile.Write(arg, value)
                                 End Sub
                             End Class
                             """;

            return VerifyVisualBasicAsync(code, fixedCode);
        }

        [Theory]
        [MemberData(nameof(VisualBasicTypes))]
        public Task VB_UseVolatileWrite_WithNamedArguments(string type)
        {
            var code = $$"""
                         Imports System
                         Imports System.Threading

                         Class Test
                             Sub M(arg As {{type}}, value As {{type}})
                                 {|#0:Thread.VolatileWrite(address:=arg, value:=value)|}
                             End Sub
                         End Class
                         """;
            var fixedCode = $"""
                             Imports System
                             Imports System.Threading

                             Class Test
                                 Sub M(arg As {type}, value As {type})
                                     Volatile.Write(location:=arg, value:=value)
                                 End Sub
                             End Class
                             """;

            return VerifyVisualBasicAsync(code, fixedCode);
        }

        [Theory]
        [MemberData(nameof(VisualBasicTypes))]
        public Task VB_UseVolatileWrite_WithReversedArguments(string type)
        {
            var code = $$"""
                         Imports System
                         Imports System.Threading

                         Class Test
                             Sub M(arg As {{type}}, value As {{type}})
                                 {|#0:Thread.VolatileWrite(value:=value, address:=arg)|}
                             End Sub
                         End Class
                         """;
            var fixedCode = $"""
                             Imports System
                             Imports System.Threading

                             Class Test
                                 Sub M(arg As {type}, value As {type})
                                     Volatile.Write(location:=arg, value:=value)
                                 End Sub
                             End Class
                             """;

            return VerifyVisualBasicAsync(code, fixedCode);
        }

        [Theory]
        [MemberData(nameof(VisualBasicTypes))]
        public Task VB_UseVolatileWrite_WithSingleNamedArgument(string type)
        {
            var code = $$"""
                         Imports System
                         Imports System.Threading

                         Class Test
                             Sub M(arg As {{type}}, value As {{type}})
                                 {|#0:Thread.VolatileWrite(address:=arg, value)|}
                             End Sub
                         End Class
                         """;
            var fixedCode = $"""
                             Imports System
                             Imports System.Threading

                             Class Test
                                 Sub M(arg As {type}, value As {type})
                                     Volatile.Write(location:=arg, value)
                                 End Sub
                             End Class
                             """;

            return VerifyVisualBasicAsync(code, fixedCode);
        }

        [Theory]
        [MemberData(nameof(VisualBasicTypes))]
        public Task VB_UseVolatileWrite_WithTrivia(string type)
        {
            var code = $$"""
                         Imports System
                         Imports System.Threading

                         Class Test
                             Sub M(arg As {{type}}, value As {{type}})
                                 ' Trivia prefix
                                 {|#0:Thread.VolatileWrite(arg, value)|} ' Trivia infix
                                 ' Trivia suffix
                             End Sub
                         End Class
                         """;
            var fixedCode = $"""
                             Imports System
                             Imports System.Threading

                             Class Test
                                 Sub M(arg As {type}, value As {type})
                                     ' Trivia prefix
                                     Volatile.Write(arg, value) ' Trivia infix
                                     ' Trivia suffix
                                 End Sub
                             End Class
                             """;

            return VerifyVisualBasicAsync(code, fixedCode);
        }

        private static Task VerifyCsharpAsync(string code, string fixedCode)
        {
            return new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code, CsharpSystemThreadingThread }
                },
                FixedState =
                {
                    Sources = { fixedCode, CsharpSystemThreadingThread }
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult("SYSLIB0054", DiagnosticSeverity.Warning).WithLocation(0)
                },
                LanguageVersion = LanguageVersion.CSharp8,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            }.RunAsync();
        }

        private static Task VerifyVisualBasicAsync(string code, string fixedCode)
        {
            return new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { code, VisualBasicSystemThreadingThread }
                },
                FixedState =
                {
                    Sources = { fixedCode, VisualBasicSystemThreadingThread }
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult("SYSLIB0054", DiagnosticSeverity.Warning).WithLocation(0)
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            }.RunAsync();
        }
    }
}