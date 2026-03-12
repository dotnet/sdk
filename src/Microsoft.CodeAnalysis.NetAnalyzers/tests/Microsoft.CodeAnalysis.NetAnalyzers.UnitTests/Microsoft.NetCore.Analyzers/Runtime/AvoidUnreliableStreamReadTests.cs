// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.AvoidUnreliableStreamReadAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.AvoidUnreliableStreamReadFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.AvoidUnreliableStreamReadAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.AvoidUnreliableStreamReadFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class AvoidUnreliableStreamReadTests
    {
        [Fact]
        public async Task EntireBuffer_OffersFixer_CS()
        {
            string source = """
                using System.IO;
                using System.Threading;

                class C
                {
                    async void M(Stream s, byte[] buffer, CancellationToken ct)
                    {
                        [|s.Read(buffer, 0, buffer.Length)|];
                        [|s.Read(buffer)|];
                        await [|s.ReadAsync(buffer, 0, buffer.Length)|];
                        await [|s.ReadAsync(buffer)|];
                        await [|s.ReadAsync(buffer, 0, buffer.Length, ct)|];
                        await [|s.ReadAsync(buffer, ct)|];
                    }
                }
                """;

            string fixedSource = """
                using System.IO;
                using System.Threading;

                class C
                {
                    async void M(Stream s, byte[] buffer, CancellationToken ct)
                    {
                        s.ReadExactly(buffer);
                        s.ReadExactly(buffer);
                        await s.ReadExactlyAsync(buffer);
                        await s.ReadExactlyAsync(buffer);
                        await s.ReadExactlyAsync(buffer, ct);
                        await s.ReadExactlyAsync(buffer, ct);
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task WithCount_OffersFixer_CS()
        {
            string source = """
                using System.IO;
                using System.Threading;

                class C
                {
                    async void M(Stream s, byte[] buffer, int count, CancellationToken ct)
                    {
                        [|s.Read(buffer, 0, count)|];
                        await [|s.ReadAsync(buffer, 0, count)|];
                        await [|s.ReadAsync(buffer, 0, count, ct)|];
                    }
                }
                """;

            string fixedSource = """
                using System.IO;
                using System.Threading;

                class C
                {
                    async void M(Stream s, byte[] buffer, int count, CancellationToken ct)
                    {
                        s.ReadExactly(buffer, 0, count);
                        await s.ReadExactlyAsync(buffer, 0, count);
                        await s.ReadExactlyAsync(buffer, 0, count, ct);
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task WithOffsetAndCount_OffersFixer_CS()
        {
            string source = """
                using System.IO;
                using System.Threading;

                class C
                {
                    async void M(Stream s, byte[] buffer, int offset, int count, CancellationToken ct)
                    {
                        [|s.Read(buffer, offset, count)|];
                        await [|s.ReadAsync(buffer, offset, count)|];
                        await [|s.ReadAsync(buffer, offset, count, ct)|];
                    }
                }
                """;

            string fixedSource = """
                using System.IO;
                using System.Threading;

                class C
                {
                    async void M(Stream s, byte[] buffer, int offset, int count, CancellationToken ct)
                    {
                        s.ReadExactly(buffer, offset, count);
                        await s.ReadExactlyAsync(buffer, offset, count);
                        await s.ReadExactlyAsync(buffer, offset, count, ct);
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task DifferentBufferInstances_OffersFixer_CS()
        {
            string source = """
                using System.IO;

                class C
                {
                    private readonly byte[] _bufferField;
                    public byte[] BufferProperty { get; set; }

                    async void M(Stream s, byte[] bufferParameter)
                    {
                        byte[] bufferLocal = { };

                        [|s.Read(bufferParameter, 0, bufferParameter.Length)|];
                        [|s.Read(bufferLocal, 0, bufferLocal.Length)|];
                        [|s.Read(_bufferField, 0, _bufferField.Length)|];
                        [|s.Read(BufferProperty, 0, BufferProperty.Length)|];

                        await [|s.ReadAsync(bufferParameter, 0, bufferParameter.Length)|];
                        await [|s.ReadAsync(bufferLocal, 0, bufferLocal.Length)|];
                        await [|s.ReadAsync(_bufferField, 0, _bufferField.Length)|];
                        await [|s.ReadAsync(BufferProperty, 0, BufferProperty.Length)|];
                    }
                }
                """;

            string fixedSource = """
                using System.IO;

                class C
                {
                    private readonly byte[] _bufferField;
                    public byte[] BufferProperty { get; set; }

                    async void M(Stream s, byte[] bufferParameter)
                    {
                        byte[] bufferLocal = { };

                        s.ReadExactly(bufferParameter);
                        s.ReadExactly(bufferLocal);
                        s.ReadExactly(_bufferField);
                        s.ReadExactly(BufferProperty);

                        await s.ReadExactlyAsync(bufferParameter);
                        await s.ReadExactlyAsync(bufferLocal);
                        await s.ReadExactlyAsync(_bufferField);
                        await s.ReadExactlyAsync(BufferProperty);
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task SpecificStreamType_OffersFixer_CS()
        {
            string source = """
                using System.IO;

                class C
                {
                    async void M(BufferedStream s, byte[] buffer)
                    {
                        [|s.Read(buffer, 0, buffer.Length)|];
                        await [|s.ReadAsync(buffer, 0, buffer.Length)|];
                    }
                }
                """;

            string fixedSource = """
                using System.IO;

                class C
                {
                    async void M(BufferedStream s, byte[] buffer)
                    {
                        s.ReadExactly(buffer);
                        await s.ReadExactlyAsync(buffer);
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task NamedArguments_OffersFixer_CS()
        {
            string source = """
                using System.IO;
                using System.Threading;

                class C
                {
                    async void M(Stream s, byte[] buffer, int offset, int count, CancellationToken ct)
                    {
                        [|s.Read(buffer: buffer, offset: 0, count: buffer.Length)|];
                        [|s.Read(buffer: buffer)|];
                        [|s.Read(buffer: buffer, offset: offset, count: count)|];
                        await [|s.ReadAsync(buffer: buffer, offset: 0, count: buffer.Length)|];
                        await [|s.ReadAsync(buffer: buffer)|];
                        await [|s.ReadAsync(buffer: buffer, offset: offset, count: count)|];
                        await [|s.ReadAsync(buffer: buffer, offset: 0, count: buffer.Length, cancellationToken: ct)|];
                        await [|s.ReadAsync(buffer: buffer, cancellationToken: ct)|];
                        await [|s.ReadAsync(buffer: buffer, offset: offset, count: count, cancellationToken: ct)|];
                    }
                }
                """;

            string fixedSource = """
                using System.IO;
                using System.Threading;

                class C
                {
                    async void M(Stream s, byte[] buffer, int offset, int count, CancellationToken ct)
                    {
                        s.ReadExactly(buffer: buffer);
                        s.ReadExactly(buffer: buffer);
                        s.ReadExactly(buffer: buffer, offset: offset, count: count);
                        await s.ReadExactlyAsync(buffer: buffer);
                        await s.ReadExactlyAsync(buffer: buffer);
                        await s.ReadExactlyAsync(buffer: buffer, offset: offset, count: count);
                        await s.ReadExactlyAsync(buffer: buffer, cancellationToken: ct);
                        await s.ReadExactlyAsync(buffer: buffer, cancellationToken: ct);
                        await s.ReadExactlyAsync(buffer: buffer, offset: offset, count: count, cancellationToken: ct);
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task NamedArgumentsSwapped_OffersFixer_CS()
        {
            string source = """
                using System.IO;
                using System.Threading;

                class C
                {
                    async void M(Stream s, byte[] buffer, int offset, int count, CancellationToken ct)
                    {
                        [|s.Read(count: buffer.Length, buffer: buffer, offset: 0)|];
                        [|s.Read(count: count, offset: offset, buffer: buffer)|];
                        await [|s.ReadAsync(offset: 0, buffer: buffer, count: buffer.Length)|];
                        await [|s.ReadAsync(buffer: buffer, count: count, offset: offset)|];
                        await [|s.ReadAsync(count: buffer.Length, buffer: buffer, offset: 0, cancellationToken: ct)|];
                        await [|s.ReadAsync(cancellationToken: ct, buffer: buffer)|];
                        await [|s.ReadAsync(count: count, cancellationToken: ct, buffer: buffer, offset: offset)|];
                    }
                }
                """;

            string fixedSource = """
                using System.IO;
                using System.Threading;

                class C
                {
                    async void M(Stream s, byte[] buffer, int offset, int count, CancellationToken ct)
                    {
                        s.ReadExactly(buffer: buffer);
                        s.ReadExactly(count: count, offset: offset, buffer: buffer);
                        await s.ReadExactlyAsync(buffer: buffer);
                        await s.ReadExactlyAsync(buffer: buffer, count: count, offset: offset);
                        await s.ReadExactlyAsync(buffer: buffer, cancellationToken: ct);
                        await s.ReadExactlyAsync(cancellationToken: ct, buffer: buffer);
                        await s.ReadExactlyAsync(count: count, cancellationToken: ct, buffer: buffer, offset: offset);
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task TriviaIsPreserved_OffersFixer_CS()
        {
            string source = """
                using System.IO;
                using System.Threading;

                class C
                {
                    async void M(Stream s, byte[] buffer, int offset, int count, CancellationToken ct)
                    {
                        // reticulates the splines
                        [|s.Read(buffer, 0, buffer.Length)|];
                        // reticulates the splines
                        await [|s.ReadAsync(buffer, 0, buffer.Length, ct)|];
                        // reticulates the splines
                        [|s.Read(buffer, offset, count)|];
                        // reticulates the splines
                        await [|s.ReadAsync(buffer, offset, count, ct)|];
                    }
                }
                """;

            string fixedSource = """
                using System.IO;
                using System.Threading;

                class C
                {
                    async void M(Stream s, byte[] buffer, int offset, int count, CancellationToken ct)
                    {
                        // reticulates the splines
                        s.ReadExactly(buffer);
                        // reticulates the splines
                        await s.ReadExactlyAsync(buffer, ct);
                        // reticulates the splines
                        s.ReadExactly(buffer, offset, count);
                        // reticulates the splines
                        await s.ReadExactlyAsync(buffer, offset, count, ct);
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task ReadExactlyNotAvailable_ReportsDiagnostic_CS()
        {
            string source = """
                using System.IO;

                class C
                {
                    async void M(Stream s, byte[] buffer)
                    {
                        [|s.Read(buffer)|];
                        await [|s.ReadAsync(buffer)|];
                    }
                }
                """;

            // No code fix because ReadExactly is not available until .NET 7.
            await VerifyCSharpCodeFixAsync(source, source, ReferenceAssemblies.Net.Net60);
        }

        [Fact]
        public async Task ReturnValueIsUsed_NoDiagnostic_CS()
        {
            string source = """
                using System.IO;
                using System.Threading;

                class C
                {
                    async void M(Stream s, byte[] buffer, int offset, int count, CancellationToken ct)
                    {
                        _ = s.Read(buffer);
                        _ = s.Read(buffer, 0, buffer.Length);
                        _ = s.Read(buffer, offset, count);
                        _ = await s.ReadAsync(buffer, 0, buffer.Length);
                        _ = await s.ReadAsync(buffer);
                        _ = await s.ReadAsync(buffer, offset, count);
                        _ = await s.ReadAsync(buffer, 0, buffer.Length, ct);
                        _ = await s.ReadAsync(buffer, ct);
                        _ = await s.ReadAsync(buffer, offset, count, ct);
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, source);
        }

        [Fact]
        public async Task DifferentRead_NoDiagnostic_CS()
        {
            string source = """
                using System.IO;

                class C
                {
                    async void M(Stream s, byte[] buffer, int count)
                    {
                        s.ReadByte();
                        s.ReadAtLeast(buffer, count);
                        await s.ReadAtLeastAsync(buffer, count);
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, source);
        }

        [Fact, WorkItem(7268, "https://github.com/dotnet/roslyn-analyzers/issues/7268")]
        public async Task StreamTypeIsKnownReliable_NoDiagnostic_CS()
        {
            string source = """
                using System.IO;

                class C
                {
                    async void M(byte[] buffer, MemoryStream memoryStream, UnmanagedMemoryStream unmanagedMemoryStream)
                    {
                        memoryStream.Read(buffer);
                        unmanagedMemoryStream.Read(buffer);

                        await memoryStream.ReadAsync(buffer);
                        await unmanagedMemoryStream.ReadAsync(buffer);
                    }
                }
                """;

            await VerifyCSharpCodeFixAsync(source, source);
        }

        [Fact]
        public async Task EntireBuffer_OffersFixer_VB()
        {
            string source = """
                Imports System.IO
                Imports System.Threading

                Class C
                    Async Sub M(s As Stream, buffer As Byte(), ct As CancellationToken)
                        [|s.Read(buffer, 0, buffer.Length)|]
                        [|s.Read(buffer)|]
                        Await [|s.ReadAsync(buffer, 0, buffer.Length)|]
                        Await [|s.ReadAsync(buffer)|]
                        Await [|s.ReadAsync(buffer, 0, buffer.Length, ct)|]
                        Await [|s.ReadAsync(buffer, ct)|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.IO
                Imports System.Threading
                
                Class C
                    Async Sub M(s As Stream, buffer As Byte(), ct As CancellationToken)
                        s.ReadExactly(buffer)
                        s.ReadExactly(buffer)
                        Await [|s.ReadExactlyAsync(buffer)|]
                        Await [|s.ReadExactlyAsync(buffer)|]
                        Await [|s.ReadExactlyAsync(buffer, ct)|]
                        Await [|s.ReadExactlyAsync(buffer, ct)|]
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task WithCount_OffersFixer_VB()
        {
            string source = """
                Imports System.IO
                Imports System.Threading

                Class C
                    Async Sub M(s As Stream, buffer As Byte(), count As Integer, ct As CancellationToken)
                        [|s.Read(buffer, 0, count)|]
                        Await [|s.ReadAsync(buffer, 0, count)|]
                        Await [|s.ReadAsync(buffer, 0, count, ct)|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.IO
                Imports System.Threading

                Class C
                    Async Sub M(s As Stream, buffer As Byte(), count As Integer, ct As CancellationToken)
                        s.ReadExactly(buffer, 0, count)
                        Await s.ReadExactlyAsync(buffer, 0, count)
                        Await s.ReadExactlyAsync(buffer, 0, count, ct)
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task WithOffsetAndCount_OffersFixer_VB()
        {
            string source = """
                Imports System.IO
                Imports System.Threading

                Class C
                    Async Sub M(s As Stream, buffer As Byte(), count As Integer, offset As Integer, ct As CancellationToken)
                        [|s.Read(buffer, offset, count)|]
                        Await [|s.ReadAsync(buffer, offset, count)|]
                        Await [|s.ReadAsync(buffer, offset, count, ct)|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.IO
                Imports System.Threading

                Class C
                    Async Sub M(s As Stream, buffer As Byte(), count As Integer, offset As Integer, ct As CancellationToken)
                        s.ReadExactly(buffer, offset, count)
                        Await s.ReadExactlyAsync(buffer, offset, count)
                        Await s.ReadExactlyAsync(buffer, offset, count, ct)
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task DifferentBufferInstances_OffersFixer_VB()
        {
            string source = """
                Imports System.IO

                Class C
                    Private ReadOnly _bufferField as Byte()
                    Public Property BufferProperty as Byte()

                    Async Sub M(s As Stream, bufferParameter As Byte())
                        Dim bufferLocal as Byte() = { }

                        [|s.Read(bufferParameter, 0, bufferParameter.Length)|]
                        [|s.Read(bufferLocal, 0, bufferLocal.Length)|]
                        [|s.Read(_bufferField, 0, _bufferField.Length)|]
                        [|s.Read(BufferProperty, 0, BufferProperty.Length)|]

                        Await [|s.ReadAsync(bufferParameter, 0, bufferParameter.Length)|]
                        Await [|s.ReadAsync(bufferLocal, 0, bufferLocal.Length)|]
                        Await [|s.ReadAsync(_bufferField, 0, _bufferField.Length)|]
                        Await [|s.ReadAsync(BufferProperty, 0, BufferProperty.Length)|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.IO
                
                Class C
                    Private ReadOnly _bufferField as Byte()
                    Public Property BufferProperty as Byte()
                
                    Async Sub M(s As Stream, bufferParameter As Byte())
                        Dim bufferLocal as Byte() = { }

                        s.ReadExactly(bufferParameter)
                        s.ReadExactly(bufferLocal)
                        s.ReadExactly(_bufferField)
                        s.ReadExactly(BufferProperty)

                        Await s.ReadExactlyAsync(bufferParameter)
                        Await s.ReadExactlyAsync(bufferLocal)
                        Await s.ReadExactlyAsync(_bufferField)
                        Await s.ReadExactlyAsync(BufferProperty)
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task SpecificStreamType_OffersFixer_VB()
        {
            string source = """
                Imports System.IO

                Class C
                    Async Sub M(s As BufferedStream, buffer As Byte())
                        [|s.Read(buffer, 0, buffer.Length)|]
                        Await [|s.ReadAsync(buffer, 0, buffer.Length)|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.IO

                Class C
                    Async Sub M(s As BufferedStream, buffer As Byte())
                        s.ReadExactly(buffer)
                        Await s.ReadExactlyAsync(buffer)
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task NamedArguments_OffersFixer_VB()
        {
            string source = """
                Imports System.IO
                Imports System.Threading

                Class C
                    Async Sub M(s As Stream, buffer As Byte(), count As Integer, offset As Integer, ct As CancellationToken)
                        [|s.Read(buffer:=buffer, offset:=0, count:=buffer.Length)|]
                        [|s.Read(buffer:=buffer)|]
                        [|s.Read(buffer:=buffer, offset:=offset, count:=count)|]
                        Await [|s.ReadAsync(buffer:=buffer, offset:=0, count:=buffer.Length)|]
                        Await [|s.ReadAsync(buffer:=buffer)|]
                        Await [|s.ReadAsync(buffer:=buffer, offset:=offset, count:=count)|]
                        Await [|s.ReadAsync(buffer:=buffer, offset:=0, count:=buffer.Length, cancellationToken:=ct)|]
                        Await [|s.ReadAsync(buffer:=buffer, cancellationToken:=ct)|]
                        Await [|s.ReadAsync(buffer:=buffer, offset:=offset, count:=count, cancellationToken:=ct)|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.IO
                Imports System.Threading

                Class C
                    Async Sub M(s As Stream, buffer As Byte(), count As Integer, offset As Integer, ct As CancellationToken)
                        s.ReadExactly(buffer:=buffer)
                        s.ReadExactly(buffer:=buffer)
                        s.ReadExactly(buffer:=buffer, offset:=offset, count:=count)
                        Await s.ReadExactlyAsync(buffer:=buffer)
                        Await s.ReadExactlyAsync(buffer:=buffer)
                        Await s.ReadExactlyAsync(buffer:=buffer, offset:=offset, count:=count)
                        Await s.ReadExactlyAsync(buffer:=buffer, cancellationToken:=ct)
                        Await s.ReadExactlyAsync(buffer:=buffer, cancellationToken:=ct)
                        Await s.ReadExactlyAsync(buffer:=buffer, offset:=offset, count:=count, cancellationToken:=ct)
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, fixedSource);
        }

        // Order of arguments in IOperation is based on evaluation order, which seems to be parameter order for VB.
        [Fact, WorkItem(3655, "https://github.com/dotnet/roslyn-analyzers/issues/3655")]
        public async Task NamedArgumentsSwapped_OffersFixer_VB()
        {
            string source = """
                Imports System.IO
                Imports System.Threading
                
                Class C
                    Async Sub M(s As Stream, buffer As Byte(), count As Integer, offset As Integer, ct As CancellationToken)
                        [|s.Read(count:=buffer.Length, buffer:=buffer, offset:=0)|]
                        [|s.Read(count:=count, offset:=offset, buffer:=buffer)|]
                        Await [|s.ReadAsync(offset:=0, buffer:=buffer, count:=buffer.Length)|]
                        Await [|s.ReadAsync(buffer:=buffer, count:=count, offset:=offset)|]
                        Await [|s.ReadAsync(count:=buffer.Length, buffer:=buffer, offset:=0, cancellationToken:=ct)|]
                        Await [|s.ReadAsync(cancellationToken:=ct, buffer:=buffer)|]
                        Await [|s.ReadAsync(count:=count, cancellationToken:=ct, buffer:=buffer, offset:=offset)|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.IO
                Imports System.Threading
                
                Class C
                    Async Sub M(s As Stream, buffer As Byte(), count As Integer, offset As Integer, ct As CancellationToken)
                        s.ReadExactly(buffer:=buffer)
                        s.ReadExactly(buffer:=buffer, offset:=offset, count:=count)
                        Await s.ReadExactlyAsync(buffer:=buffer)
                        Await s.ReadExactlyAsync(buffer:=buffer, offset:=offset, count:=count)
                        Await s.ReadExactlyAsync(buffer:=buffer, cancellationToken:=ct)
                        Await s.ReadExactlyAsync(buffer:=buffer, cancellationToken:=ct)
                        Await s.ReadExactlyAsync(buffer:=buffer, offset:=offset, count:=count, cancellationToken:=ct)
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task TriviaIsPreserved_OffersFixer_VB()
        {
            string source = """
                Imports System.IO
                Imports System.Threading
                
                Class C
                    Async Sub M(s As Stream, buffer As Byte(), count As Integer, offset As Integer, ct As CancellationToken)
                        ' reticulates the splines
                        [|s.Read(buffer, 0, buffer.Length)|]
                        ' reticulates the splines
                        Await [|s.ReadAsync(buffer, 0, buffer.Length, ct)|]
                        ' reticulates the splines
                        [|s.Read(buffer, offset, count)|]
                        ' reticulates the splines
                        Await [|s.ReadAsync(buffer, offset, count, ct)|]
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.IO
                Imports System.Threading
                
                Class C
                    Async Sub M(s As Stream, buffer As Byte(), count As Integer, offset As Integer, ct As CancellationToken)
                        ' reticulates the splines
                        s.ReadExactly(buffer)
                        ' reticulates the splines
                        Await s.ReadExactlyAsync(buffer, ct)
                        ' reticulates the splines
                        s.ReadExactly(buffer, offset, count)
                        ' reticulates the splines
                        Await s.ReadExactlyAsync(buffer, offset, count, ct)
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task ReadExactlyNotAvailable_ReportsDiagnostic_VB()
        {
            string source = """
                Imports System.IO

                Class C
                    Async Sub M(s As Stream, buffer As Byte())
                        [|s.Read(buffer)|]
                        Await [|s.ReadAsync(buffer)|]
                    End Sub
                End Class
                """;

            // No code fix because ReadExactly is not available until .NET 7.
            await VerifyBasicCodeFixAsync(source, source, ReferenceAssemblies.Net.Net60);
        }

        [Fact]
        public async Task ReturnValueIsUsed_NoDiagnostic_VB()
        {
            string source = """
                Imports System.IO
                Imports System.Threading
                
                Class C
                    Async Sub M(s As Stream, buffer As Byte(), count As Integer, offset As Integer, ct As CancellationToken)
                        Dim bytesRead As Integer
                        bytesRead = s.Read(buffer)
                        bytesRead = s.Read(buffer, 0, buffer.Length)
                        bytesRead = s.Read(buffer, offset, count)
                        bytesRead = Await s.ReadAsync(buffer, 0, buffer.Length)
                        bytesRead = Await s.ReadAsync(buffer)
                        bytesRead = Await s.ReadAsync(buffer, offset, count)
                        bytesRead = Await s.ReadAsync(buffer, 0, buffer.Length, ct)
                        bytesRead = Await s.ReadAsync(buffer, ct)
                        bytesRead = Await s.ReadAsync(buffer, offset, count, ct)
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, source);
        }

        [Fact]
        public async Task DifferentRead_NoDiagnostic_VB()
        {
            string source = """
                Imports System.IO

                Class C
                    Async Sub M(s As Stream, buffer As Byte(), count As Integer)
                        s.ReadByte()
                        s.ReadAtLeast(buffer, count)
                        Await s.ReadAtLeastAsync(buffer, count)
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, source);
        }

        [Fact, WorkItem(7268, "https://github.com/dotnet/roslyn-analyzers/issues/7268")]
        public async Task StreamTypeIsKnownReliable_NoDiagnostic_VB()
        {
            string source = """
                Imports System.IO

                Class C
                    Async Sub M(buffer As Byte(), memoryStream As MemoryStream, unmanagedMemoryStream As UnmanagedMemoryStream)
                        memoryStream.Read(buffer)
                        unmanagedMemoryStream.Read(buffer)

                        await memoryStream.ReadAsync(buffer)
                        await unmanagedMemoryStream.ReadAsync(buffer)
                    End Sub
                End Class
                """;

            await VerifyBasicCodeFixAsync(source, source);
        }

        private static async Task VerifyCSharpCodeFixAsync(string source, string fixedSource, ReferenceAssemblies referenceAssemblies = null)
        {
            await new VerifyCS.Test
            {
                TestCode = source,
                FixedCode = fixedSource,
                ReferenceAssemblies = referenceAssemblies ?? ReferenceAssemblies.Net.Net70
            }.RunAsync();
        }

        private static async Task VerifyBasicCodeFixAsync(string source, string fixedSource, ReferenceAssemblies referenceAssemblies = null)
        {
            await new VerifyVB.Test
            {
                TestCode = source,
                FixedCode = fixedSource,
                ReferenceAssemblies = referenceAssemblies ?? ReferenceAssemblies.Net.Net70
            }.RunAsync();
        }
    }
}
