// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;

using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DoNotUseEndOfStreamInAsyncMethodsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DoNotUseEndOfStreamInAsyncMethodsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class DoNotUseEndOfStreamInAsyncMethodsTests
    {
        [Fact]
        public async Task AsyncMethod_ReportsDiagnostic_CS()
        {
            string source = """
                using System.IO;
                using System.Threading.Tasks;

                class C
                {
                    private StreamReader _field;

                    public StreamReader Property { get; set; }

                    async Task M(StreamReader parameter, Stream stream)
                    {
                        var local = new StreamReader(stream);

                        _ = {|CA2024:local.EndOfStream|};
                        _ = {|CA2024:parameter.EndOfStream|};
                        _ = {|CA2024:_field.EndOfStream|};
                        _ = {|CA2024:Property.EndOfStream|};
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task AsyncLocalMethodInAsyncMethod_ReportsDiagnostic_CS()
        {
            string source = """
                using System.IO;
                using System.Threading.Tasks;

                class C
                {
                    private StreamReader _field;

                    public StreamReader Property { get; set; }

                    async Task M(StreamReader parameter, Stream stream)
                    {
                        var local = new StreamReader(stream);

                        async Task LocalMethod()
                        {
                            _ = {|CA2024:local.EndOfStream|};
                            _ = {|CA2024:parameter.EndOfStream|};
                            _ = {|CA2024:_field.EndOfStream|};
                            _ = {|CA2024:Property.EndOfStream|};
                        }
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task AsyncLocalMethodInSyncMethod_ReportsDiagnostic_CS()
        {
            string source = """
                using System.IO;
                using System.Threading.Tasks;

                class C
                {
                    private StreamReader _field;

                    public StreamReader Property { get; set; }

                    void M(StreamReader parameter, Stream stream)
                    {
                        var local = new StreamReader(stream);

                        async Task LocalMethod()
                        {
                            _ = {|CA2024:local.EndOfStream|};
                            _ = {|CA2024:parameter.EndOfStream|};
                            _ = {|CA2024:_field.EndOfStream|};
                            _ = {|CA2024:Property.EndOfStream|};
                        }
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task AsyncLambdaExpressionInAsyncMethod_ReportsDiagnostic_CS()
        {
            string source = """
                using System;
                using System.IO;
                using System.Threading.Tasks;

                class C
                {
                    async Task M()
                    {
                        Func<StreamReader, Task<bool>> func = async (StreamReader sr) => {|CA2024:sr.EndOfStream|};
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task AsyncLambdaExpressionInSyncMethod_ReportsDiagnostic_CS()
        {
            string source = """
                using System;
                using System.IO;
                using System.Threading.Tasks;

                class C
                {
                    void M()
                    {
                        Func<StreamReader, Task<bool>> func = async (StreamReader sr) => {|CA2024:sr.EndOfStream|};
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task AsyncAnonymousMethodInAsyncMethod_ReportsDiagnostic_CS()
        {
            string source = """
                using System;
                using System.IO;
                using System.Threading.Tasks;

                class C
                {
                    async Task M()
                    {
                        Func<StreamReader, Task<bool>> func = async delegate (StreamReader sr)
                        {
                            return {|CA2024:sr.EndOfStream|};
                        };
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task AsyncAnonymousMethodInSyncMethod_ReportsDiagnostic_CS()
        {
            string source = """
                using System;
                using System.IO;
                using System.Threading.Tasks;

                class C
                {
                    void M()
                    {
                        Func<StreamReader, Task<bool>> func = async delegate (StreamReader sr)
                        {
                            return {|CA2024:sr.EndOfStream|};
                        };
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task SyncMethod_NoDiagnostic_CS()
        {
            string source = """
                using System.IO;

                class C
                {
                    bool M(StreamReader sr)
                    {
                        return sr.EndOfStream;
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task SyncLocalMethodInAsyncMethod_NoDiagnostic_CS()
        {
            string source = """
                using System.IO;
                using System.Threading.Tasks;

                class C
                {
                    async Task M(StreamReader sr)
                    {
                        bool LocalMethod()
                        {
                            return sr.EndOfStream;
                        }
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task SyncLocalMethodInSyncMethod_NoDiagnostic_CS()
        {
            string source = """
                using System.IO;

                class C
                {
                    void M(StreamReader sr)
                    {
                        bool LocalMethod()
                        {
                            return sr.EndOfStream;
                        }
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task SyncLambdaExpressionInAsyncMethod_NoDiagnostic_CS()
        {
            string source = """
                using System;
                using System.IO;
                using System.Threading.Tasks;

                class C
                {
                    async Task M()
                    {
                        Func<StreamReader, bool> func = (StreamReader sr) => sr.EndOfStream;
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task SyncLambdaExpressionInSyncMethod_NoDiagnostic_CS()
        {
            string source = """
                using System;
                using System.IO;

                class C
                {
                    void M()
                    {
                        Func<StreamReader, bool> func = (StreamReader sr) => sr.EndOfStream;
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task SyncAnonymousMethodInAsyncMethod_NoDiagnostic_CS()
        {
            string source = """
                using System;
                using System.IO;
                using System.Threading.Tasks;

                class C
                {
                    async Task M()
                    {
                        Func<StreamReader, bool> func = delegate (StreamReader sr)
                        {
                            return sr.EndOfStream;
                        };
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task SyncAnonymousMethodInSyncMethod_NoDiagnostic_CS()
        {
            string source = """
                using System;
                using System.IO;

                class C
                {
                    void M()
                    {
                        Func<StreamReader, bool> func = delegate (StreamReader sr)
                        {
                            return sr.EndOfStream;
                        };
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task NotAStreamReaderProperty_NoDiagnostic_CS()
        {
            string source = """
                using System.Threading.Tasks;

                class OtherReader
                {
                    public bool EndOfStream { get; } = false;
                }

                class C
                {
                    async Task<bool> M(OtherReader otherReader)
                    {
                        return otherReader.EndOfStream;
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task AsyncMethod_ReportsDiagnostic_VB()
        {
            string source = """
                Imports System.IO
                Imports System.Threading.Tasks

                Class C
                    Private _field As StreamReader
                    Public Property [Property] As StreamReader

                    Async Function M(ByVal parameter As StreamReader, ByVal stream As Stream) As Task
                        Dim local = New StreamReader(stream)

                        Dim __ = {|CA2024:local.EndOfStream|}
                        __ = {|CA2024:parameter.EndOfStream|}
                        __ = {|CA2024:_field.EndOfStream|}
                        __ = {|CA2024:[Property].EndOfStream|}
                    End Function
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task AsyncLambdaExpressionInAsyncMethod_ReportsDiagnostic_VB()
        {
            string source = """
                Imports System.IO
                Imports System.Threading.Tasks

                Class C
                    Async Function M() As Task
                        Dim func = Async Function(sr As StreamReader) {|CA2024:sr.EndOfStream|}
                    End Function
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task AsyncLambdaExpressionInSyncMethod_ReportsDiagnostic_VB()
        {
            string source = """
                Imports System.IO

                Class C
                    Sub M()
                        Dim func = Async Function(sr As StreamReader) {|CA2024:sr.EndOfStream|}
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task SyncMethod_NoDiagnostic_VB()
        {
            string source = """
                Imports System.IO

                Class C
                    Function M(sr As StreamReader) As Boolean
                        return sr.EndOfStream
                    End Function
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task SyncLambdaExpressionInAsyncMethod_NoDiagnostic_VB()
        {
            string source = """
                Imports System.IO
                Imports System.Threading.Tasks

                Class C
                    Async Function M() As Task
                        Dim func = Function(sr As StreamReader) sr.EndOfStream
                    End Function
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task SyncLambdaExpressionInSyncMethod_NoDiagnostic_VB()
        {
            string source = """
                Imports System.IO
                Imports System.Threading.Tasks

                Class C
                    Sub M()
                        Dim func = Function(sr As StreamReader) sr.EndOfStream
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task NotAStreamReaderProperty_NoDiagnostic_VB()
        {
            string source = """
                Imports System.IO
                Imports System.Threading.Tasks

                Class OtherReader
                    Public ReadOnly Property EndOfStream As Boolean = False
                End Class

                Class C
                    Async Function M(otherReader As OtherReader) As Task(Of Boolean)
                        Return otherReader.EndOfStream
                    End Function
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }
    }
}
