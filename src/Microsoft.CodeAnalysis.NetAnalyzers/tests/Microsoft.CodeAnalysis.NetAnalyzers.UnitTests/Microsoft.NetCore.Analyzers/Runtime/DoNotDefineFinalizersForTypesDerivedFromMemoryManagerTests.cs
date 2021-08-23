// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DoNotDefineFinalizersForTypesDerivedFromMemoryManager,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DoNotDefineFinalizersForTypesDerivedFromMemoryManager,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class DoNotDefineFinalizersForTypesDerivedFromMemoryManagerTests
    {
        [Fact]
        public async Task ClassNotDerivedFromMemoryManagerOKAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System;

                namespace TestNamespace
                {
                    class TestClass
                    {
                        private void TestMethod() { }
                    }
                }");

            await VerifyVB.VerifyAnalyzerAsync(@"
                Imports System

                Namespace TestNamespace
                    Class TestClass
                        Private Sub TestMethod()
                        End Sub
                    End Class
                End Namespace");
        }

        [Fact]
        public async Task ClassHavingFinalizerButNotDerivedFromMemoryManagerOKAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System;

                namespace TestNamespace
                {
                    class TestClass
                    {
                        private void TestMethod()
                        {
                        }

                        ~TestClass() 
                        {
                            TestMethod();
                        }
                    }
                }");

            await VerifyVB.VerifyAnalyzerAsync(@"
                Imports System

                Namespace TestNamespace
                    Class TestClass
                        Private Sub TestMethod()
                        End Sub

                        Protected Overrides Sub Finalize()
                            TestMethod()
                        End Sub
                    End Class
                End Namespace");
        }

        [Fact]
        public async Task ClassDerivedFromMemoryManagerNoFinilizerOKAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System;
                using System.Buffers;

                namespace TestNamespace
                {
                    abstract class TestClass<T> : MemoryManager<T>
                    {
                    }
                }");

            await VerifyVB.VerifyAnalyzerAsync(@"
                Imports System
                Imports System.Buffers

                Namespace TestNamespace
                    MustInherit Class TestClass(Of T)
                        Inherits MemoryManager(Of T)
                    End Class
                End Namespace
            ");
        }

        [Fact]
        public async Task ClassDerivedFromMemoryManagerWithFinilizerWarnsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System;
                using System.Buffers;

                 namespace TestNamespace
                 {
                    class TestClass<T> : MemoryManager<T>
                    {
                        public override Span<T> GetSpan()
                        {
                            throw new NotImplementedException();
                        }

                        public override MemoryHandle Pin(int elementIndex = 0)
                        {
                            throw new NotImplementedException();
                        }

                        public override void Unpin() { }

                        ~[|TestClass|](){ }

                        protected override void Dispose(bool disposing) { }
                    }
                }");

            await VerifyVB.VerifyAnalyzerAsync(@"
                Imports System
                Imports System.Buffers

                Namespace TestNamespace
                    MustInherit Class TestClass(Of T)
                        Inherits MemoryManager(Of T)

                        Public Overrides Function Pin(ByVal Optional elementIndex As Integer = 0) As MemoryHandle
                            Throw New NotImplementedException()
                        End Function

                        Public Overrides Sub Unpin()
                        End Sub

                        Protected Overrides Sub [|Finalize|]()
                        End Sub

                        Protected Overrides Sub Dispose(ByVal disposing As Boolean)
                        End Sub
                    End Class
                End Namespace");
        }

        [Fact]
        public async Task ClassIndirectlyDerivedFromMemoryManagerWithFinilizerWarnsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System;
                using System.Buffers;

                 namespace TestNamespace
                 {
                    class Deeper<T> : Middle<T>
                    {
                        ~[|Deeper|]() { }
                    }

                    class Middle<T> : MemoryManager<T>
                    {
                        public override Span<T> GetSpan()
                        {
                            throw new NotImplementedException();
                        }

                        public override MemoryHandle Pin(int elementIndex = 0)
                        {
                            throw new NotImplementedException();
                        }

                        public override void Unpin() { }

                        protected override void Dispose(bool disposing) { }
                    }
                }");

            await VerifyVB.VerifyAnalyzerAsync(@"
                Imports System
                Imports System.Buffers

                Namespace TestNamespace
                        MustInherit Class Deeper(Of T)
                        Inherits Middle(Of T)

                        Protected Overrides Sub [|Finalize|]()
                        End Sub
                    End Class

                    MustInherit Class Middle(Of T)
                        Inherits MemoryManager(Of T)

                        Public Overrides Function Pin(ByVal Optional elementIndex As Integer = 0) As MemoryHandle
                            Throw New NotImplementedException()
                        End Function

                        Public Overrides Sub Unpin()
                        End Sub

                        Protected Overrides Sub Dispose(ByVal disposing As Boolean)
                        End Sub
                    End Class
                End Namespace");
        }
    }
}
