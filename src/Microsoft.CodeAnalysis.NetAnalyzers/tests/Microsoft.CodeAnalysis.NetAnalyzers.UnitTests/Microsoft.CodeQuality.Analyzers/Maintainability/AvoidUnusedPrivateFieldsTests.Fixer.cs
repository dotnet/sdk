// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.Maintainability.AvoidUnusedPrivateFieldsAnalyzer,
    Microsoft.CodeQuality.Analyzers.Maintainability.AvoidUnusedPrivateFieldsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.Maintainability.AvoidUnusedPrivateFieldsAnalyzer,
    Microsoft.CodeQuality.Analyzers.Maintainability.AvoidUnusedPrivateFieldsFixer>;

namespace Microsoft.CodeQuality.Analyzers.Maintainability.UnitTests
{
    public class AvoidUnusedPrivateFieldsFixerTests
    {
        [Fact]
        public async Task CA1823CSharp()
        {
            await VerifyCS.VerifyCodeFixAsync(
                @"  
class C  
{  
    public int x;
    public int y;
    public int z;
    private int a;
    private int [|b|];
    private int c;
    private int d, [|e|], f;

    public int SomeMethod()
    {
        return x + z + a + c + d + f;
    }
}  
 ",
                @"  
class C  
{  
    public int x;
    public int y;
    public int z;
    private int a;
    private int c;
    private int d, f;

    public int SomeMethod()
    {
        return x + z + a + c + d + f;
    }
}  
 ");
        }

        [Fact]
        public async Task CA1823VisualBasic()
        {
            await VerifyVB.VerifyCodeFixAsync(
                @"
Class C
    Public x As Integer
    Public y As Integer
    Public z As Integer
    Private a As Integer
    Private [|b|] As Integer
    Private c As Integer
    Private d, [|e|], f As Integer

    Public Function SomeMethod() As Integer
        Return x + z + a + c + d + f
    End Function
End Class
 ",
                @"
Class C
    Public x As Integer
    Public y As Integer
    Public z As Integer
    Private a As Integer
    Private c As Integer
    Private d, f As Integer

    Public Function SomeMethod() As Integer
        Return x + z + a + c + d + f
    End Function
End Class
 ");
        }
    }
}