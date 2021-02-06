// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;

using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.PreferAsSpanOverSubstring,
    Microsoft.NetCore.Analyzers.Runtime.PreferAsSpanOverSubstringFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.PreferAsSpanOverSubstring,
    Microsoft.NetCore.Analyzers.Runtime.PreferAsSpanOverSubstringFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class PreferAsSpanOverSubstringTests
    {

        #region Helpers
        private static string CSWithBody(string statements)
        {
            const string indent = "        ";
            string indentedStatements = indent + statements.TrimStart().Replace(Environment.NewLine, Environment.NewLine + indent);

            return $@"
public class Program
{{
    private static void Main()
    {{
{indentedStatements}
    }}
}}";
        }

        private static string VBWithBody(string statements)
        {
            const string indent = "        ";
            string indentedStatements = indent + statements.TrimStart().Replace(Environment.NewLine, Environment.NewLine + indent);

            return $@"
Public Class Program

    Private Sub Main()

{indentedStatements}
    End Sub
End Class";
        }

        private static DiagnosticDescriptor Rule => PreferAsSpanOverSubstring.Rule;
        #endregion
    }
}
