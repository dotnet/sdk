// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.DoNotGuardDictionaryRemoveByContainsKey,
    Microsoft.NetCore.CSharp.Analyzers.Performance.CSharpDoNotGuardDictionaryRemoveByContainsKeyFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.DoNotGuardDictionaryRemoveByContainsKey,
    Microsoft.NetCore.VisualBasic.Analyzers.Performance.BasicDoNotGuardDictionaryRemoveByContainsKeyFixer>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public class DoNotGuardDictionaryRemoveByContainsKeyTests
    {
        #region Tests
        [Fact]
        public async Task NonInvocationConditionDoesNotThrow_CS()
        {
            string source = CSUsings + CSNamespaceAndClassStart + @"
        public MyClass()
        {
            if (!true) { }
        }" + CSNamespaceAndClassEnd;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task RemoveIsTheOnlyStatement_OffersFixer_CS()
        {
            string source = CSUsings + CSNamespaceAndClassStart + @"
        private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

        public MyClass()
        {
            if ([|MyDictionary.ContainsKey(""Key"")|])
                MyDictionary.Remove(""Key"");
        }" + CSNamespaceAndClassEnd;

            string fixedSource = CSUsings + CSNamespaceAndClassStart + @"
        private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

        public MyClass()
        {
            MyDictionary.Remove(""Key"");
        }" + CSNamespaceAndClassEnd;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveWithOutValueIsTheOnlyStatement_OffersFixer_CS()
        {
            string source = CSUsings + CSNamespaceAndClassStart + @"
        private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

        public MyClass()
        {
            if ([|MyDictionary.ContainsKey(""Key"")|])
                MyDictionary.Remove(""Key"", out var value);
        }" + CSNamespaceAndClassEnd;

            string fixedSource = CSUsings + CSNamespaceAndClassStart + @"
        private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

        public MyClass()
        {
            MyDictionary.Remove(""Key"", out var value);
        }" + CSNamespaceAndClassEnd;

            await new VerifyCS.Test
            {
                TestCode = source,
                FixedCode = fixedSource,
                ReferenceAssemblies = ReferenceAssemblies.NetCore.NetCoreApp31
            }.RunAsync();
        }

        [Fact]
        public async Task RemoveIsTheOnlyStatementInABlock_OffersFixer_CS()
        {
            string source = CSUsings + CSNamespaceAndClassStart + @"
        private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

        public MyClass()
        {
            if ([|MyDictionary.ContainsKey(""Key"")|])
            {
                MyDictionary.Remove(""Key"");
            }
        }" + CSNamespaceAndClassEnd;

            string fixedSource = CSUsings + CSNamespaceAndClassStart + @"
        private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

        public MyClass()
        {
            MyDictionary.Remove(""Key"");
        }" + CSNamespaceAndClassEnd;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        [WorkItem(6274, "https://github.com/dotnet/roslyn-analyzers/issues/6274")]
        public async Task RemoveIsTheOnlyStatementInTernaryOperator_NoDiagnostic_CS()
        {
            string source = CSUsings + CSNamespaceAndClassStart + @"
        private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

        public MyClass()
        {
            bool test = MyDictionary.ContainsKey(""Key"") ? MyDictionary.Remove(""Key"") : false;
        }" + CSNamespaceAndClassEnd;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task HasElseBlock_OffersFixer_CS()
        {
            string source = CSUsings + CSNamespaceAndClassStart + @"
        private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

        public MyClass()
        {
            if ([|MyDictionary.ContainsKey(""Key"")|])
            {
                MyDictionary.Remove(""Key"");
            }
            else
            {
                throw new Exception(""Key doesn't exist"");
            }
        }" + CSNamespaceAndClassEnd;

            string fixedSource = CSUsings + CSNamespaceAndClassStart + @"
        private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

        public MyClass()
        {
            if (!MyDictionary.Remove(""Key""))
            {
                throw new Exception(""Key doesn't exist"");
            }
        }" + CSNamespaceAndClassEnd;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task HasElseStatement_OffersFixer_CS()
        {
            string source = CSUsings + CSNamespaceAndClassStart + @"
        private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

        public MyClass()
        {
            if ([|MyDictionary.ContainsKey(""Key"")|])
                MyDictionary.Remove(""Key"");
            else
                throw new Exception(""Key doesn't exist"");
        }" + CSNamespaceAndClassEnd;

            string fixedSource = CSUsings + CSNamespaceAndClassStart + @"
        private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

        public MyClass()
        {
            if (!MyDictionary.Remove(""Key""))
                throw new Exception(""Key doesn't exist"");
        }" + CSNamespaceAndClassEnd;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        [WorkItem(6274, "https://github.com/dotnet/roslyn-analyzers/issues/6274")]
        public async Task NegatedCondition_ReportsDiagnostic_CS()
        {
            string source = CSUsings + CSNamespaceAndClassStart + @"
        private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

        public MyClass()
        {
            if (![|MyDictionary.ContainsKey(""Key"")|])
                MyDictionary.Remove(""Key"");
        }" + CSNamespaceAndClassEnd;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task AdditionalCondition_NoDiagnostic_CS()
        {
            string source = CSUsings + CSNamespaceAndClassStart + @"
        private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

        public MyClass()
        {
            if (MyDictionary.ContainsKey(""Key"") && MyDictionary.Count > 2)
                MyDictionary.Remove(""Key"");
        }" + CSNamespaceAndClassEnd;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task ConditionInVariable_NoDiagnostic_CS()
        {
            string source = CSUsings + CSNamespaceAndClassStart + @"
        private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

        public MyClass()
        {
            var result = MyDictionary.ContainsKey(""Key"");
            if (result)
	            MyDictionary.Remove(""Key"");
        }" + CSNamespaceAndClassEnd;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task RemoveInSeparateLine_NoDiagnostic_CS()
        {
            string source = CSUsings + CSNamespaceAndClassStart + @"
        private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

        public MyClass()
        {
            if (MyDictionary.ContainsKey(""Key""))
	            _ = MyDictionary.Count;
	        MyDictionary.Remove(""Key"");
        }" + CSNamespaceAndClassEnd;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task NotDictionaryRemove_NoDiagnostic_CS()
        {
            string source = CSUsings + CSNamespaceAndClassStart + @"
        private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();
        private bool Remove(string key) => false;

        public MyClass()
        {
            if (MyDictionary.ContainsKey(""Key""))
	            Remove(""Key"");
        }" + CSNamespaceAndClassEnd;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        [WorkItem(6274, "https://github.com/dotnet/roslyn-analyzers/issues/6274")]
        public async Task AdditionalStatements_ReportsDiagnostic_CS()
        {
            string source = CSUsings + CSNamespaceAndClassStart + @"
        private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

        public MyClass()
        {
            if ([|MyDictionary.ContainsKey(""Key"")|])
            {
                MyDictionary.Remove(""Key"");
                Console.WriteLine();
            }
        }
        " + CSNamespaceAndClassEnd;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task TriviaIsPreserved_CS()
        {
            string source = CSUsings + CSNamespaceAndClassStart + @"
        private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

        public MyClass()
        {
            // reticulates the splines
            if ([|MyDictionary.ContainsKey(""Key"")|])
            {
                MyDictionary.Remove(""Key"");
            }
        }" + CSNamespaceAndClassEnd;

            string fixedSource = CSUsings + CSNamespaceAndClassStart + @"
        private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

        public MyClass()
        {
            // reticulates the splines
            MyDictionary.Remove(""Key"");
        }" + CSNamespaceAndClassEnd;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveIsTheOnlyStatement_OffersFixer_VB()
        {
            string source = @"
" + VBUsings + @"
Namespace Testopolis
    Public Class SomeClass
        Public MyDictionary As New Dictionary(Of String, String)()

        Public Sub New()
            If ([|MyDictionary.ContainsKey(""Key"")|]) Then MyDictionary.Remove(""Key"")
        End Sub
    End Class
End Namespace";

            string fixedSource = @"
" + VBUsings + @"
Namespace Testopolis
    Public Class SomeClass
        Public MyDictionary As New Dictionary(Of String, String)()

        Public Sub New()
            MyDictionary.Remove(""Key"")
        End Sub
    End Class
End Namespace";

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveIsTheOnlyStatementInABlock_OffersFixer_VB()
        {
            string source = @"
" + VBUsings + @"
Namespace Testopolis
    Public Class SomeClass
        Public MyDictionary As New Dictionary(Of String, String)()

        Public Sub New()
            If ([|MyDictionary.ContainsKey(""Key"")|]) Then
                MyDictionary.Remove(""Key"")
            End If
        End Sub
    End Class
End Namespace";

            string fixedSource = @"
" + VBUsings + @"
Namespace Testopolis
    Public Class SomeClass
        Public MyDictionary As New Dictionary(Of String, String)()

        Public Sub New()
            MyDictionary.Remove(""Key"")
        End Sub
    End Class
End Namespace";

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        [WorkItem(6274, "https://github.com/dotnet/roslyn-analyzers/issues/6274")]
        public async Task RemoveIsTheOnlyStatementInTernaryOperator_NoDiagnostic_VB()
        {
            string source = @"
" + VBUsings + @"
Namespace Testopolis
    Public Class SomeClass
        Public MyDictionary As New Dictionary(Of String, String)()

        Public Sub New()
            Dim test As Boolean = If(MyDictionary.ContainsKey(""Key""), MyDictionary.Remove(""Key""), False)
        End Sub
    End Class
End Namespace";

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task HasElseBlock_OffersFixer_VB()
        {
            string source = @"
" + VBUsings + @"
Namespace Testopolis
    Public Class SomeClass
        Public MyDictionary As New Dictionary(Of String, String)()

        Public Sub New()
            If [|MyDictionary.ContainsKey(""Key"")|] Then
                MyDictionary.Remove(""Key"")
            Else
                Throw new Exception(""Key doesn't exist"")
            End If
        End Sub
    End Class
End Namespace";

            string fixedSource = @"
" + VBUsings + @"
Namespace Testopolis
    Public Class SomeClass
        Public MyDictionary As New Dictionary(Of String, String)()

        Public Sub New()
            If Not MyDictionary.Remove(""Key"") Then
                Throw new Exception(""Key doesn't exist"")
            End If
        End Sub
    End Class
End Namespace";

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task HasElseStatement_OffersFixer_VB()
        {
            string source = @"
" + VBUsings + @"
Namespace Testopolis
    Public Class SomeClass
        Public MyDictionary As New Dictionary(Of String, String)()

        Public Sub New()
            If [|MyDictionary.ContainsKey(""Key"")|] Then MyDictionary.Remove(""Key"") Else Throw new Exception(""Key doesn't exist"")
        End Sub
    End Class
End Namespace";

            string fixedSource = @"
" + VBUsings + @"
Namespace Testopolis
    Public Class SomeClass
        Public MyDictionary As New Dictionary(Of String, String)()

        Public Sub New()
            If Not MyDictionary.Remove(""Key"") Then Throw new Exception(""Key doesn't exist"")
        End Sub
    End Class
End Namespace";

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task NegatedCondition_ReportsDiagnostic_VB()
        {
            string source = @"
" + VBUsings + @"
Namespace Testopolis
    Public Class SomeClass
        Public MyDictionary As New Dictionary(Of String, String)()

        Public Sub New()
            If Not [|MyDictionary.ContainsKey(""Key"")|] Then
                MyDictionary.Remove(""Key"")
            End If
        End Sub
    End Class
End Namespace";

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        [WorkItem(6274, "https://github.com/dotnet/roslyn-analyzers/issues/6274")]
        public async Task AdditionalStatements_ReportsDiagnostic_VB()
        {
            string source = @"
" + VBUsings + @"
Namespace Testopolis
    Public Class SomeClass
        Public MyDictionary As New Dictionary(Of String, String)()

        Public Sub New()
            If [|MyDictionary.ContainsKey(""Key"")|] Then
                MyDictionary.Remove(""Key"")
                Console.WriteLine()
            End If
        End Sub
    End Class
End Namespace";

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task TriviaIsPreserved_VB()
        {
            string source = @"
" + VBUsings + @"
Namespace Testopolis
    Public Class SomeClass
        Public MyDictionary As New Dictionary(Of String, String)()

        Public Sub New()
            ' reticulates the splines
            If ([|MyDictionary.ContainsKey(""Key"")|]) Then
                MyDictionary.Remove(""Key"")
            End If
        End Sub
    End Class
End Namespace";

            string fixedSource = @"
" + VBUsings + @"
Namespace Testopolis
    Public Class SomeClass
        Public MyDictionary As New Dictionary(Of String, String)()

        Public Sub New()
            ' reticulates the splines
            MyDictionary.Remove(""Key"")
        End Sub
    End Class
End Namespace";

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        [WorkItem(6377, "https://github.com/dotnet/roslyn-analyzers/issues/6377")]
        public async Task ContainsKeyAndRemoveCalledOnDifferentInstances_NoDiagnostic_CS()
        {
            string source = CSUsings + CSNamespaceAndClassStart + @"
        private readonly Dictionary<string, string> DictionaryField1 = new Dictionary<string, string>();
        private readonly Dictionary<string, string> DictionaryField2 = new Dictionary<string, string>();

        public Dictionary<string, string> DictionaryProperty1 { get; } = new Dictionary<string, string>();

        public MyClass()
        {
            if (DictionaryField2.ContainsKey(""Key""))
                DictionaryField1.Remove(""Key"");

            if (!DictionaryField1.ContainsKey(""Key""))
            {
                DictionaryField2.Remove(""Key"");
            }

            if (DictionaryProperty1.ContainsKey(""Key""))
                DictionaryField1.Remove(""Key"");

            if (!DictionaryField1.ContainsKey(""Key""))
            {
                DictionaryProperty1.Remove(""Key"");
            }

            var myDictionaryLocal4 = new Dictionary<string, string>();
            if (myDictionaryLocal4.ContainsKey(""Key""))
                DictionaryField1.Remove(""Key"");

            if (!DictionaryField1.ContainsKey(""Key""))
            {
                myDictionaryLocal4.Remove(""Key"");
            }
        }

        private void RemoveItem(Dictionary<string, string> dictionaryParam)
        {
            if (dictionaryParam.ContainsKey(""Key""))
                DictionaryField1.Remove(""Key"");

            if (!DictionaryField1.ContainsKey(""Key""))
            {
                dictionaryParam.Remove(""Key"");
            }
        }" + CSNamespaceAndClassEnd;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        #endregion

        #region Helpers
        private const string CSUsings = @"using System;
using System.Collections.Generic;";

        private const string CSNamespaceAndClassStart = @"namespace Testopolis
{
    public class MyClass
    {
";

        private const string CSNamespaceAndClassEnd = @"
    }
}";

        private const string VBUsings = @"Imports System
Imports System.Collections.Generic";
        #endregion
    }
}
