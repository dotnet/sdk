// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpTypesThatOwnDisposableFieldsShouldBeDisposableAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.TypesThatOwnDisposableFieldsShouldBeDisposableFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicTypesThatOwnDisposableFieldsShouldBeDisposableAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.TypesThatOwnDisposableFieldsShouldBeDisposableFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class TypesThatOwnDisposableFieldsShouldBeDisposableFixerTests
    {
        [Fact]
        public async Task CA1001CSharpCodeFixNoDisposeAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
using System.IO;

// This class violates the rule.
public class [|NoDisposeClass|]
{
    FileStream newFile;

    public NoDisposeClass()
    {
        newFile = new FileStream("""", FileMode.Append);
    }
}
",
@"
using System;
using System.IO;

// This class violates the rule.
public class NoDisposeClass : IDisposable
{
    FileStream newFile;

    public NoDisposeClass()
    {
        newFile = new FileStream("""", FileMode.Append);
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
");
        }

        [Fact]
        public async Task CA1001BasicCodeFixNoDisposeAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System
Imports System.IO

' This class violates the rule. 
Public Class [|NoDisposeMethod|]

    Dim newFile As FileStream

    Sub New()
        newFile = New FileStream("""", FileMode.Append)
    End Sub

End Class
",
@"
Imports System
Imports System.IO

' This class violates the rule. 
Public Class NoDisposeMethod
    Implements IDisposable

    Dim newFile As FileStream

    Sub New()
        newFile = New FileStream("""", FileMode.Append)
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Throw New NotImplementedException()
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA1001CSharpCodeFixHasDisposeAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
using System.IO;

// This class violates the rule.
public class [|NoDisposeClass|]
{
    FileStream newFile = new FileStream("""", FileMode.Append);

    void Dispose() {
// Some content
}
}
",
@"
using System;
using System.IO;

// This class violates the rule.
public class NoDisposeClass : IDisposable
{
    FileStream newFile = new FileStream("""", FileMode.Append);

    public void Dispose() {
// Some content
}
}
");
        }

        [Fact]
        public async Task CA1001CSharpCodeFixHasWrongDisposeAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
using System.IO;

// This class violates the rule.
public partial class [|NoDisposeClass|]
{
    FileStream newFile = new FileStream("""", FileMode.Append);

    void Dispose(int x) {
// Some content
}
}
",
@"
using System;
using System.IO;

// This class violates the rule.
public partial class NoDisposeClass : IDisposable
{
    FileStream newFile = new FileStream("""", FileMode.Append);

    void Dispose(int x) {
// Some content
}

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
");
        }

        [Fact]
        public async Task CA1001BasicCodeFixHasDisposeAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System
Imports System.IO

' This class violates the rule. 
Public Class [|NoDisposeMethod|]

    Dim newFile As FileStream = New FileStream("""", FileMode.Append)

    Sub Dispose()

    End Sub
End Class
",
@"
Imports System
Imports System.IO

' This class violates the rule. 
Public Class NoDisposeMethod
    Implements IDisposable

    Dim newFile As FileStream = New FileStream("""", FileMode.Append)

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA1001BasicCodeFixHasWrongDisposeAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System
Imports System.IO

' This class violates the rule. 
Public Class [|NoDisposeMethod|]

    Dim newFile As FileStream = New FileStream("""", FileMode.Append)

    Sub Dispose(x As Integer)
    End Sub
End Class
",
@"
Imports System
Imports System.IO

' This class violates the rule. 
Public Class NoDisposeMethod
    Implements IDisposable

    Dim newFile As FileStream = New FileStream("""", FileMode.Append)

    Sub Dispose(x As Integer)
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Throw New NotImplementedException()
    End Sub
End Class
");
        }
    }
}