// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.UseRegexMembers,
    Microsoft.NetCore.Analyzers.Runtime.UseRegexMembersFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.UseRegexMembers,
    Microsoft.NetCore.Analyzers.Runtime.UseRegexMembersFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class UseRegexMembersTests
    {
        [Fact]
        public async Task Regex_MatchToIsMatch_CSharpAsync()
        {
            await VerifyCS.VerifyCodeFixAsync("""
                using System;
                using System.Text.RegularExpressions;

                class C
                {
                    bool HandleMatchSuccess_Instance(Regex r)
                    {
                        Match m = r.Match("input");
                        bool success = m.Success;

                        m = r.Match("input");
                        if (m.Success) { }

                        success = {|CA1874:r.Match("input").Success|};
                        success = r.Match("input", 0, 5).Success; // no corresponding IsMatch overload
                        Use({|CA1874:r.Match("input", 1)/* will be removed */.Success|});
                        Use("test",
                            {|CA1874:r.Match("input", 2).Success|});
                        Use("test",
                            r.Match("input", 1, 3).Success, // no corresponding IsMatch overload
                            0.0);
                        return {|CA1874:r.Match("input").Success|};
                    }

                    bool HandleMatchSuccess_Static()
                    {
                        Match m = Regex.Match("input", "pattern");
                        bool success = m.Success;

                        m = Regex.Match("input", "pattern");
                        if (m.Success) { }

                        success = {|CA1874:Regex.Match("input", "pattern").Success|};
                        success = {|CA1874:Regex.Match("input", "pattern")/*willberemoved*/.Success|};
                        Use({|CA1874:Regex.Match("input", "pattern", RegexOptions.Compiled).Success|});
                        Use("test",
                            {|CA1874:Regex.Match("input", "pattern", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(10)).Success|});
                        Use("test",
                            {|CA1874:Regex.Match("input", "pattern", RegexOptions.Compiled | RegexOptions.IgnoreCase).Success|} /* comment */,
                            0.0);
                        return {|CA1874:Regex.Match("input", "pattern").Success|};
                    }

                    void Use(bool success) {}
                    void Use(string something, bool success) {}
                    void Use(string something, bool success, double somethingElse) { }
                }
                """,
                """
                using System;
                using System.Text.RegularExpressions;

                class C
                {
                    bool HandleMatchSuccess_Instance(Regex r)
                    {
                        Match m = r.Match("input");
                        bool success = m.Success;

                        m = r.Match("input");
                        if (m.Success) { }

                        success = r.IsMatch("input");
                        success = r.Match("input", 0, 5).Success; // no corresponding IsMatch overload
                        Use(r.IsMatch("input", 1));
                        Use("test",
                            r.IsMatch("input", 2));
                        Use("test",
                            r.Match("input", 1, 3).Success, // no corresponding IsMatch overload
                            0.0);
                        return r.IsMatch("input");
                    }

                    bool HandleMatchSuccess_Static()
                    {
                        Match m = Regex.Match("input", "pattern");
                        bool success = m.Success;

                        m = Regex.Match("input", "pattern");
                        if (m.Success) { }

                        success = Regex.IsMatch("input", "pattern");
                        success = Regex.IsMatch("input", "pattern");
                        Use(Regex.IsMatch("input", "pattern", RegexOptions.Compiled));
                        Use("test",
                            Regex.IsMatch("input", "pattern", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(10)));
                        Use("test",
                            Regex.IsMatch("input", "pattern", RegexOptions.Compiled | RegexOptions.IgnoreCase) /* comment */,
                            0.0);
                        return Regex.IsMatch("input", "pattern");
                    }

                    void Use(bool success) {}
                    void Use(string something, bool success) {}
                    void Use(string something, bool success, double somethingElse) { }
                }
                """);
        }

        [Fact]
        public async Task Regex_MatchToIsMatch_VisualBasicAsync()
        {
            await VerifyVB.VerifyCodeFixAsync("""
                Imports System
                Imports System.Text.RegularExpressions

                Class C
                    Function HandleMatchSuccess_Instance(r As Regex) As Boolean
                        Return {|CA1874:r.Match("input", 2).Success|}
                    End Function

                    Function HandleMatchSuccess_Static() As Boolean
                        Return {|CA1874:Regex.Match("input", "pattern").Success|}
                    End Function
                End Class
                """,
                """
                Imports System
                Imports System.Text.RegularExpressions

                Class C
                    Function HandleMatchSuccess_Instance(r As Regex) As Boolean
                        Return r.IsMatch("input", 2)
                    End Function

                    Function HandleMatchSuccess_Static() As Boolean
                        Return Regex.IsMatch("input", "pattern")
                    End Function
                End Class
                """);
        }

        [Fact]
        public async Task Regex_MatchesToCount_CSharpAsync()
        {
            await new VerifyCS.Test()
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
                TestCode = /* lang=c#-test */ """
                using System;
                using System.Text.RegularExpressions;

                class C
                {
                    int HandleMatchesCount_Instance(Regex r)
                    {
                        var m = r.Matches("input");
                        int count = m.Count;

                        m = r.Matches("input");
                        if (m.Count != 0) { }

                        count = {|CA1875:r.Matches("input").Count|};
                        Use(r.Matches("input", 1).Count); // no corresponding Count overload
                        return {|CA1875:r.Matches("input").Count|};
                    }

                    int HandleMatchesCount_Static()
                    {
                        var m = Regex.Matches("input", "pattern");
                        int count = m.Count;

                        m = Regex.Matches("input", "pattern");
                        if (m.Count != 0) { }

                        count = {|CA1875:Regex.Matches("input", "pattern").Count|};
                        Use({|CA1875:Regex.Matches("input", "pattern", RegexOptions.Compiled).Count|});
                        return {|CA1875:Regex.Matches("input", "pattern", RegexOptions.Compiled, TimeSpan.FromMinutes(1)).Count|};
                    }

                    void Use(int count) {}
                }
                """,
                FixedCode = /* lang=c#-test */ """
                using System;
                using System.Text.RegularExpressions;

                class C
                {
                    int HandleMatchesCount_Instance(Regex r)
                    {
                        var m = r.Matches("input");
                        int count = m.Count;

                        m = r.Matches("input");
                        if (m.Count != 0) { }

                        count = r.Count("input");
                        Use(r.Matches("input", 1).Count); // no corresponding Count overload
                        return r.Count("input");
                    }

                    int HandleMatchesCount_Static()
                    {
                        var m = Regex.Matches("input", "pattern");
                        int count = m.Count;

                        m = Regex.Matches("input", "pattern");
                        if (m.Count != 0) { }

                        count = Regex.Count("input", "pattern");
                        Use(Regex.Count("input", "pattern", RegexOptions.Compiled));
                        return Regex.Count("input", "pattern", RegexOptions.Compiled, TimeSpan.FromMinutes(1));
                    }

                    void Use(int count) {}
                }
                """
            }.RunAsync();
        }

        [Fact]
        public async Task NoDiagnostics_NoRegexCount_CSharpAsync()
        {
            await new VerifyCS.Test()
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net48.Default,
                TestCode = /* lang=c#-test */ """
                    using System.Text.RegularExpressions;

                    class C
                    {
                        void M(Regex r)
                        {
                            int count1 = r.Matches("input").Count;
                            int count2 = Regex.Matches("input", "pattern").Count;
                        }
                    }
                    """
            }.RunAsync();
        }
    }
}