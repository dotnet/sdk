// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.DoNotGuardCallAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Performance.CSharpPreferDictionaryRemoveOverTryGetValueAndRemoveFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.DoNotGuardCallAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    [TestClass]
    public class PreferDictionaryRemoveOverTryGetValueAndRemoveTests
    {
        [TestMethod]
        public async Task RemoveFirstInBlock_RemoveIsDropped_CS()
        {
            await VerifyCSAsync("""
                using System;
                using System.Collections.Generic;

                class C
                {
                    void M(Dictionary<string, int> dictionary, string key)
                    {
                        if ({|CA1883:dictionary.TryGetValue(key, out var value)|})
                        {
                            dictionary.Remove(key);
                            Console.WriteLine(value);
                        }
                    }
                }
                """, """
                using System;
                using System.Collections.Generic;

                class C
                {
                    void M(Dictionary<string, int> dictionary, string key)
                    {
                        if (dictionary.Remove(key, out var value))
                        {
                            Console.WriteLine(value);
                        }
                    }
                }
                """);
        }

        [TestMethod]
        public async Task RemoveIsTheOnlyStatementInBlock_BecomesStatement_CS()
        {
            await VerifyCSAsync("""
                using System;
                using System.Collections.Generic;

                class C
                {
                    void M(Dictionary<string, int> dictionary, string key)
                    {
                        if ({|CA1883:dictionary.TryGetValue(key, out var value)|})
                        {
                            dictionary.Remove(key);
                        }

                        Console.WriteLine(value);
                    }
                }
                """, """
                using System;
                using System.Collections.Generic;

                class C
                {
                    void M(Dictionary<string, int> dictionary, string key)
                    {
                        dictionary.Remove(key, out var value);

                        Console.WriteLine(value);
                    }
                }
                """);
        }

        [TestMethod]
        public async Task RemoveIsTheEmbeddedStatement_BecomesStatement_CS()
        {
            await VerifyCSAsync("""
                using System.Collections.Generic;

                class C
                {
                    int M(Dictionary<string, int> dictionary, string key)
                    {
                        if ({|CA1883:dictionary.TryGetValue(key, out var value)|})
                            dictionary.Remove(key);

                        return value;
                    }
                }
                """, """
                using System.Collections.Generic;

                class C
                {
                    int M(Dictionary<string, int> dictionary, string key)
                    {
                        dictionary.Remove(key, out var value);

                        return value;
                    }
                }
                """);
        }

        [TestMethod]
        public async Task RemoveIsTheEmbeddedStatementWithElse_LeavesEmptyBlock_CS()
        {
            await VerifyCSAsync("""
                using System;
                using System.Collections.Generic;

                class C
                {
                    void M(Dictionary<string, int> dictionary, string key)
                    {
                        if ({|CA1883:dictionary.TryGetValue(key, out var value)|})
                            dictionary.Remove(key);
                        else
                            Console.WriteLine("missing");
                    }
                }
                """, """
                using System;
                using System.Collections.Generic;

                class C
                {
                    void M(Dictionary<string, int> dictionary, string key)
                    {
                        if (dictionary.Remove(key, out var value))
                        {
                        }
                        else
                            Console.WriteLine("missing");
                    }
                }
                """);
        }

        [TestMethod]
        public async Task NegatedConditionRemoveFirstInElse_RemoveIsDropped_CS()
        {
            await VerifyCSAsync("""
                using System;
                using System.Collections.Generic;

                class C
                {
                    void M(Dictionary<string, int> dictionary, string key)
                    {
                        if (!{|CA1883:dictionary.TryGetValue(key, out var value)|})
                        {
                            Console.WriteLine("missing");
                        }
                        else
                        {
                            dictionary.Remove(key);
                            Console.WriteLine(value);
                        }
                    }
                }
                """, """
                using System;
                using System.Collections.Generic;

                class C
                {
                    void M(Dictionary<string, int> dictionary, string key)
                    {
                        if (!dictionary.Remove(key, out var value))
                        {
                            Console.WriteLine("missing");
                        }
                        else
                        {
                            Console.WriteLine(value);
                        }
                    }
                }
                """);
        }

        [TestMethod]
        public async Task NegatedConditionRemoveIsTheOnlyStatementInElse_ElseIsDropped_CS()
        {
            await VerifyCSAsync("""
                using System;
                using System.Collections.Generic;

                class C
                {
                    void M(Dictionary<string, int> dictionary, string key)
                    {
                        if (!({|CA1883:dictionary.TryGetValue(key, out var value)|}))
                        {
                            Console.WriteLine("missing");
                        }
                        else
                        {
                            dictionary.Remove(key);
                        }
                    }
                }
                """, """
                using System;
                using System.Collections.Generic;

                class C
                {
                    void M(Dictionary<string, int> dictionary, string key)
                    {
                        if (!(dictionary.Remove(key, out var value)))
                        {
                            Console.WriteLine("missing");
                        }
                    }
                }
                """);
        }

        [TestMethod]
        public async Task FieldLocalAndConstantKey_Fixed_CS()
        {
            await VerifyCSAsync("""
                using System;
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, int> _field = new();

                    void M()
                    {
                        var local = new Dictionary<int, string>();
                        int existing;

                        if ({|CA1883:_field.TryGetValue("key", out existing)|})
                        {
                            _field.Remove("key");
                            Console.WriteLine(existing);
                        }

                        if ({|CA1883:local.TryGetValue(1, out string text)|})
                        {
                            local.Remove(1);
                            Console.WriteLine(text);
                        }
                    }
                }
                """, """
                using System;
                using System.Collections.Generic;

                class C
                {
                    private readonly Dictionary<string, int> _field = new();

                    void M()
                    {
                        var local = new Dictionary<int, string>();
                        int existing;

                        if (_field.Remove("key", out existing))
                        {
                            Console.WriteLine(existing);
                        }

                        if (local.Remove(1, out string text))
                        {
                            Console.WriteLine(text);
                        }
                    }
                }
                """);
        }

        [TestMethod]
        public async Task NamedArguments_Fixed_CS()
        {
            await VerifyCSAsync("""
                using System;
                using System.Collections.Generic;

                class C
                {
                    void M(Dictionary<string, int> dictionary, string key)
                    {
                        if ({|CA1883:dictionary.TryGetValue(value: out var value, key: key)|})
                        {
                            dictionary.Remove(key: key);
                            Console.WriteLine(value);
                        }
                    }
                }
                """, """
                using System;
                using System.Collections.Generic;

                class C
                {
                    void M(Dictionary<string, int> dictionary, string key)
                    {
                        if (dictionary.Remove(value: out var value, key: key))
                        {
                            Console.WriteLine(value);
                        }
                    }
                }
                """);
        }

        [TestMethod]
        public async Task Trivia_IsKept_CS()
        {
            await VerifyCSAsync("""
                using System;
                using System.Collections.Generic;

                class C
                {
                    void M(Dictionary<string, int> dictionary, string key)
                    {
                        // Take the entry out.
                        if ({|CA1883:dictionary.TryGetValue(key, out var value)|}) // found
                        {
                            dictionary.Remove(key);

                            // Use it.
                            Console.WriteLine(value);
                        }
                    }
                }
                """, """
                using System;
                using System.Collections.Generic;

                class C
                {
                    void M(Dictionary<string, int> dictionary, string key)
                    {
                        // Take the entry out.
                        if (dictionary.Remove(key, out var value)) // found
                        {

                            // Use it.
                            Console.WriteLine(value);
                        }
                    }
                }
                """);
        }

        [TestMethod]
        public async Task Nested_AllFixed_CS()
        {
            await VerifyCSAsync("""
                using System;
                using System.Collections.Generic;

                class C
                {
                    void M(Dictionary<string, int> outer, Dictionary<int, string> inner, string key)
                    {
                        if ({|CA1883:outer.TryGetValue(key, out var id)|})
                        {
                            outer.Remove(key);
                            if ({|CA1883:inner.TryGetValue(id, out var name)|})
                            {
                                inner.Remove(id);
                                Console.WriteLine(name);
                            }
                        }
                    }
                }
                """, """
                using System;
                using System.Collections.Generic;

                class C
                {
                    void M(Dictionary<string, int> outer, Dictionary<int, string> inner, string key)
                    {
                        if (outer.Remove(key, out var id))
                        {
                            if (inner.Remove(id, out var name))
                            {
                                Console.WriteLine(name);
                            }
                        }
                    }
                }
                """);
        }

        [TestMethod]
        [DataRow("Console.WriteLine(value); dictionary.Remove(key);", DisplayName = "Call before Remove")]
        [DataRow("bool removed = dictionary.Remove(key);", DisplayName = "Remove result is used")]
        [DataRow("dictionary.Remove(other);", DisplayName = "Different key")]
        [DataRow("_ = dictionary.Remove(key);", DisplayName = "Remove is assigned")]
        public async Task RemoveIsNotImmediateOrNotTheSame_NoDiagnostic_CS(string body)
        {
            string source = $$"""
                using System;
                using System.Collections.Generic;

                class C
                {
                    void M(Dictionary<string, int> dictionary, string key, string other)
                    {
                        if (dictionary.TryGetValue(key, out var value))
                        {
                            {{body}}
                        }
                    }
                }
                """;

            await VerifyCSAsync(source, source);
        }

        [TestMethod]
        public async Task DifferentDictionary_NoDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    void M(Dictionary<string, int> first, Dictionary<string, int> second, string key)
                    {
                        if (first.TryGetValue(key, out var value))
                        {
                            second.Remove(key);
                        }
                    }
                }
                """;

            await VerifyCSAsync(source, source);
        }

        [TestMethod]
        public async Task DictionaryIsAProperty_NoDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    Dictionary<string, int> Dictionary { get; } = new();

                    void M(string key)
                    {
                        if (Dictionary.TryGetValue(key, out var value))
                        {
                            Dictionary.Remove(key);
                        }
                    }
                }
                """;

            await VerifyCSAsync(source, source);
        }

        [TestMethod]
        public async Task InterfaceTypedDictionary_NoDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    void M(IDictionary<string, int> dictionary, string key)
                    {
                        if (dictionary.TryGetValue(key, out var value))
                        {
                            dictionary.Remove(key);
                        }
                    }
                }
                """;

            await VerifyCSAsync(source, source);
        }

        [TestMethod]
        public async Task RemoveRunsWhenKeyIsMissing_NoDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    void M(Dictionary<string, int> dictionary, string key)
                    {
                        if (!dictionary.TryGetValue(key, out var value))
                        {
                            dictionary.Remove(key);
                        }
                    }
                }
                """;

            await VerifyCSAsync(source, source);
        }

        [TestMethod]
        public async Task ValueIsWrittenIntoTheKey_NoDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    void M(Dictionary<string, string> dictionary, string key)
                    {
                        if (dictionary.TryGetValue(key, out key))
                        {
                            dictionary.Remove(key);
                        }
                    }
                }
                """;

            await VerifyCSAsync(source, source);
        }

        [TestMethod]
        public async Task ConditionalExpression_NoDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    bool M(Dictionary<string, int> dictionary, string key)
                    {
                        return dictionary.TryGetValue(key, out var value) ? dictionary.Remove(key) : false;
                    }
                }
                """;

            await VerifyCSAsync(source, source);
        }

        [TestMethod]
        public async Task RemoveWithValueOverloadMissing_NoDiagnostic_CS()
        {
            string source = """
                using System.Collections.Generic;

                class C
                {
                    void M(Dictionary<string, int> dictionary, string key)
                    {
                        if (dictionary.TryGetValue(key, out var value))
                        {
                            dictionary.Remove(key);
                        }
                    }
                }
                """;

            await VerifyCSAsync(source, source, ReferenceAssemblies.NetFramework.Net472.Default);
        }

        [TestMethod]
        public async Task ImmutableCollectionsNotReferenced_StillFixed_CS()
        {
            await VerifyCSAsync("""
                using System.Collections.Generic;

                class C
                {
                    int M(Dictionary<string, int> dictionary, string key)
                    {
                        if ({|CA1883:dictionary.TryGetValue(key, out var value)|})
                        {
                            dictionary.Remove(key);
                            return value;
                        }

                        return 0;
                    }
                }
                """, """
                using System.Collections.Generic;

                class C
                {
                    int M(Dictionary<string, int> dictionary, string key)
                    {
                        if (dictionary.Remove(key, out var value))
                        {
                            return value;
                        }

                        return 0;
                    }
                }
                """, ReferenceAssemblies.NetStandard.NetStandard21);
        }

        [TestMethod]
        public async Task RemoveFirstInBlock_Diagnostic_VB()
        {
            await VerifyVBAsync("""
                Imports System
                Imports System.Collections.Generic

                Class C
                    Sub M(dictionary As Dictionary(Of String, Integer), key As String)
                        Dim value As Integer
                        If {|CA1883:dictionary.TryGetValue(key, value)|} Then
                            dictionary.Remove(key)
                            Console.WriteLine(value)
                        End If
                    End Sub
                End Class
                """);
        }

        [TestMethod]
        public async Task CallBeforeRemove_NoDiagnostic_VB()
        {
            await VerifyVBAsync("""
                Imports System
                Imports System.Collections.Generic

                Class C
                    Sub M(dictionary As Dictionary(Of String, Integer), key As String)
                        Dim value As Integer
                        If dictionary.TryGetValue(key, value) Then
                            Console.WriteLine(value)
                            dictionary.Remove(key)
                        End If
                    End Sub
                End Class
                """);
        }

        private static Task VerifyCSAsync(string source, string fixedSource, ReferenceAssemblies referenceAssemblies = null)
            => new VerifyCS.Test
            {
                TestCode = source,
                FixedCode = fixedSource,
                ReferenceAssemblies = referenceAssemblies ?? ReferenceAssemblies.Net.Net80,
                LanguageVersion = Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp10,
            }.RunAsync();

        private static Task VerifyVBAsync(string source)
            => new VerifyVB.Test
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            }.RunAsync();
    }
}
