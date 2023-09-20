// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Test.Utilities;
using Xunit;

using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.DoNotGuardCallAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Performance.CSharpDoNotGuardCallFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.DoNotGuardCallAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Performance.BasicDoNotGuardCallFixer>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public class DoNotGuardDictionaryRemoveByContainsKeyKeyTests
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
        public async Task RemoveIsTheOnlyStatement_OffersFixer_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if ({|CA1853:MyDictionary.ContainsKey("Item")|})
                            MyDictionary.Remove("Item");
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        MyDictionary.Remove("Item");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveWithOutIsTheOnlyStatement_OffersFixer_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if ({|CA1853:MyDictionary.ContainsKey("Item")|})
                            MyDictionary.Remove("Item", out var item);
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        MyDictionary.Remove("Item", out var item);
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
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if ({|CA1853:MyDictionary.ContainsKey("Item")|})
                        {
                            MyDictionary.Remove("Item");
                        }
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        MyDictionary.Remove("Item");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveWithOutIsTheOnlyStatementInBlock_OffersFixer_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if ({|CA1853:MyDictionary.ContainsKey("Item")|})
                        {
                            MyDictionary.Remove("Item", out var item);
                        }
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        MyDictionary.Remove("Item", out var item);
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
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if ({|CA1853:MyDictionary.ContainsKey("Item")|})
                            MyDictionary.Remove("Item");
                        else
                            throw new System.Exception("Item doesn't exist");
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if (!MyDictionary.Remove("Item"))
                            throw new System.Exception("Item doesn't exist");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveWithOutHasElseStatement_OffersFixer_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if ({|CA1853:MyDictionary.ContainsKey("Item")|})
                            MyDictionary.Remove("Item", out var item);
                        else
                            throw new System.Exception("Item doesn't exist");
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if (!MyDictionary.Remove("Item", out var item))
                            throw new System.Exception("Item doesn't exist");
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
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if (!{|CA1853:MyDictionary.ContainsKey("Item")|})
                            throw new System.Exception("Item doesn't exist");
                        else
                            MyDictionary.Remove("Item");
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if (!MyDictionary.Remove("Item"))
                            throw new System.Exception("Item doesn't exist");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveWithOutWhenFalseHasElseStatement_OffersFixer_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if (!{|CA1853:MyDictionary.ContainsKey("Item")|})
                            throw new System.Exception("Item doesn't exist");
                        else
                            MyDictionary.Remove("Item", out var item);
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if (!MyDictionary.Remove("Item", out var item))
                            throw new System.Exception("Item doesn't exist");
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
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if ({|CA1853:MyDictionary.ContainsKey("Item")|})
                        {
                            MyDictionary.Remove("Item");
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
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if (!MyDictionary.Remove("Item"))
                        {
                            throw new System.Exception("Item doesn't exist");
                        }
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveWithOutHasElseBlock_OffersFixer_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if ({|CA1853:MyDictionary.ContainsKey("Item")|})
                        {
                            MyDictionary.Remove("Item", out var item);
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
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if (!MyDictionary.Remove("Item", out var item))
                        {
                            throw new System.Exception("Item doesn't exist");
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
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if (!{|CA1853:MyDictionary.ContainsKey("Item")|})
                        {
                            throw new System.Exception("Item doesn't exist");
                        }
                        else
                        {
                            MyDictionary.Remove("Item");
                        }
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if (!MyDictionary.Remove("Item"))
                        {
                            throw new System.Exception("Item doesn't exist");
                        }
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveWithOutWhenFalseHasElseBlock_OffersFixer_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if (!{|CA1853:MyDictionary.ContainsKey("Item")|})
                        {
                            throw new System.Exception("Item doesn't exist");
                        }
                        else
                        {
                            MyDictionary.Remove("Item", out var item);
                        }
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if (!MyDictionary.Remove("Item", out var item))
                        {
                            throw new System.Exception("Item doesn't exist");
                        }
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveWithAdditionalStatements_ReportsDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if ({|CA1853:MyDictionary.ContainsKey("Item")|})
                        {
                            MyDictionary.Remove("Item");
                            System.Console.WriteLine();
                        }
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveWithOutWithAdditionalStatements_ReportsDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if ({|CA1853:MyDictionary.ContainsKey("Item")|})
                        {
                            MyDictionary.Remove("Item", out var item);
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
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if (!{|CA1853:MyDictionary.ContainsKey("Item")|})
                        {
                            throw new System.Exception("Item doesn't exist");
                        }
                        else
                        {
                            MyDictionary.Remove("Item");
                            System.Console.WriteLine();
                        }
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveWithOutWhenFalseWithAdditionalStatements_ReportsDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if (!{|CA1853:MyDictionary.ContainsKey("Item")|})
                        {
                            throw new System.Exception("Item doesn't exist");
                        }
                        else
                        {
                            MyDictionary.Remove("Item", out var item);
                            System.Console.WriteLine();
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
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if ({|CA1853:MyDictionary.ContainsKey("Item")|})
                        {
                            bool result = MyDictionary.Remove("Item");
                        }
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveWithOutWithVariableAssignment_ReportsDiagnostic_CS()
        {
            string source = """
            using System.Collections.Generic;

            class C
            {
                private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                void M()
                {
                    if ({|CA1853:MyDictionary.ContainsKey("Item")|})
                    {
                        bool result = MyDictionary.Remove("Item", out var item);
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
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if (!{|CA1853:MyDictionary.ContainsKey("Item")|})
                        {
                            throw new System.Exception("Item doesn't exist");
                        }
                        else
                        {
                            bool result = MyDictionary.Remove("Item");
                        }
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveWithOutWhenFalseWithVariableAssignment_ReportsDiagnostic_CS()
        {
            string source = """
            using System.Collections.Generic;

            class C
            {
                private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                void M()
                {
                    if (!{|CA1853:MyDictionary.ContainsKey("Item")|})
                    {
                        throw new System.Exception("Item doesn't exist");
                    }
                    else
                    {
                        bool result = MyDictionary.Remove("Item", out var item);
                    }
                }
            }
            """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveWithNegatedContainsKey_NoDiagnostics_CS()
        {
            string source = """
            using System.Collections.Generic;

            class C
            {
                private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                void M()
                {
                    if (!MyDictionary.ContainsKey("Item"))
                        MyDictionary.Remove("Item");
                }
            }
            """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task RemoveWithOutWithNegatedContainsKey_NoDiagnostics_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if (!MyDictionary.ContainsKey("Item"))
                            MyDictionary.Remove("Item", out var item);
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task RemoveWhenFalseWithNonNegatedContainsKey_NoDiagnostics_CS()
        {
            string source = """
            using System.Collections.Generic;

            class C
            {
                private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                void M()
                {
                    if (MyDictionary.ContainsKey("Item"))
                        throw new System.Exception("Item already exists");
                    else
                        MyDictionary.Remove("Item");
                }
            }
            """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task RemoveWithOutWhenFalseWithNonNegatedContainsKey_NoDiagnostics_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if (MyDictionary.ContainsKey("Item"))
                            throw new System.Exception("Item already exists");
                        else
                            MyDictionary.Remove("Item", out var item);
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
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if (MyDictionary.ContainsKey("Item") && MyDictionary.Count > 2)
                            MyDictionary.Remove("Item");
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
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        var result = MyDictionary.ContainsKey("Item");
                        if (result)
                            MyDictionary.Remove("Item");
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
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if (MyDictionary.ContainsKey("Item"))
                            _ = MyDictionary.Count;
                        MyDictionary.Remove("Item");
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task NotDictionaryRemove_NoDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();
                    private bool Remove(string item) => false;

                    void M()
                    {
                        if (MyDictionary.ContainsKey("Item"))
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
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();
                    private readonly Dictionary<string, string> OtherDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        if (MyDictionary.ContainsKey("Item"))
                        {
                            if (OtherDictionary.ContainsKey("Item"))
                            {
                                MyDictionary.Remove("Item");
                            }
                        }
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task RemoveInTernaryWhenTrue_ReportsDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        bool removed = {|CA1853:MyDictionary.ContainsKey("Item")|} ? MyDictionary.Remove("Item") : false;
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
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        bool removed = !{|CA1853:MyDictionary.ContainsKey("Item")|} ? false : MyDictionary.Remove("Item");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveWithOutInTernaryWhenTrue_ReportsDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        bool removed = {|CA1853:MyDictionary.ContainsKey("Item")|} ? MyDictionary.Remove("Item", out var item) : false;
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveWithOutInTernaryWhenFalse_ReportsDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        bool removed = !{|CA1853:MyDictionary.ContainsKey("Item")|} ? false : MyDictionary.Remove("Item", out var item);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveInTernaryWhenFalseNested_ReportsDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        bool nestedRemoved = !{|CA1853:MyDictionary.ContainsKey("Item")|}
                            ? false
                            : MyDictionary.Remove("Item") ? true : false;
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
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        bool nestedRemoved = {|CA1853:MyDictionary.ContainsKey("Item")|}
                            ? MyDictionary.Remove("Item") ? true : false
                            : false;
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveInTernaryWhenTrueWithNegatedContainsKey_NoDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        bool removed = !MyDictionary.ContainsKey("Item") ? MyDictionary.Remove("Item") : false;
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveInTernaryWhenFalseWithNonNegatedContainsKey_NoDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        bool removed = MyDictionary.ContainsKey("Item") ? false : MyDictionary.Remove("Item");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveWithOutInTernaryWhenTrueWithNegatedContainsKey_NoDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        bool removed = !MyDictionary.ContainsKey("Item") ? MyDictionary.Remove("Item", out var item) : false;
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveWithOutInTernaryWhenFalseWithNonNegatedContainsKey_NoDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        bool removed = MyDictionary.ContainsKey("Item") ? false : MyDictionary.Remove("Item", out var item);
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
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        // reticulates the splines
                        if ({|CA1853:MyDictionary.ContainsKey("Item")|})
                        {
                            MyDictionary.Remove("Item");
                        }
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        // reticulates the splines
                        MyDictionary.Remove("Item");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveIsTheOnlyStatement_OffersFixer_VB()
        {
            string source = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        If {|CA1853:MyDictionary.ContainsKey("Item")|} Then MyDictionary.Remove("Item")
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        MyDictionary.Remove("Item")
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveWithOutIsTheOnlyStatement_OffersFixer_VB()
        {
            string source = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        Dim item As String
                        If ({|CA1853:MyDictionary.ContainsKey("Item")|}) Then MyDictionary.Remove("Item", item)
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        Dim item As String
                        MyDictionary.Remove("Item", item)
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
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        If {|CA1853:MyDictionary.ContainsKey("Item")|} Then
                            MyDictionary.Remove("Item")
                        End If
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        MyDictionary.Remove("Item")
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveWithOutIsTheOnlyStatementInBlock_OffersFixer_VB()
        {
            string source = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        Dim item As String
                        If ({|CA1853:MyDictionary.ContainsKey("Item")|}) Then
                            MyDictionary.Remove("Item", item)
                        End If
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        Dim item As String
                        MyDictionary.Remove("Item", item)
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
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        If {|CA1853:MyDictionary.ContainsKey("Item")|} Then MyDictionary.Remove("Item") Else Throw new System.Exception("Item doesn't exist")
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        If Not MyDictionary.Remove("Item") Then Throw new System.Exception("Item doesn't exist")
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveWithOutHasElseStatement_OffersFixer_VB()
        {
            string source = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        Dim item As String
                        If {|CA1853:MyDictionary.ContainsKey("Item")|} Then MyDictionary.Remove("Item", item) Else Throw new System.Exception("Item doesn't exist")
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        Dim item As String
                        If Not MyDictionary.Remove("Item", item) Then Throw new System.Exception("Item doesn't exist")
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
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        If Not {|CA1853:MyDictionary.ContainsKey("Item")|} Then Throw new System.Exception("Item doesn't exist") Else MyDictionary.Remove("Item")
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        If Not MyDictionary.Remove("Item") Then Throw new System.Exception("Item doesn't exist")
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveWithOutWhenFalseHasElseStatement_OffersFixer_VB()
        {
            string source = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        Dim item As String
                        If Not {|CA1853:MyDictionary.ContainsKey("Item")|} Then Throw new System.Exception("Item doesn't exist") Else MyDictionary.Remove("Item", item)
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        Dim item As String
                        If Not MyDictionary.Remove("Item", item) Then Throw new System.Exception("Item doesn't exist")
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
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        If {|CA1853:MyDictionary.ContainsKey("Item")|} Then
                            MyDictionary.Remove("Item")
                        Else
                            Throw new System.Exception("Item doesn't exist")
                        End If
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        If Not MyDictionary.Remove("Item") Then
                            Throw new System.Exception("Item doesn't exist")
                        End If
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveWithOutHasElseBlock_OffersFixer_VB()
        {
            string source = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        Dim item As String
                        If {|CA1853:MyDictionary.ContainsKey("Item")|} Then
                            MyDictionary.Remove("Item", item)
                        Else
                            Throw new System.Exception("Item doesn't exist")
                        End If
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        Dim item As String
                        If Not MyDictionary.Remove("Item", item) Then
                            Throw new System.Exception("Item doesn't exist")
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
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        If Not {|CA1853:MyDictionary.ContainsKey("Item")|} Then
                            Throw new System.Exception("Item doesn't exist")
                        Else
                            MyDictionary.Remove("Item")
                        End If
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        If Not MyDictionary.Remove("Item") Then
                            Throw new System.Exception("Item doesn't exist")
                        End If
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveWithOutWhenFalseHasElseBlock_OffersFixer_VB()
        {
            string source = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        Dim item As String
                        If Not {|CA1853:MyDictionary.ContainsKey("Item")|} Then
                            Throw new System.Exception("Item doesn't exist")
                        Else
                            MyDictionary.Remove("Item", item)
                        End If
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        Dim item As String
                        If Not MyDictionary.Remove("Item", item) Then
                            Throw new System.Exception("Item doesn't exist")
                        End If
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task RemoveWithNegatedContainsKey_NoDiagnostics_VB()
        {
            string source = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        If Not MyDictionary.ContainsKey("Item") Then MyDictionary.Remove("Item")
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task RemoveWithOutWithNegatedContainsKey_NoDiagnostics_VB()
        {
            string source = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        If Not MyDictionary.ContainsKey("Item") Then MyDictionary.Remove("Item")
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task RemoveWhenFalseWithNonNegatedContainsKey_NoDiagnostics_VB()
        {
            string source = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        If MyDictionary.ContainsKey("Item") Then Throw new System.Exception("Item already exists") Else MyDictionary.Remove("Item")
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task RemoveWithOutWhenFalseWithNonNegatedContainsKey_NoDiagnostics_VB()
        {
            string source = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        Dim item As String
                        If MyDictionary.ContainsKey("Item") Then Throw new System.Exception("Item already exists") Else MyDictionary.Remove("Item", item)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task RemoveWithVariableAssignment_ReportsDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        If {|CA1853:MyDictionary.ContainsKey("Item")|} Then
                            Dim result = MyDictionary.Remove("Item")
                        End If
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveWithOutWithVariableAssignment_ReportsDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        Dim item As String
                        If {|CA1853:MyDictionary.ContainsKey("Item")|} Then
                            Dim result = MyDictionary.Remove("Item", item)
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
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        If Not {|CA1853:MyDictionary.ContainsKey("Item")|} Then
                            Throw new System.Exception("Item doesn't exist")
                        Else
                            Dim result = MyDictionary.Remove("Item")
                        End If
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveWithOutWhenFalseWithVariableAssignment_ReportsDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        Dim item As String
                        If Not {|CA1853:MyDictionary.ContainsKey("Item")|} Then
                            Throw new System.Exception("Item doesn't exist")
                        Else
                            Dim result = MyDictionary.Remove("Item", item)
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
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        If {|CA1853:MyDictionary.ContainsKey("Item")|} Then
                            MyDictionary.Remove("Item")
                            System.Console.WriteLine()
                        End If
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveWithOutWithAdditionalStatements_ReportsDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        Dim item As String
                        If {|CA1853:MyDictionary.ContainsKey("Item")|} Then
                            MyDictionary.Remove("Item", item)
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
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        If Not {|CA1853:MyDictionary.ContainsKey("Item")|} Then
                            Throw new System.Exception("Item doesn't exist")
                        Else
                            MyDictionary.Remove("Item")
                            System.Console.WriteLine()
                        End If
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveWithOutWhenFalseWithAdditionalStatements_ReportsDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        Dim item As String
                        If Not {|CA1853:MyDictionary.ContainsKey("Item")|} Then
                            Throw new System.Exception("Item doesn't exist")
                        Else
                            MyDictionary.Remove("Item", item)
                            System.Console.WriteLine()
                        End If
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
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        Dim removed = If({|CA1853:MyDictionary.ContainsKey("Item")|}, MyDictionary.Remove("Item"), false)
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
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        Dim removed = If(Not {|CA1853:MyDictionary.ContainsKey("Item")|}, false, MyDictionary.Remove("Item"))
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveWithOutInTernaryWhenTrue_ReportsDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        Dim item As String
                        Dim removed = If({|CA1853:MyDictionary.ContainsKey("Item")|}, MyDictionary.Remove("Item", item), false)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveWithOutInTernaryWhenFalse_ReportsDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        Dim item As String
                        Dim removed = If(Not {|CA1853:MyDictionary.ContainsKey("Item")|}, false, MyDictionary.Remove("Item"))
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveInTernaryWhenFalseNested_ReportsDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        Dim removed = If(Not {|CA1853:MyDictionary.ContainsKey("Item")|}, false, If(MyDictionary.Remove("Item"), true, false))
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
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        Dim removed = If({|CA1853:MyDictionary.ContainsKey("Item")|}, If(MyDictionary.Remove("Item"), true, false), false)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveInTernaryWhenTrueWithNegatedContainsKey_NoDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        Dim removed = If(Not MyDictionary.ContainsKey("Item"), MyDictionary.Remove("Item"), false)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveInTernaryWhenFalseWithNegatedContainsKey_NoDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        Dim removed = If(MyDictionary.ContainsKey("Item"), false, MyDictionary.Remove("Item"))
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveWithOutInTernaryWhenTrueWithNegatedContainsKey_NoDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        Dim removed = If(Not MyDictionary.ContainsKey("Item"), MyDictionary.Remove("Item"), false)
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task RemoveInTernaryWhenFalseWithNonNegatedContainsKey_NoDiagnostic_VB()
        {
            string source = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        Dim removed = If(MyDictionary.ContainsKey("Item"), false, MyDictionary.Remove("Item"))
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
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        ' reticulates the splines
                        If ({|CA1853:MyDictionary.ContainsKey("Item")|}) Then
                            MyDictionary.Remove("Item")
                        End If
                    End Sub
                End Class
                """;

            string fixedSource = """
                Imports System.Collections.Generic

                Public Class C
                    Private ReadOnly MyDictionary As New Dictionary(Of String, String)()

                    Public Sub M()
                        ' reticulates the splines
                        MyDictionary.Remove("Item")
                    End Sub
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        [WorkItem(6377, "https://github.com/dotnet/roslyn-analyzers/issues/6377")]
        public async Task ContainsKeyAndRemoveCalledOnDifferentInstances_NoDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> DictionaryField1 = new Dictionary<string, string>();
                    private readonly Dictionary<string, string> DictionaryField2 = new Dictionary<string, string>();

                    public Dictionary<string, string> DictionaryProperty1 { get; } = new Dictionary<string, string>();

                    void M()
                    {
                        if (DictionaryField2.ContainsKey("Item"))
                            DictionaryField1.Remove("Item");

                        if (!DictionaryField1.ContainsKey("Item"))
                        {
                            DictionaryField2.Remove("Item");
                        }

                        if (DictionaryProperty1.ContainsKey("Item"))
                            DictionaryField1.Remove("Item");

                        if (!DictionaryField1.ContainsKey("Item"))
                        {
                            DictionaryProperty1.Remove("Item");
                        }

                        var MyDictionaryLocal4 = new Dictionary<string, string>();
                        if (MyDictionaryLocal4.ContainsKey("Item"))
                            DictionaryField1.Remove("Item");

                        if (!DictionaryField1.ContainsKey("Item"))
                        {
                            MyDictionaryLocal4.Remove("Item");
                        }
                    }

                    private void RemoveItem(Dictionary<string, string> dictionaryParam)
                    {
                        if (dictionaryParam.ContainsKey("Item"))
                            DictionaryField1.Remove("Item");

                        if (!DictionaryField1.ContainsKey("Item"))
                        {
                            dictionaryParam.Remove("Item");
                        }
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task ContainsKeyAndRemoveCalledWithDifferentArguments_NoDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();
                    private const string OtherItemField = "Other Item";

                    public string OtherItemProperty { get; } = "Other Item";

                    void M(string otherItemParameter)
                    {
                        if (!MyDictionary.ContainsKey("Item"))
                            MyDictionary.Remove("Other Item");

                        if (!MyDictionary.ContainsKey("Item"))
                            MyDictionary.Remove(otherItemParameter);

                        if (!MyDictionary.ContainsKey("Item"))
                            MyDictionary.Remove(OtherItemField);

                        if (!MyDictionary.ContainsKey("Item"))
                            MyDictionary.Remove(OtherItemProperty);

                        string otherItemLocal = "Other Item";
                        if (!MyDictionary.ContainsKey("Item"))
                            MyDictionary.Remove(otherItemLocal);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task ContainsKeyAndRemoveCalledWithSameArgumentsFields_OffersFixer_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();
                    private const string FieldItem = "Item";

                    void M()
                    {
                        if ({|CA1853:MyDictionary.ContainsKey(FieldItem)|})
                        {
                            MyDictionary.Remove(FieldItem);
                        }
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();
                    private const string FieldItem = "Item";

                    void M()
                    {
                        MyDictionary.Remove(FieldItem);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task ContainsKeyAndRemoveCalledWithSameArgumentsLocals_OffersFixer_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        const string LocalItem = "Item";

                        if ({|CA1853:MyDictionary.ContainsKey(LocalItem)|})
                        {
                            MyDictionary.Remove(LocalItem);
                        }
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M()
                    {
                        const string LocalItem = "Item";

                        MyDictionary.Remove(LocalItem);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Fact]
        public async Task ContainsKeyAndRemoveCalledWithSameArgumentsParameters_OffersFixer_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M(string parameterItem)
                    {
                        if ({|CA1853:MyDictionary.ContainsKey(parameterItem)|})
                        {
                            MyDictionary.Remove(parameterItem);
                        }
                    }
                }
                """;

            string fixedSource = """
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, string> MyDictionary = new Dictionary<string, string>();

                    void M(string parameterItem)
                    {
                        MyDictionary.Remove(parameterItem);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("Dictionary<string, string>", 1)]
        [InlineData("Dictionary<string, string>", 2)]
        [InlineData("SortedDictionary<string, string>", 1)]
        [InlineData("SortedDictionary<string, string>", 2)]
        [InlineData("ImmutableDictionary<string, string>.Builder", 1)]
        [InlineData("ImmutableDictionary<string, string>.Builder", 2)]
        [InlineData("ImmutableSortedDictionary<string, string>.Builder", 1)]
        [InlineData("ImmutableSortedDictionary<string, string>.Builder", 2)]
        [InlineData("ImmutableDictionary<string, string>", 2)]
        [InlineData("ImmutableSortedDictionary<string, string>", 2)]
        public async Task SupportsDictionariesWithRemoveReturningBool_OffersFixer_CS(string dictionaryType, int argumentCount)
        {
            string source = $$"""
                using System.Collections.Generic;
                using System.Collections.Immutable;

                class C
                {
                    private readonly {{dictionaryType}} MyDictionary = {{DictionaryCreationExpression(dictionaryType)}}

                    void M()
                    {
                        if ({|CA1853:MyDictionary.ContainsKey("Item")|})
                            MyDictionary.Remove{{GetArguments(argumentCount)}};
                    }
                }
                """;

            string fixedSource = $$"""
                using System.Collections.Generic;
                using System.Collections.Immutable;

                class C
                {
                    private readonly {{dictionaryType}} MyDictionary = {{DictionaryCreationExpression(dictionaryType)}}

                    void M()
                    {
                        MyDictionary.Remove{{GetArguments(argumentCount)}};
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("IDictionary<string, string>", "Dictionary<string, string>", 1)]
        [InlineData("IDictionary<string, string>", "Dictionary<string, string>", 2)]
        [InlineData("IDictionary<string, string>", "SortedDictionary<string, string>", 1)]
        [InlineData("IDictionary<string, string>", "SortedDictionary<string, string>", 2)]
        [InlineData("IDictionary<string, string>", "ImmutableDictionary<string, string>.Builder", 1)]
        [InlineData("IDictionary<string, string>", "ImmutableDictionary<string, string>.Builder", 2)]
        [InlineData("IDictionary<string, string>", "ImmutableSortedDictionary<string, string>.Builder", 1)]
        [InlineData("IDictionary<string, string>", "ImmutableSortedDictionary<string, string>.Builder", 2)]
        [InlineData("IDictionary<string, string>", "ImmutableDictionary<string, string>", 2)]
        [InlineData("IDictionary<string, string>", "ImmutableSortedDictionary<string, string>", 2)]
        public async Task SupportsDictionariesWithRemoveReturningBoolWithInterfaceType_OffersFixer_CS(string interfaceType, string concreteType, int argumentCount)
        {
            string source = $$"""
                using System.Collections.Generic;
                using System.Collections.Immutable;

                class C
                {
                    private readonly {{interfaceType}} MyDictionary = {{DictionaryCreationExpression(concreteType)}}

                    void M()
                    {
                        if ({|CA1853:MyDictionary.ContainsKey("Item")|})
                            MyDictionary.Remove{{GetArguments(argumentCount)}};
                    }
                }
                """;

            string fixedSource = $$"""
                using System.Collections.Generic;
                using System.Collections.Immutable;

                class C
                {
                    private readonly {{interfaceType}} MyDictionary = {{DictionaryCreationExpression(concreteType)}}

                    void M()
                    {
                        MyDictionary.Remove{{GetArguments(argumentCount)}};
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [Theory]
        [InlineData("ImmutableDictionary<string, string>")]
        [InlineData("ImmutableSortedDictionary<string, string>")]
        public async Task SupportsDictionariesWithRemoveReturningGenericType_ReportsDiagnostic_CS(string dictionaryType)
        {
            string source = $$"""
                using System.Collections.Generic;
                using System.Collections.Immutable;

                class C
                {
                    private {{dictionaryType}} MyDictionary = {{dictionaryType[..dictionaryType.LastIndexOf('<')]}}.Create<string, string>();

                    void M()
                    {
                        if ({|CA1853:MyDictionary.ContainsKey("Item")|})
                           MyDictionary = MyDictionary.Remove("Item");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Theory]
        [InlineData("IImmutableDictionary<string, string>", "ImmutableDictionary<string, string>")]
        [InlineData("IImmutableDictionary<string, string>", "ImmutableSortedDictionary<string, string>")]
        public async Task SupportsDictionaryWithRemoveReturningGenericTypeWithInterfaceType_ReportsDiagnostic_CS(string interfaceType, string concreteType)
        {
            string source = $$"""
                using System.Collections.Generic;
                using System.Collections.Immutable;

                class C
                {
                    private {{interfaceType}} MyDictionary = {{concreteType[..concreteType.LastIndexOf('<')]}}.Create<string, string>();

                    void M()
                    {
                        if ({|CA1853:MyDictionary.ContainsKey("Item")|})
                           MyDictionary = MyDictionary.Remove("Item");
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(source, source);
        }
        #endregion

        #region Helpers
        private string DictionaryCreationExpression(string dictionaryType)
        {
            return dictionaryType.Contains("Immutable", StringComparison.Ordinal)
                ? dictionaryType.Contains("Builder", StringComparison.Ordinal)
                    ? $"{dictionaryType[..dictionaryType.LastIndexOf('<')]}.CreateBuilder<string, string>();"
                    : $"{dictionaryType[..dictionaryType.LastIndexOf('<')]}.Create<string, string>();"
                : $"new {dictionaryType}();";
        }

        private string GetArguments(int argumentCount)
        {
            return argumentCount switch
            {
                1 => @"(""Item"")",
                2 => @"(""Item"", out var item)",
                _ => throw new NotImplementedException(),
            };
        }
        #endregion
    }
}
