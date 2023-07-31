// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Test.Utilities;
using Xunit;

using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.DoNotGuardSetAddOrRemoveByContains,
    Microsoft.NetCore.CSharp.Analyzers.Performance.CSharpDoNotGuardSetAddOrRemoveByContainsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.DoNotGuardSetAddOrRemoveByContains,
    Microsoft.NetCore.VisualBasic.Analyzers.Performance.BasicDoNotGuardSetAddOrRemoveByContainsFixer>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public class DoNotGuardSetAddOrRemoveByContainsTests
    {
        #region Tests
        [Fact]
        public async Task NonInvocationConditionDoesNotThrow_CS()
        {
            string source = """
                class C
                {
                    void M()
                    {
                        if (!true) { }
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task AddIsTheOnlyStatement_OffersFixer_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if (![|MySet.Contains("Item")|])
                            MySet.Add("Item");
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        MySet.Add("Item");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveIsTheOnlyStatement_OffersFixer_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if ([|MySet.Contains("Item")|])
                            MySet.Remove("Item");
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        MySet.Remove("Item");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task AddIsTheOnlyStatementInBlock_OffersFixer_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if (![|MySet.Contains("Item")|])
                        {
                            MySet.Add("Item");
                        }
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        MySet.Add("Item");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveIsTheOnlyStatementInBlock_OffersFixer_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if ([|MySet.Contains("Item")|])
                        {
                            MySet.Remove("Item");
                        }
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        MySet.Remove("Item");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task AddHasElseStatement_OffersFixer_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if (![|MySet.Contains("Item")|])
                            MySet.Add("Item");
                        else
                            throw new System.Exception("Item already exists");
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if (!MySet.Add("Item"))
                            throw new System.Exception("Item already exists");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveHasElseStatement_OffersFixer_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if ([|MySet.Contains("Item")|])
                            MySet.Remove("Item");
                        else
                            throw new System.Exception("Item doesn't exist");
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if (!MySet.Remove("Item"))
                            throw new System.Exception("Item doesn't exist");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task AddWhenFalseHasElseStatement_OffersFixer_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if ([|MySet.Contains("Item")|])
                            throw new System.Exception("Item already exists");
                        else
                            MySet.Add("Item");
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if (!MySet.Add("Item"))
                            throw new System.Exception("Item already exists");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveWhenFalseHasElseStatement_OffersFixer_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if (![|MySet.Contains("Item")|])
                            throw new System.Exception("Item doesn't exist");
                        else
                            MySet.Remove("Item");
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if (!MySet.Remove("Item"))
                            throw new System.Exception("Item doesn't exist");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task AddHasElseBlock_OffersFixer_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if (![|MySet.Contains("Item")|])
                        {
                            MySet.Add("Item");
                        }
                        else
                        {
                            throw new System.Exception("Item already exists");
                        }
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if (!MySet.Add("Item"))
                        {
                            throw new System.Exception("Item already exists");
                        }
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveHasElseBlock_OffersFixer_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if ([|MySet.Contains("Item")|])
                        {
                            MySet.Remove("Item");
                        }
                        else
                        {
                            throw new System.Exception("Item doesn't exist");
                        }
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if (!MySet.Remove("Item"))
                        {
                            throw new System.Exception("Item doesn't exist");
                        }
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task AddWhenFalseHasElseBlock_OffersFixer_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if ([|MySet.Contains("Item")|])
                        {
                            throw new System.Exception("Item already exists");
                        }
                        else
                        {
                            MySet.Add("Item");
                        }
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if (!MySet.Add("Item"))
                        {
                            throw new System.Exception("Item already exists");
                        }
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveWhenFalseHasElseBlock_OffersFixer_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if (![|MySet.Contains("Item")|])
                        {
                            throw new System.Exception("Item doesn't exist");
                        }
                        else
                        {
                            MySet.Remove("Item");
                        }
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if (!MySet.Remove("Item"))
                        {
                            throw new System.Exception("Item doesn't exist");
                        }
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task AddWithAdditionalStatements_ReportsDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if (![|MySet.Contains("Item")|])
                        {
                            MySet.Add("Item");
                            System.Console.WriteLine();
                        }
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveWithAdditionalStatements_ReportsDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if ([|MySet.Contains("Item")|])
                        {
                            MySet.Remove("Item");
                            System.Console.WriteLine();
                        }
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task AddWhenFalseWithAdditionalStatements_ReportsDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if ([|MySet.Contains("Item")|])
                        {
                            throw new System.Exception("Item already exists");
                        }
                        else
                        {
                            MySet.Add("Item");
                            System.Console.WriteLine();
                        }
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveWhenFalseWithAdditionalStatements_ReportsDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if (![|MySet.Contains("Item")|])
                        {
                            throw new System.Exception("Item doesn't exist");
                        }
                        else
                        {
                            MySet.Remove("Item");
                            System.Console.WriteLine();
                        }
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task AddWithVariableAssignment_ReportsDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if (![|MySet.Contains("Item")|])
                        {
                            bool result = MySet.Add("Item");
                        }
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveWithVariableAssignment_ReportsDiagnostic_CS()
        {
            string source = """
            using System.Collections.Generic;

            class C
            {
                private readonly HashSet<string> MySet = new HashSet<string>();

                void M()
                {
                    if ([|MySet.Contains("Item")|])
                    {
                        bool result = MySet.Remove("Item");
                    }
                }
            }
            """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task AddWhenFalseWithVariableAssignment_ReportsDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if ([|MySet.Contains("Item")|])
                        {
                            throw new System.Exception("Item already exists");
                        }
                        else
                        {
                            bool result = MySet.Add("Item");
                        }
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveWhenFalseWithVariableAssignment_ReportsDiagnostic_CS()
        {
            string source = """
            using System.Collections.Generic;

            class C
            {
                private readonly HashSet<string> MySet = new HashSet<string>();

                void M()
                {
                    if (![|MySet.Contains("Item")|])
                    {
                        throw new System.Exception("Item doesn't exist");
                    }
                    else
                    {
                        bool result = MySet.Remove("Item");
                    }
                }
            }
            """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task AddWithNonNegatedContains_NoDiagnostics_CS()
        {
            string source = """
            using System.Collections.Generic;

            class C
            {
                private readonly HashSet<string> MySet = new HashSet<string>();

                void M()
                {
                    if (MySet.Contains("Item"))
                        MySet.Add("Item");
                }
            }
            """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task RemoveWithNegatedContains_NoDiagnostics_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if (!MySet.Contains("Item"))
                            MySet.Remove("Item");
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task AddWhenFalseWithNegatedContains_NoDiagnostics_CS()
        {
            string source = """
            using System.Collections.Generic;

            class C
            {
                private readonly HashSet<string> MySet = new HashSet<string>();

                void M()
                {
                    if (!MySet.Contains("Item"))
                        throw new System.Exception("Item doesn't exist");
                    else
                        MySet.Add("Item");
                }
            }
            """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task RemoveWhenFalseWithNonNegatedContains_NoDiagnostics_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if (MySet.Contains("Item"))
                            throw new System.Exception("Item already exists");
                        else
                            MySet.Remove("Item");
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task AdditionalCondition_NoDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if (MySet.Contains("Item") && MySet.Count > 2)
                            MySet.Remove("Item");
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task ConditionInVariable_NoDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        var result = MySet.Contains("Item");
                        if (result)
                            MySet.Remove("Item");
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task RemoveInSeparateLine_NoDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        if (MySet.Contains("Item"))
                            _ = MySet.Count;
                        MySet.Remove("Item");
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task NotSetRemove_NoDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();
                    private bool Remove(string item) => false;

                    void M()
                    {
                        if (MySet.Contains("Item"))
                            Remove("Item");
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task NestedConditional_NoDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();
                    private readonly HashSet<string> OtherSet = new HashSet<string>();

                    void M()
                    {
                        if (MySet.Contains("Item"))
                        {
                            if (OtherSet.Contains("Item"))
                            {
                                MySet.Remove("Item");
                            }
                        }
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task AddInTernaryWhenTrue_ReportsDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        bool added = ![|MySet.Contains("Item")|] ? MySet.Add("Item") : false;
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task AddInTernaryWhenFalse_ReportsDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        bool added = [|MySet.Contains("Item")|] ? false : MySet.Add("Item");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveInTernaryWhenTrue_ReportsDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        bool removed = [|MySet.Contains("Item")|] ? MySet.Remove("Item") : false;
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveInTernaryWhenFalse_ReportsDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        bool removed = ![|MySet.Contains("Item")|] ? false : MySet.Remove("Item");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task AddInTernaryWhenFalseNested_ReportsDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        bool nestedAdded = [|MySet.Contains("Item")|]
                            ? false
                            : MySet.Add("Item") ? true : false;
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveInTernaryWhenTrueNested_ReportsDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        bool nestedRemoved = [|MySet.Contains("Item")|]
                            ? MySet.Remove("Item") ? true : false
                            : false;
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task AddInTernaryWhenTrueWithNonNegatedContains_NoDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        bool added = MySet.Contains("Item") ? MySet.Add("Item") : false;
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task AddInTernaryWhenFalseWithNegatedContains_NoDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        bool added = !MySet.Contains("Item") ? false : MySet.Add("Item");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveInTernaryWhenTrueWithNegatedContains_NoDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        bool removed = !MySet.Contains("Item") ? MySet.Remove("Item") : false;
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveInTernaryWhenFalseWithNonNegatedContains_NoDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        bool removed = MySet.Contains("Item") ? false : MySet.Remove("Item");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task TriviaIsPreserved_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        // reticulates the splines
                        if ([|MySet.Contains("Item")|])
                        {
                            MySet.Remove("Item");
                        }
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        // reticulates the splines
                        MySet.Remove("Item");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task AddIsTheOnlyStatement_OffersFixer_VB()
        {
            string source = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If Not [|MySet.Contains("Item")|] Then MySet.Add("Item")
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        MySet.Add("Item")
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveIsTheOnlyStatement_OffersFixer_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If ([|MySet.Contains("Item")|]) Then MySet.Remove("Item")
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        MySet.Remove("Item")
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task AddIsTheOnlyStatementInBlock_OffersFixer_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If Not [|MySet.Contains("Item")|] Then
                            MySet.Add("Item")
                        End If
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        MySet.Add("Item")
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveIsTheOnlyStatementInBlock_OffersFixer_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If ([|MySet.Contains("Item")|]) Then
                            MySet.Remove("Item")
                        End If
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        MySet.Remove("Item")
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task AddHasElseStatement_OffersFixer_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If Not [|MySet.Contains("Item")|] Then MySet.Add("Item") Else Throw new System.Exception("Item already exists")
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If Not MySet.Add("Item") Then Throw new System.Exception("Item already exists")
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveHasElseStatement_OffersFixer_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If [|MySet.Contains("Item")|] Then MySet.Remove("Item") Else Throw new System.Exception("Item doesn't exist")
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If Not MySet.Remove("Item") Then Throw new System.Exception("Item doesn't exist")
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task AddWhenFalseHasElseStatement_OffersFixer_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If [|MySet.Contains("Item")|] Then Throw new System.Exception("Item already exists") Else MySet.Add("Item")
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If Not MySet.Add("Item") Then Throw new System.Exception("Item already exists")
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveWhenFalseHasElseStatement_OffersFixer_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If Not [|MySet.Contains("Item")|] Then Throw new System.Exception("Item doesn't exist") Else MySet.Remove("Item")
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If Not MySet.Remove("Item") Then Throw new System.Exception("Item doesn't exist")
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task AddHasElseBlock_OffersFixer_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If Not [|MySet.Contains("Item")|] Then
                            MySet.Add("Item")
                        Else
                            Throw new System.Exception("Item already exists")
                        End If
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If Not MySet.Add("Item") Then
                            Throw new System.Exception("Item already exists")
                        End If
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveHasElseBlock_OffersFixer_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If [|MySet.Contains("Item")|] Then
                            MySet.Remove("Item")
                        Else
                            Throw new System.Exception("Item doesn't exist")
                        End If
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If Not MySet.Remove("Item") Then
                            Throw new System.Exception("Item doesn't exist")
                        End If
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task AddWhenFalseHasElseBlock_OffersFixer_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If [|MySet.Contains("Item")|] Then
                            Throw new System.Exception("Item already exists")
                        Else
                            MySet.Add("Item")
                        End If
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If Not MySet.Add("Item") Then
                            Throw new System.Exception("Item already exists")
                        End If
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveWhenFalseHasElseBlock_OffersFixer_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If Not [|MySet.Contains("Item")|] Then
                            Throw new System.Exception("Item doesn't exist")
                        Else
                            MySet.Remove("Item")
                        End If
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If Not MySet.Remove("Item") Then
                            Throw new System.Exception("Item doesn't exist")
                        End If
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task AddWithNonNegatedContains_NoDiagnostics_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If MySet.Contains("Item") Then MySet.Add("Item")
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task RemoveWithNegatedContains_NoDiagnostics_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If Not MySet.Contains("Item") Then MySet.Remove("Item")
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task AddWhenFalseWithNegatedContains_NoDiagnostics_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If Not MySet.Contains("Item") Then Throw new System.Exception("Item doesn't exist") Else MySet.Add("Item")
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task RemoveWhenFalseWithNonNegatedContains_NoDiagnostics_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If MySet.Contains("Item") Then Throw new System.Exception("Item already exists") Else MySet.Remove("Item")
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task AddWithVariableAssignment_ReportsDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If Not [|MySet.Contains("Item")|] Then
                            Dim result = MySet.Add("Item")
                        End If
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveWithVariableAssignment_ReportsDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If [|MySet.Contains("Item")|] Then
                            Dim result = MySet.Remove("Item")
                        End If
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task AddWhenFalseWithVariableAssignment_ReportsDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If [|MySet.Contains("Item")|] Then
                            Throw new System.Exception("Item already exists")
                        Else
                            Dim result = MySet.Add("Item")
                        End If
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveWhenFalseWithVariableAssignment_ReportsDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If Not [|MySet.Contains("Item")|] Then
                            Throw new System.Exception("Item doesn't exist")
                        Else
                            Dim result = MySet.Remove("Item")
                        End If
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task AddWithAdditionalStatements_ReportsDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If Not [|MySet.Contains("Item")|] Then
                            MySet.Add("Item")
                            System.Console.WriteLine()
                        End If
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveWithAdditionalStatements_ReportsDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If [|MySet.Contains("Item")|] Then
                            MySet.Remove("Item")
                            System.Console.WriteLine()
                        End If
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task AddWhenFalseWithAdditionalStatements_ReportsDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If [|MySet.Contains("Item")|] Then
                            Throw new System.Exception("Item already exists")
                        Else
                            MySet.Add("Item")
                            System.Console.WriteLine()
                        End If
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveWhenFalseWithAdditionalStatements_ReportsDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        If Not [|MySet.Contains("Item")|] Then
                            Throw new System.Exception("Item doesn't exist")
                        Else
                            MySet.Remove("Item")
                            System.Console.WriteLine()
                        End If
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task AddInTernaryWhenTrue_ReportsDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        Dim added = If(Not [|MySet.Contains("Item")|], MySet.Add("Item"), false)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task AddInTernaryWhenFalse_ReportsDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        Dim added = If([|MySet.Contains("Item")|], false, MySet.Add("Item"))
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveInTernaryWhenTrue_ReportsDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        Dim removed = If([|MySet.Contains("Item")|], MySet.Remove("Item"), false)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveInTernaryWhenFalse_ReportsDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        Dim added = If(Not [|MySet.Contains("Item")|], false, MySet.Remove("Item"))
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task AddInTernaryWhenFalseNested_ReportsDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        Dim added = If([|MySet.Contains("Item")|], false, If(MySet.Add("Item"), true, false))
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveInTernaryWhenTrueNested_ReportsDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        Dim added = If([|MySet.Contains("Item")|], If(MySet.Remove("Item"), true, false), false)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task AddInTernaryWhenTrueWithNonNegatedContains_NoDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        Dim added = If(MySet.Contains("Item"), MySet.Add("Item"), false)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task AddInTernaryWhenFalseWithNegatedContains_NoDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        Dim added = If(Not MySet.Contains("Item"), false, MySet.Add("Item"))
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveInTernaryWhenTrueWithNegatedContains_NoDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        Dim added = If(Not MySet.Contains("Item"), MySet.Remove("Item"), false)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveInTernaryWhenFalseWithNonNegatedContains_ReportsDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        Dim added = If(MySet.Contains("Item"), false, MySet.Remove("Item"))
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task TriviaIsPreserved_VB()
        {
            string source = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        ' reticulates the splines
                        If ([|MySet.Contains("Item")|]) Then
                            MySet.Remove("Item")
                        End If
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.Collections.Generic
                
                Public Class C
                    Private ReadOnly MySet As New HashSet(Of String)()

                    Public Sub M()
                        ' reticulates the splines
                        MySet.Remove("Item")
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        [WorkItem(6377, "https://github.com/dotnet/roslyn-analyzers/issues/6377")]
        public async Task ContainsAndRemoveCalledOnDifferentInstances_NoDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> SetField1 = new HashSet<string>();
                    private readonly HashSet<string> SetField2 = new HashSet<string>();

                    public HashSet<string> SetProperty1 { get; } = new HashSet<string>();

                    void M()
                    {
                        if (SetField2.Contains("Item"))
                            SetField1.Remove("Item");

                        if (!SetField1.Contains("Item"))
                        {
                            SetField2.Remove("Item");
                        }

                        if (SetProperty1.Contains("Item"))
                            SetField1.Remove("Item");

                        if (!SetField1.Contains("Item"))
                        {
                            SetProperty1.Remove("Item");
                        }

                        var mySetLocal4 = new HashSet<string>();
                        if (mySetLocal4.Contains("Item"))
                            SetField1.Remove("Item");

                        if (!SetField1.Contains("Item"))
                        {
                            mySetLocal4.Remove("Item");
                        }
                    }

                    private void RemoveItem(HashSet<string> setParam)
                    {
                        if (setParam.Contains("Item"))
                            SetField1.Remove("Item");

                        if (!SetField1.Contains("Item"))
                        {
                            setParam.Remove("Item");
                        }
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task ContainsAndAddCalledWithDifferentArguments_NoDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();
                    private const string OtherItemField = "Other Item";

                    public string OtherItemProperty { get; } = "Other Item";

                    void M(string otherItemParameter)
                    {
                        if (!MySet.Contains("Item"))
                            MySet.Add("Other Item");

                        if (!MySet.Contains("Item"))
                            MySet.Add(otherItemParameter);

                        if (!MySet.Contains("Item"))
                            MySet.Add(OtherItemField);

                        if (!MySet.Contains("Item"))
                            MySet.Add(OtherItemProperty);

                        string otherItemLocal = "Other Item";
                        if (!MySet.Contains("Item"))
                            MySet.Add(otherItemLocal);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task ContainsAndAddCalledWithSameArgumentsFields_OffersFixer_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();
                    private const string FieldItem = "Item";

                    void M()
                    {
                        if (![|MySet.Contains(FieldItem)|])
                        {
                            MySet.Add(FieldItem);
                        }
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();
                    private const string FieldItem = "Item";

                    void M()
                    {
                        MySet.Add(FieldItem);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task ContainsAndAddCalledWithSameArgumentsLocals_OffersFixer_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        const string LocalItem = "Item";

                        if (![|MySet.Contains(LocalItem)|])
                        {
                            MySet.Add(LocalItem);
                        }
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M()
                    {
                        const string LocalItem = "Item";

                        MySet.Add(LocalItem);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task ContainsAndAddCalledWithSameArgumentsParameters_OffersFixer_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M(string parameterItem)
                    {
                        if (![|MySet.Contains(parameterItem)|])
                        {
                            MySet.Add(parameterItem);
                        }
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly HashSet<string> MySet = new HashSet<string>();

                    void M(string parameterItem)
                    {
                        MySet.Add(parameterItem);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("SortedSet<string>", "Add")]
        [InlineData("SortedSet<string>", "Remove")]
        [InlineData("HashSet<string>", "Add")]
        [InlineData("HashSet<string>", "Remove")]
        [InlineData("ImmutableHashSet<string>.Builder", "Add")]
        [InlineData("ImmutableHashSet<string>.Builder", "Remove")]
        [InlineData("ImmutableSortedSet<string>.Builder", "Add")]
        [InlineData("ImmutableSortedSet<string>.Builder", "Remove")]
        public async Task SupportsSetsWithAddOrRemoveReturningBool_OffersFixer_CS(string setType, string method)
        {
            string source = $$"""
                using System.Collections.Generic;
                using System.Collections.Immutable;

                class C
                {
                    private readonly {{setType}} MySet = {{SetCreationExpression(setType)}}

                    void M()
                    {
                        if ({{(method == "Add" ? "!" : string.Empty)}}[|MySet.Contains("Item")|])
                            MySet.{{method}}("Item");
                    }
                }
                """;

            string fixedSource = $$"""
                using System.Collections.Generic;
                using System.Collections.Immutable;

                class C
                {
                    private readonly {{setType}} MySet = {{SetCreationExpression(setType)}}

                    void M()
                    {
                        MySet.{{method}}("Item");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("ISet<string>", "SortedSet<string>", "Add")]
        [InlineData("ISet<string>", "SortedSet<string>", "Remove")]
        [InlineData("ISet<string>", "HashSet<string>", "Add")]
        [InlineData("ISet<string>", "HashSet<string>", "Remove")]
        [InlineData("ISet<string>", "ImmutableHashSet<string>.Builder", "Add")]
        [InlineData("ISet<string>", "ImmutableHashSet<string>.Builder", "Remove")]
        [InlineData("ISet<string>", "ImmutableSortedSet<string>.Builder", "Add")]
        [InlineData("ISet<string>", "ImmutableSortedSet<string>.Builder", "Remove")]
        public async Task SupportsSetsWithAddOrRemoveReturningBoolWithInterfaceType_OffersFixer_CS(string interfaceType, string concreteType, string method)
        {
            string source = $$"""
                using System.Collections.Generic;
                using System.Collections.Immutable;

                class C
                {
                    private readonly {{interfaceType}} MySet = {{SetCreationExpression(concreteType)}}

                    void M()
                    {
                        if ({{(method == "Add" ? "!" : string.Empty)}}[|MySet.Contains("Item")|])
                            MySet.{{method}}("Item");
                    }
                }
                """;

            string fixedSource = $$"""
                using System.Collections.Generic;
                using System.Collections.Immutable;

                class C
                {
                    private readonly {{interfaceType}} MySet = {{SetCreationExpression(concreteType)}}

                    void M()
                    {
                        MySet.{{method}}("Item");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("ImmutableHashSet<string>", "Add")]
        [InlineData("ImmutableHashSet<string>", "Remove")]
        [InlineData("ImmutableSortedSet<string>", "Add")]
        [InlineData("ImmutableSortedSet<string>", "Remove")]
        public async Task SupportsSetWithAddOrRemoveReturningGenericType_ReportsDiagnostic_CS(string setType, string method)
        {
            string source = $$"""
                using System.Collections.Generic;
                using System.Collections.Immutable;

                class C
                {
                    private {{setType}} MySet = {{setType[..setType.IndexOf('<', StringComparison.Ordinal)]}}.Create<string>();

                    void M()
                    {
                        if ({{(method == "Add" ? "!" : string.Empty)}}[|MySet.Contains("Item")|])
                           MySet = MySet.{{method}}("Item");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Theory]
        [InlineData("IImmutableSet<string>", "ImmutableHashSet<string>", "Add")]
        [InlineData("IImmutableSet<string>", "ImmutableHashSet<string>", "Remove")]
        [InlineData("IImmutableSet<string>", "ImmutableSortedSet<string>", "Add")]
        [InlineData("IImmutableSet<string>", "ImmutableSortedSet<string>", "Remove")]
        public async Task SupportsSetWithAddOrRemoveReturningGenericTypeWithInterfaceType_ReportsDiagnostic_CS(string interfaceType, string concreteType, string method)
        {
            string source = $$"""
                using System.Collections.Generic;
                using System.Collections.Immutable;

                class C
                {
                    private {{interfaceType}} MySet = {{concreteType[..concreteType.IndexOf('<', StringComparison.Ordinal)]}}.Create<string>();

                    void M()
                    {
                        if ({{(method == "Add" ? "!" : string.Empty)}}[|MySet.Contains("Item")|])
                           MySet = MySet.{{method}}("Item");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }
        #endregion

        #region Helpers
        private string SetCreationExpression(string setType)
        {
            return setType.Contains("Builder", StringComparison.Ordinal)
                ? $"{setType[..setType.IndexOf('<', StringComparison.Ordinal)]}.CreateBuilder<string>();"
                : $"new {setType}();";
        }
        #endregion
    }
}
