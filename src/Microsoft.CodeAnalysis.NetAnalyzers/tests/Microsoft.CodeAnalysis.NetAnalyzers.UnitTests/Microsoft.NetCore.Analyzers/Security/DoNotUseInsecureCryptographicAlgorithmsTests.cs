// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotUseInsecureCryptographicAlgorithmsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotUseInsecureCryptographicAlgorithmsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class DoNotUseInsecureCryptographicAlgorithmsTests
    {
        #region CA5350

        [Fact]
        public async Task CA5350UseMD5CreateInMethodDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            var md5 = MD5.Create();
        }
    }
}",
                GetCSharpResultAt(10, 23, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "MD5"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography

Module TestClass
    Sub TestSub()
        Dim md5alg As MD5 = MD5.Create()
    End Sub
End Module",
                GetBasicResultAt(6, 29, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestSub", "MD5"));
        }
        //NO VB
        [Fact]
        public async Task CA5350UseMD5CreateInPropertyDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
   class TestClass1
    {
        public MD5 GetMD5 => MD5.Create();
    }
}",
                GetCSharpResultAt(7, 30, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "get_GetMD5", "MD5"));
        }

        [Fact]
        public async Task CA5350UseMD5CreateInGetDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
   class TestClass1
    {                                     
        public HashAlgorithm GetAlg
        {
            get { return MD5.Create(); }
        }
    }
}",
                GetCSharpResultAt(9, 26, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "get_GetAlg", "MD5"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass1
		Public ReadOnly Property GetAlg() As HashAlgorithm
			Get
				Return MD5.Create()
			End Get
		End Property
	End Class
End Namespace",
                GetBasicResultAt(7, 12, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "get_GetAlg", "MD5"));
        }

        [Fact]
        public async Task CA5350UseMD5CreateInFieldDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
    class TestClass1
    {
        public HashAlgorithm Alg = MD5.Create();  
    }
}",
                GetCSharpResultAt(7, 36, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "Alg", "MD5"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass1
		Public Alg As HashAlgorithm = MD5.Create()
	End Class
End Namespace",
                GetBasicResultAt(5, 33, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "Alg", "MD5"));
        }

        [Fact]
        public async Task CA5350UseMD5CreateInLambdaExpression()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
using System.Threading.Tasks;
namespace TestNamespace
{
    class TestClass
    {
        private async Task TestMethod()
        {
            await Task.Run(() => { MD5.Create(); });
        }
    }
}",
                GetCSharpResultAt(10, 36, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "MD5"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Imports System.Threading.Tasks
Namespace TestNamespace
	Class TestClass
		Private Async Function TestMethod() As Task
			Await Task.Run(Function() 
			    MD5.Create()
            End Function)
		End Function
	End Class
End Namespace",
                GetBasicResultAt(8, 8, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "MD5"));
        }

        [Fact]
        public async Task CA5350UseMD5CreateInAnonymousMethodExpression()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
    class TestClass
    {
        delegate void Del();
        Del d = delegate () { MD5.Create(); };
    }
}",
                GetCSharpResultAt(8, 31, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "d", "MD5"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Private Delegate Sub Del()
		Private d As Del = Sub() MD5.Create()
	End Class
End Namespace",
                GetBasicResultAt(6, 28, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "d", "MD5"));
        }

        [Fact]
        public async Task CA5350CreateObjectFromMD5DerivedClass()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
//Test0
@"
using System.Security.Cryptography;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(byte[] inBytes)
        {
            var myMd5 = new MyMD5();
        }
    }
}",
//Test1
@"
using System;
using System.Security.Cryptography;

namespace TestNamespace
{
    class MyMD5 : MD5
    {
        public override void Initialize()
        {
            throw new NotImplementedException();
        }

        protected override void HashCore(byte[] array, int ibStart, int cbSize)
        {
            throw new NotImplementedException();
        }

        protected override byte[] HashFinal()
        {
            throw new NotImplementedException();
        }
    }
}"
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(10, 25, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "MD5"),
                    }
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
@"
Imports System.Security.Cryptography

Namespace TestNamespace
	Class TestClass
		Private Shared Sub TestMethod(inBytes As Byte())
			Dim myMd5 = New MyMD5()
		End Sub
	End Class
End Namespace",

//Test1
@"
Imports System.Security.Cryptography

Namespace TestNamespace
	Class MyMD5
		Inherits MD5
		Public Overrides Sub Initialize()
			Throw New System.NotImplementedException()
		End Sub

		Protected Overrides Sub HashCore(array As Byte(), ibStart As Integer, cbSize As Integer)
			Throw New System.NotImplementedException()
		End Sub

		Protected Overrides Function HashFinal() As Byte()
			Throw New System.NotImplementedException()
		End Function
	End Class
End Namespace"
                    },
                    ExpectedDiagnostics =
                    {
                        GetBasicResultAt(7, 16, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "MD5"),
                    }
                },
            }.RunAsync();
        }

        #endregion

        #region CA5354

        [Fact]
        public async Task CA5354UseSHA1CreateInMethodDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            var sha1 = SHA1.Create();
        }
    }
}",
                GetCSharpResultAt(10, 24, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "SHA1"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography

Module TestClass
    Sub TestSub()
        Dim sha1alg As SHA1 = SHA1.Create()
    End Sub
End Module",
                GetBasicResultAt(6, 31, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestSub", "SHA1"));
        }
        //NO VB
        [Fact]
        public async Task CA5354UseSHA1CreateInPropertyDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
   class TestClass1
    {
        public SHA1 GetSHA1 => SHA1.Create();
    }
}",
                GetCSharpResultAt(7, 32, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "get_GetSHA1", "SHA1"));
        }

        [Fact]
        public async Task CA5354UseSHA1CreateInGetDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
   class TestClass1
    {                                     
        public HashAlgorithm GetAlg
        {
            get { return SHA1.Create(); }
        }
    }
}",
                GetCSharpResultAt(9, 26, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "get_GetAlg", "SHA1"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass1
		Public ReadOnly Property GetAlg() As HashAlgorithm
			Get
				Return SHA1.Create()
			End Get
		End Property
	End Class
End Namespace",
                GetBasicResultAt(7, 12, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "get_GetAlg", "SHA1"));
        }

        [Fact]
        public async Task CA5354UseSHA1CreateInFieldDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
    class TestClass1
    {
        public HashAlgorithm Alg = SHA1.Create();  
    }
}",
                GetCSharpResultAt(7, 36, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "Alg", "SHA1"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass1
		Public Alg As HashAlgorithm = SHA1.Create()
	End Class
End Namespace",
                GetBasicResultAt(5, 33, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "Alg", "SHA1"));
        }

        [Fact]
        public async Task CA5354UseSHA1CreateInLambdaExpression()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
using System.Threading.Tasks;
namespace TestNamespace
{
    class TestClass
    {
        private async Task TestMethod()
        {
            await Task.Run(() => { SHA1.Create(); });
        }
    }
}",
                GetCSharpResultAt(10, 36, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "SHA1"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Imports System.Threading.Tasks
Namespace TestNamespace
	Class TestClass
		Private Async Function TestMethod() As Task
			Await Task.Run(Function() 
			    SHA1.Create()
            End Function)
		End Function
	End Class
End Namespace",
                GetBasicResultAt(8, 8, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "SHA1"));
        }

        [Fact]
        public async Task CA5354UseSHA1CreateInAnonymousMethodExpression()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
    class TestClass
    {
        delegate void Del();
        Del d = delegate () { SHA1.Create(); };
    }
}",
                GetCSharpResultAt(8, 31, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "d", "SHA1"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Private Delegate Sub Del()
		Private d As Del = Sub() SHA1.Create()
	End Class
End Namespace",
                GetBasicResultAt(6, 28, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "d", "SHA1"));
        }

        [Fact]
        public async Task CA5354CreateObjectFromSHA1DerivedClass()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
//Test0
@"
using System.Security.Cryptography;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(byte[] inBytes)
        {
            var mySHA1 = new MySHA1();
        }
    }
}",
//Test1
@"
using System;
using System.Security.Cryptography;

namespace TestNamespace
{
    class MySHA1 : SHA1
    {
        public override void Initialize()
        {
            throw new NotImplementedException();
        }

        protected override void HashCore(byte[] array, int ibStart, int cbSize)
        {
            throw new NotImplementedException();
        }

        protected override byte[] HashFinal()
        {
            throw new NotImplementedException();
        }
    }
}"
},
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(10, 26, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "SHA1"),
                    }
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
//Test0
@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Private Shared Sub TestMethod(inBytes As Byte())
			Dim mySHA1 = New MySHA1()
		End Sub
	End Class
End Namespace",
//Test1
@"
Imports System.Security.Cryptography

Namespace TestNamespace
	Class MySHA1
		Inherits SHA1
		Public Overrides Sub Initialize()
			Throw New System.NotImplementedException()
		End Sub

		Protected Overrides Sub HashCore(array As Byte(), ibStart As Integer, cbSize As Integer)
			Throw New System.NotImplementedException()
		End Sub

		Protected Overrides Function HashFinal() As Byte()
			Throw New System.NotImplementedException()
		End Function
	End Class
End Namespace"
                    },
                    ExpectedDiagnostics =
                    {
                        GetBasicResultAt(6, 17, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "SHA1"),
                    }
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CA5354UseSHA1CryptoServiceProviderInMethodDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            var sha1 = new SHA1CryptoServiceProvider();
        }
    }
}",
                GetCSharpResultAt(10, 24, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "SHA1"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography

Module TestClass
    Sub TestMethod()
        Dim SHA1alg As New SHA1CryptoServiceProvider
    End Sub
End Module",
                GetBasicResultAt(6, 24, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "SHA1"));
        }

        [Fact]
        public async Task CA5354CreateHMACSHA1ObjectInMethodDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            var hmacsha1 = new HMACSHA1();
        }
    }
}",
                GetCSharpResultAt(10, 28, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "HMACSHA1"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography

Module TestClass
    Sub TestSub()
        Dim hmacsha1 As HMACSHA1 = New HMACSHA1()
    End Sub
End Module",
                GetBasicResultAt(6, 36, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestSub", "HMACSHA1"));
        }
        //No VB
        [Fact]
        public async Task CA5354CreateHMACSHA1ObjectInPropertyDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
   class TestClass1
    {
        public HMAC GetHMACSHA1 => new HMACSHA1();
    }
}",
                GetCSharpResultAt(7, 36, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "get_GetHMACSHA1", "HMACSHA1"));
        }

        [Fact]
        public async Task CA5354CreateHMACSHA1ObjectInGetDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
   class TestClass1
    {                                     
        public HMAC GetAlg
        {
            get { return new HMACSHA1(); }
        }
    }
}",
                GetCSharpResultAt(9, 26, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "get_GetAlg", "HMACSHA1"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass1
		Public ReadOnly Property GetAlg() As HMAC
			Get
				Return New HMACSHA1()
			End Get
		End Property
	End Class
End Namespace",
                GetBasicResultAt(7, 12, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "get_GetAlg", "HMACSHA1"));
        }

        [Fact]
        public async Task CA5354CreateHMACSHA1ObjectInFieldDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
    class TestClass1
    {
        public HMAC Alg = new HMACSHA1();  
    }
}",
                GetCSharpResultAt(7, 27, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "Alg", "HMACSHA1"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass1
		Public Alg As HMAC = New HMACSHA1()
	End Class
End Namespace",
                GetBasicResultAt(5, 24, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "Alg", "HMACSHA1"));
        }
        //No VB
        [Fact]
        public async Task CA5354CreateHMACSHA1ObjectInLambdaExpression()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
using System.Threading.Tasks;
namespace TestNamespace
{
    class TestClass
    {
        private async Task TestMethod()
        {
            await Task.Run(() => { new HMACSHA1(); });
        }
    }
}",
                GetCSharpResultAt(10, 36, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "HMACSHA1"));
        }
        //No VB
        [Fact]
        public async Task CA5354CreateHMACSHA1ObjectInAnonymousMethodExpression()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
    class TestClass
    {
        delegate void Del();
        Del d = delegate () { new HMACSHA1(); };
    }
}",
                GetCSharpResultAt(8, 31, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "d", "HMACSHA1"));
        }

        [Fact]
        public async Task CA5354CreateObjectFromHMACSHA1DerivedClass()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
//Test0
@"
using System.Security.Cryptography;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(byte[] inBytes)
        {
            var myHMACSHA1 = new MyHMACSHA1();
        }
    }
}",
//Test1
@"
using System;
using System.Security.Cryptography;

namespace TestNamespace
{ 
    class MyHMACSHA1 : HMACSHA1
    {
        public override void Initialize()
        {
            throw new NotImplementedException();
        }

        protected override void HashCore(byte[] array, int ibStart, int cbSize)
        {
            throw new NotImplementedException();
        }

        protected override byte[] HashFinal()
        {
            throw new NotImplementedException();
        }
    }
}"
},
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(10, 30, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "HMACSHA1"),
                    }
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
//Test0
@"
Imports System.Security.Cryptography

Namespace TestNamespace
	Class TestClass
		Private Shared Sub TestMethod(inBytes As Byte())
			Dim myHMACSHA1 = New MyHMACSHA1()
		End Sub
	End Class
End Namespace",
//Test1
@"
Imports System.Security.Cryptography

Namespace TestNamespace
	Class MyHMACSHA1
		Inherits HMACSHA1
		Public Overrides Sub Initialize()
			Throw New System.NotImplementedException()
		End Sub

		Protected Overrides Sub HashCore(array As Byte(), ibStart As Integer, cbSize As Integer)
			Throw New System.NotImplementedException()
		End Sub

		Protected Overrides Function HashFinal() As Byte()
			Throw New System.NotImplementedException()
		End Function
	End Class
End Namespace
"
},
                    ExpectedDiagnostics =
                    {
                        GetBasicResultAt(7, 21, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "HMACSHA1"),
                    }
                },
            }.RunAsync();
        }
        #endregion

        [Fact]
        public async Task CA5350UseHMACMD5CreateInMethodDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            var md5 = new HMACMD5();
        }
    }
}",
                GetCSharpResultAt(10, 23, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "HMACMD5"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography

Namespace TestNamespace
	Class TestClass
		Private Shared Sub TestMethod()
			Dim md5 = New HMACMD5()
		End Sub
	End Class
End Namespace",
                GetBasicResultAt(7, 14, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "HMACMD5"));
        }

        [Fact]
        public async Task CA5350CreateObjectFromHMACMD5DerivedClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

namespace TestNamespace
{
    class MyHMACMD5 : HMACMD5 {}

    class TestClass
    {
        private static void TestMethod()
        {
            var md5 = new MyHMACMD5();
        }
    }
}",
                GetCSharpResultAt(12, 23, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "HMACMD5"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class MyHMACMD5
		Inherits HMACMD5
	End Class

	Class TestClass
		Private Shared Sub TestMethod()
			Dim md5 = New MyHMACMD5()
		End Sub
	End Class
End Namespace",
                GetBasicResultAt(10, 14, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "HMACMD5"));
        }

        [Fact]
        public async Task CA5350UseHMACMD5CreateInGetDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
   class TestClass1
    {
        public HMACMD5 GetHMACMD5
        {
            get { return new HMACMD5(); }
        }
    }
}",
                GetCSharpResultAt(9, 26, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "get_GetHMACMD5", "HMACMD5"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass1
		Public ReadOnly Property GetHMACMD5() As HMACMD5
			Get
				Return New HMACMD5()
			End Get
		End Property
	End Class
End Namespace",
                GetBasicResultAt(7, 12, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "get_GetHMACMD5", "HMACMD5"));
        }

        [Fact]
        public async Task CA5350UseHMACMD5InFieldDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
    class TestClass
    {
        HMACMD5 privateMd5 = new HMACMD5();
    }
}",
                GetCSharpResultAt(7, 30, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "privateMd5", "HMACMD5"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Private privateMd5 As New HMACMD5()
	End Class
End Namespace",
                GetBasicResultAt(5, 25, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "privateMd5", "HMACMD5"));
        }

        [Fact]
        public async Task CA5350UseHMACMD5InLambdaExpression()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
using System.Threading.Tasks;
namespace TestNamespace
{
    class TestClass
    {
        private async Task TestMethod()
        {
            await Task.Run(() => { new HMACMD5(); });
        }
    }
}",
                GetCSharpResultAt(10, 36, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "HMACMD5"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Imports System.Threading.Tasks

Module TestClass
    Public Async Sub TestMethod()
        Await Task.Run(Function()
                           Return New HMACMD5()
                       End Function)
    End Sub
End Module",
                GetBasicResultAt(8, 35, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "HMACMD5"));
        }

        [Fact]
        public async Task CA5350UseHMACMD5InAnonymousMethodExpression()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
    class TestClass
    {
        delegate void Del();
        Del d = delegate () { new HMACMD5(); };
    }
}",
                GetCSharpResultAt(8, 31, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "d", "HMACMD5"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography

Module TestClass 
    Delegate Function Del() As HashAlgorithm
    Dim d As Del = Function() New HMACMD5()
End Module",
                GetBasicResultAt(6, 31, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "d", "HMACMD5"));
        }

        [Fact]
        public async Task CA5351UseDESCreateInMethodDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            var aes = DES.Create();  
        }
    }
}",
                GetCSharpResultAt(10, 23, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "DES"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography

Module TestClass
    Sub TestMethod()
        Dim desalg As DES = DES.Create()
    End Sub
End Module",
                GetBasicResultAt(6, 29, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "DES"));
        }

        [Fact]
        public async Task CA5351UseDESCreateInGetDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
   class TestClass
    {
        public DES GetDES
        {
            get { return DES.Create(); }
        }
    }
}",
                GetCSharpResultAt(9, 26, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "get_GetDES", "DES"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Public ReadOnly Property GetDES() As DES
			Get
				Return DES.Create()
			End Get
		End Property
	End Class
End Namespace
",
                GetBasicResultAt(7, 12, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "get_GetDES", "DES"));
        }

        [Fact]
        public async Task CA5351UseDESCreateInFieldDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
    class TestClass
    {
        DES privateDES = DES.Create();
    }
}",
                GetCSharpResultAt(7, 26, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "privateDES", "DES"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Private privateDES As DES = DES.Create()
	End Class
End Namespace",
                GetBasicResultAt(5, 31, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "privateDES", "DES"));
        }

        [Fact]
        public async Task CA5351UseDESCreateInLambdaExpression()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
using System.Threading.Tasks;
namespace TestNamespace
{
    class TestClass
    {
        private async Task TestMethod()
        {
            await Task.Run(() => { DES.Create(); });
        }
    }
}",
                GetCSharpResultAt(10, 36, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "DES"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Imports System.Threading.Tasks
Namespace TestNamespace
	Class TestClass
		Private Async Function TestMethod() As Task
			Await Task.Run(Function() 
			DES.Create()
End Function)
		End Function
	End Class
End Namespace",
                GetBasicResultAt(8, 4, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "DES"));
        }

        [Fact]
        public async Task CA5351UseDESCreateInAnonymousMethodExpression()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
    class TestClass
    {
        delegate void Del();
        Del d = delegate () { DES.Create(); };
    }
}",
                GetCSharpResultAt(8, 31, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "d", "DES"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Private Delegate Sub Del()
		Private d As Del = Sub() DES.Create()
	End Class
End Namespace",
                GetBasicResultAt(6, 28, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "d", "DES"));
        }

        [Fact]
        public async Task CA5351UseDESCryptoServiceProviderCreateInMethodDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            DES des = new DESCryptoServiceProvider();
        }
    }
}",
                GetCSharpResultAt(10, 23, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "DES"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Private Shared Sub TestMethod()
			Dim des As DES = New DESCryptoServiceProvider()
		End Sub
	End Class
End Namespace",
                GetBasicResultAt(6, 21, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "DES"));
        }

        [Fact]
        public async Task CA5351UseDESCryptoServiceProviderCreateInGetDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
   class TestClass
    {
        public DESCryptoServiceProvider GetDES
        {
            get { return new DESCryptoServiceProvider(); }
        }
    }
}",
                GetCSharpResultAt(9, 26, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "get_GetDES", "DES"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Public ReadOnly Property GetDES() As DESCryptoServiceProvider
			Get
				Return New DESCryptoServiceProvider()
			End Get
		End Property
	End Class
End Namespace",
                GetBasicResultAt(7, 12, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "get_GetDES", "DES"));
        }

        [Fact]
        public async Task CA5351UseDESCryptoServiceProviderCreateInFieldDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
    class TestClass
    {
        DESCryptoServiceProvider privateDES = new DESCryptoServiceProvider();
    }
}",
                GetCSharpResultAt(7, 47, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "privateDES", "DES"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Private privateDES As New DESCryptoServiceProvider()
	End Class
End Namespace",
                GetBasicResultAt(5, 25, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "privateDES", "DES"));
        }
        //No VB
        [Fact]
        public async Task CA5351UseDESCryptoServiceProviderInLambdaExpression()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
using System.Threading.Tasks;
namespace TestNamespace
{
    class TestClass
    {
        private async Task TestMethod()
        {
            await Task.Run(() => { new DESCryptoServiceProvider(); });
        }
    }
}",
                GetCSharpResultAt(10, 36, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "DES"));
        }
        //No VB
        [Fact]
        public async Task CA5351UseDESCryptoServiceProviderInAnonymousMethodExpression()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
    class TestClass
    {
        delegate void Del();
        Del d = delegate () { new DESCryptoServiceProvider(); };
    }
}",
                GetCSharpResultAt(8, 31, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "d", "DES"));
        }

        [Fact]
        public async Task CA5351CreateObjectFromDESDerivedClass()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
//Test0
                @"
using System.Security.Cryptography;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            MyDES des = new MyDES();
            des.GenerateKey();
        }
    }
}",
//Test1
                @"
using System;
using System.Security.Cryptography;

namespace TestNamespace
{
    class MyDES : DES
    {
        public override ICryptoTransform CreateDecryptor(byte[] rgbKey, byte[] rgbIV)
        {
            throw new NotImplementedException();
        }

        public override ICryptoTransform CreateEncryptor(byte[] rgbKey, byte[] rgbIV)
        {
            throw new NotImplementedException();
        }

        public override void GenerateIV()
        {
            throw new NotImplementedException();
        }

        public override void GenerateKey()
        {
            throw new NotImplementedException();
        }
    }
}"
},
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(10, 25, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "DES"),
                        GetCSharpResultAt(11, 13, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "DES"),
                    }
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
//Test0
                @"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Private Shared Sub TestMethod()
			Dim des As New MyDES()
			des.GenerateKey()
		End Sub
	End Class
End Namespace",
//Test1
                @"
Imports System
Imports System.Security.Cryptography
Namespace TestNamespace
	Class MyDES
		Inherits DES
		Public Overrides Function CreateDecryptor(rgbKey As Byte(), rgbIV As Byte()) As ICryptoTransform
			Throw New NotImplementedException()
		End Function

		Public Overrides Function CreateEncryptor(rgbKey As Byte(), rgbIV As Byte()) As ICryptoTransform
			Throw New NotImplementedException()
		End Function

		Public Overrides Sub GenerateIV()
			Throw New NotImplementedException()
		End Sub

		Public Overrides Sub GenerateKey()
			Throw New NotImplementedException()
		End Sub
	End Class
End Namespace
"
},
                    ExpectedDiagnostics =
                    {
                        GetBasicResultAt(6, 15, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "DES"),
                        GetBasicResultAt(7, 4, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "DES"),
                    }
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CA5352UseRC2CryptoServiceProviderInMethodDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            var rc2 = new RC2CryptoServiceProvider();
        }
    }
}",
                GetCSharpResultAt(10, 23, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "RC2"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography

Module TestClass
    Sub TestMethod()
        Dim rc2alg As New RC2CryptoServiceProvider
    End Sub
End Module",
                GetBasicResultAt(6, 23, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "RC2"));
        }

        [Fact]
        public async Task CA5352UseRC2CryptoServiceProviderInGetDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
   class TestClass
    {
        public RC2CryptoServiceProvider GetRC2
        {
            get { return new RC2CryptoServiceProvider(); }
        }
    }
}",
                GetCSharpResultAt(9, 26, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "get_GetRC2", "RC2"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Public ReadOnly Property GetRC2() As RC2CryptoServiceProvider
			Get
				Return New RC2CryptoServiceProvider()
			End Get
		End Property
	End Class
End Namespace",
                GetBasicResultAt(7, 12, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "get_GetRC2", "RC2"));
        }

        [Fact]
        public async Task CA5352UseRC2CryptoServiceProviderInFieldDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
    class TestClass
    {
        RC2CryptoServiceProvider privateRC2 = new RC2CryptoServiceProvider();
    }
}",
                GetCSharpResultAt(7, 47, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "privateRC2", "RC2"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Private privateRC2 As New RC2CryptoServiceProvider()
	End Class
End Namespace
",
                GetBasicResultAt(5, 25, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "privateRC2", "RC2"));
        }
        //No VB
        [Fact]
        public async Task CA5352UseRC2CryptoServiceProviderInLambdaExpression()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
using System.Threading.Tasks;
namespace TestNamespace
{
    class TestClass
    {
        private async Task TestMethod()
        {
            await Task.Run(() => { new RC2CryptoServiceProvider(); });
        }
    }
}",
                GetCSharpResultAt(10, 36, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "RC2"));
        }
        //No VB
        [Fact]
        public async Task CA5352UseRC2CryptoServiceProviderInAnonymousMethodExpression()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
    class TestClass
    {
        delegate void Del();
        Del d = delegate () { new RC2CryptoServiceProvider(); };
    }
}",
                GetCSharpResultAt(8, 31, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "d", "RC2"));
        }

        [Fact]
        public async Task CA5352CreateObjectFromRC2DerivedClass()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
//Test0
@"
using System.Security.Cryptography;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            var rc2 = new MyRC2();
        }
    }
}",
//Test1
@"
using System;
using System.Security.Cryptography;

namespace TestNamespace
{
    class MyRC2 : RC2
    {
        public override ICryptoTransform CreateDecryptor(byte[] rgbKey, byte[] rgbIV)
        {
            throw new NotImplementedException();
        }

        public override ICryptoTransform CreateEncryptor(byte[] rgbKey, byte[] rgbIV)
        {
            throw new NotImplementedException();
        }

        public override void GenerateIV()
        {
            throw new NotImplementedException();
        }

        public override void GenerateKey()
        {
            throw new NotImplementedException();
        }
    }
}"
},
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(10, 23, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "RC2"),
                    }
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
//Test0
@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Private Shared Sub TestMethod()
			Dim rc2 = New MyRC2()
		End Sub
	End Class
End Namespace",
//Test1
@"
Imports System
Imports System.Security.Cryptography
Namespace TestNamespace
	Class MyRC2
		Inherits RC2
		Public Overrides Function CreateDecryptor(rgbKey As Byte(), rgbIV As Byte()) As ICryptoTransform
			Throw New NotImplementedException()
		End Function

		Public Overrides Function CreateEncryptor(rgbKey As Byte(), rgbIV As Byte()) As ICryptoTransform
			Throw New NotImplementedException()
		End Function

		Public Overrides Sub GenerateIV()
			Throw New NotImplementedException()
		End Sub

		Public Overrides Sub GenerateKey()
			Throw New NotImplementedException()
		End Sub
	End Class
End Namespace
"
},
                    ExpectedDiagnostics =
                    {
                        GetBasicResultAt(6, 14, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "RC2"),
                    }
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CA5353TripleDESCreateInMethodDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            var tripleDES = TripleDES.Create(""TripleDES"");
        }
    }
}",
                GetCSharpResultAt(10, 29, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "TripleDES"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Private Shared Sub TestMethod()
			Dim tripleDES__1 = TripleDES.Create(""TripleDES"")
        End Sub
    End Class
End Namespace",
                GetBasicResultAt(6, 23, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "TripleDES"));
        }

        [Fact]
        public async Task CA5353TripleDESCreateInGetDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
   class TestClass
    {
        public TripleDES GetTripleDES
        {
            get { return TripleDES.Create(""TripleDES""); }
        }
    }
}",
                GetCSharpResultAt(9, 26, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "get_GetTripleDES", "TripleDES"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Public ReadOnly Property GetTripleDES() As TripleDES
			Get
				Return TripleDES.Create(""TripleDES"")
            End Get
        End Property
    End Class
End Namespace",
                GetBasicResultAt(7, 12, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "get_GetTripleDES", "TripleDES"));
        }

        [Fact]
        public async Task CA5353TripleDESCreateInFieldDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
    class TestClass
    {
        TripleDES privateDES = TripleDES.Create(""TripleDES"");
    }
}",
                GetCSharpResultAt(7, 32, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "privateDES", "TripleDES"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Private privateDES As TripleDES = TripleDES.Create(""TripleDES"")
    End Class
End Namespace",
                GetBasicResultAt(5, 37, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "privateDES", "TripleDES"));
        }
        //No VB
        [Fact]
        public async Task CA5353TripleDESCreateInLambdaExpression()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
using System.Threading.Tasks;
namespace TestNamespace
{
    class TestClass
    {
        private async Task TestMethod()
        {
            await Task.Run(() => { TripleDES.Create(""TripleDES""); });
        }
    }
}",
                GetCSharpResultAt(10, 36, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "TripleDES"));
        }

        [Fact]
        public async Task CA5353TripleDESCreateInAnonymousMethodExpression()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
    class TestClass
    {
        delegate void Del();
        Del d = delegate () { TripleDES.Create(""TripleDES""); };
    }
}",
                GetCSharpResultAt(8, 31, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "d", "TripleDES"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Private Delegate Sub Del()
		Private d As Del = Sub() TripleDES.Create(""TripleDES"")
    End Class
End Namespace",
                GetBasicResultAt(6, 28, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "d", "TripleDES"));
        }

        [Fact]
        public async Task CA5353TripleDESCryptoServiceProviderInMethodDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            TripleDESCryptoServiceProvider tripleDES = new TripleDESCryptoServiceProvider();
        }
    }
}",
                GetCSharpResultAt(10, 56, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "TripleDES"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography

Module TestClass
    Sub TestMethod()
        Dim tDESalg As New TripleDESCryptoServiceProvider
    End Sub
End Module",
                GetBasicResultAt(6, 24, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "TripleDES"));
        }

        [Fact]
        public async Task CA5353TripleDESCryptoServiceProviderInGetDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
   class TestClass
    {
        public TripleDESCryptoServiceProvider GetDES
        {
            get { return new TripleDESCryptoServiceProvider(); }
        }
    }
}",
                GetCSharpResultAt(9, 26, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "get_GetDES", "TripleDES"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Public ReadOnly Property GetDES() As TripleDESCryptoServiceProvider
			Get
				Return New TripleDESCryptoServiceProvider()
			End Get
		End Property
	End Class
End Namespace",
                GetBasicResultAt(7, 12, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "get_GetDES", "TripleDES"));
        }

        [Fact]
        public async Task CA5353TripleDESCryptoServiceProviderInFieldDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
    class TestClass
    {
        TripleDESCryptoServiceProvider privateDES = new TripleDESCryptoServiceProvider();
    }
}",
                GetCSharpResultAt(7, 53, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "privateDES", "TripleDES"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Private privateDES As New TripleDESCryptoServiceProvider()
	End Class
End Namespace",
                GetBasicResultAt(5, 25, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "privateDES", "TripleDES"));
        }
        //No VB
        [Fact]
        public async Task CA5353TripleDESCryptoServiceProviderInLambdaExpression()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
using System.Threading.Tasks;
namespace TestNamespace
{
    class TestClass
    {
        private async Task TestMethod()
        {
            await Task.Run(() => { new TripleDESCryptoServiceProvider(); });
        }
    }
}",
                GetCSharpResultAt(10, 36, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "TripleDES"));
        }
        //No VB
        [Fact]
        public async Task CA5353TripleDESCryptoServiceProviderInAnonymousMethodExpression()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
    class TestClass
    {
        delegate void Del();
        Del d = delegate () { new TripleDESCryptoServiceProvider(); };
    }
}",
                GetCSharpResultAt(8, 31, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "d", "TripleDES"));
        }

        [Fact]
        public async Task CA5353CreateObjectFromTripleDESDerivedClass()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
//Test0
                @"
using System.Security.Cryptography;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            var my3DES = new My3DES();
            my3DES.GenerateKey();
        }
    }
}",
//Test1
                @"
using System;
using System.Security.Cryptography;

namespace TestNamespace
{
    class My3DES : TripleDES
    {
        public override ICryptoTransform CreateDecryptor(byte[] rgbKey, byte[] rgbIV)
        {
            throw new NotImplementedException();
        }

        public override ICryptoTransform CreateEncryptor(byte[] rgbKey, byte[] rgbIV)
        {
            throw new NotImplementedException();
        }

        public override void GenerateIV()
        {
            throw new NotImplementedException();
        }

        public override void GenerateKey()
        {
            throw new NotImplementedException();
        }
    }
}"
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(10, 26, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "TripleDES"),
                        GetCSharpResultAt(11, 13, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "TripleDES"),
                    }
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
//Test0
                @"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Private Shared Sub TestMethod()
			Dim my3DES = New My3DES()
			my3DES.GenerateKey()
		End Sub
	End Class
End Namespace",

//Test1
                @"
Imports System
Imports System.Security.Cryptography

Namespace TestNamespace
	Class My3DES
		Inherits TripleDES
		Public Overrides Function CreateDecryptor(rgbKey As Byte(), rgbIV As Byte()) As ICryptoTransform
			Throw New NotImplementedException()
		End Function

		Public Overrides Function CreateEncryptor(rgbKey As Byte(), rgbIV As Byte()) As ICryptoTransform
			Throw New NotImplementedException()
		End Function

		Public Overrides Sub GenerateIV()
			Throw New NotImplementedException()
		End Sub

		Public Overrides Sub GenerateKey()
			Throw New NotImplementedException()
		End Sub
	End Class
End Namespace
"
                },
                    ExpectedDiagnostics =
                    {
                        GetBasicResultAt(6, 17, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "TripleDES"),
                        GetBasicResultAt(7, 4, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "TripleDES"),
                    }
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CA5350RIPEMD160ManagedInMethodDeclaration()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
using System.Security.Cryptography;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            var md160 = new RIPEMD160Managed();
        }
    }
}",
                ExpectedDiagnostics =
                {
                    GetCSharpResultAt(10, 25, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "RIPEMD160"),
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
Imports System.Security.Cryptography

Module TestClass
    Sub TestMethod()
        Dim md1601alg As New RIPEMD160Managed
    End Sub
End Module",
                ExpectedDiagnostics =
                {
                    GetBasicResultAt(6, 26, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "RIPEMD160"),
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CA5350RIPEMD160ManagedInGetDeclaration()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
using System.Security.Cryptography;
namespace TestNamespace
{
   class TestClass1
    {
        public RIPEMD160Managed GetRIPEMD160
        {
            get { return new RIPEMD160Managed(); }
        }
    }
}",
                ExpectedDiagnostics =
                {
                    GetCSharpResultAt(9, 26, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "get_GetRIPEMD160", "RIPEMD160"),
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass1
		Public ReadOnly Property GetRIPEMD160() As RIPEMD160Managed
			Get
				Return New RIPEMD160Managed()
			End Get
		End Property
	End Class
End Namespace",
                ExpectedDiagnostics =
                {
                    GetBasicResultAt(7, 12, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "get_GetRIPEMD160", "RIPEMD160"),
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CA5350RIPEMD160ManagedInFieldDeclaration()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
using System.Security.Cryptography;
namespace TestNamespace
{
    class TestClass
    {
        RIPEMD160Managed privateRIPEMD160 = new RIPEMD160Managed();
    }
}",
                ExpectedDiagnostics =
                {
                    GetCSharpResultAt(7, 45, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "privateRIPEMD160", "RIPEMD160"),
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Private privateRIPEMD160 As New RIPEMD160Managed()
	End Class
End Namespace
",
                ExpectedDiagnostics =
                {
                    GetBasicResultAt(5, 31, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "privateRIPEMD160", "RIPEMD160"),
                },
            }.RunAsync();
        }
        //No VB
        [Fact]
        public async Task CA5350RIPEMD160ManagedInLambdaExpression()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
using System.Security.Cryptography;
using System.Threading.Tasks;
namespace TestNamespace
{
    class TestClass
    {
        private async Task TestMethod()
        {
            await Task.Run(() => { new RIPEMD160Managed(); });
        }
    }
}",
                ExpectedDiagnostics =
                {
                    GetCSharpResultAt(10, 36, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "RIPEMD160"),
                },
            }.RunAsync();
        }
        //No VB
        [Fact]
        public async Task CA5350RIPEMD160ManagedInAnonymousMethodExpression()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
using System.Security.Cryptography;
namespace TestNamespace
{
    class TestClass
    {
        delegate void Del();
        Del d = delegate () { new RIPEMD160Managed(); };
    }
}",
                ExpectedDiagnostics =
                {
                    GetCSharpResultAt(8, 31, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "d", "RIPEMD160"),
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CA5350RIPEMD160CreateInMethodDeclaration()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
using System.Security.Cryptography;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            RIPEMD160 md160 = RIPEMD160.Create();
        }
    }
}",
                ExpectedDiagnostics =
                {
                    GetCSharpResultAt(10, 31, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "RIPEMD160"),
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Private Shared Sub TestMethod()
			Dim md160 As RIPEMD160 = RIPEMD160.Create()
		End Sub
	End Class
End Namespace",
                ExpectedDiagnostics =
                {
                    GetBasicResultAt(6, 29, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "RIPEMD160"),
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CA5350RIPEMD160CreateInGetDeclaration()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
using System.Security.Cryptography;
namespace TestNamespace
{
   class TestClass1
    {
        public RIPEMD160 GetRIPEMD160
        {
            get { return RIPEMD160.Create(); }
        }
    }
}",
                ExpectedDiagnostics =
                {
                    GetCSharpResultAt(9, 26, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "get_GetRIPEMD160", "RIPEMD160"),
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass1
		Public ReadOnly Property GetRIPEMD160() As RIPEMD160
			Get
				Return RIPEMD160.Create()
			End Get
		End Property
	End Class
End Namespace",
                ExpectedDiagnostics =
                {
                    GetBasicResultAt(7, 12, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "get_GetRIPEMD160", "RIPEMD160"),
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CA5350RIPEMD160CreateInFieldDeclaration()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
using System.Security.Cryptography;
namespace TestNamespace
{
    class TestClass
    {
        RIPEMD160 privateRIPEMD160 = RIPEMD160.Create();
    }
}",
                ExpectedDiagnostics =
                {
                    GetCSharpResultAt(7, 38, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "privateRIPEMD160", "RIPEMD160"),
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Private privateRIPEMD160 As RIPEMD160 = RIPEMD160.Create()
	End Class
End Namespace",
                ExpectedDiagnostics =
                {
                    GetBasicResultAt(5, 43, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "privateRIPEMD160", "RIPEMD160"),
                },
            }.RunAsync();
        }
        //No VB
        [Fact]
        public async Task CA5350RIPEMD160CreateInLambdaExpression()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
using System.Security.Cryptography;
using System.Threading.Tasks;
namespace TestNamespace
{
    class TestClass
    {
        private async Task TestMethod()
        {
            await Task.Run(() => { RIPEMD160.Create(); });
        }
    }
}",
                ExpectedDiagnostics =
                {
                    GetCSharpResultAt(10, 36, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "RIPEMD160"),
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CA5350RIPEMD160CreateInAnonymousMethodExpression()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
using System.Security.Cryptography;
namespace TestNamespace
{
    class TestClass
    {
        delegate void Del();
        Del d = delegate () { RIPEMD160.Create(); };
    }
}",
                ExpectedDiagnostics =
                {
                    GetCSharpResultAt(8, 31, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "d", "RIPEMD160"),
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
Imports System.Security.Cryptography
Namespace TestNamespace
    Class TestClass
        Private Delegate Sub Del()
        Private d As Del = Sub() RIPEMD160.Create()
    End Class
End Namespace",
                ExpectedDiagnostics =
                {
                    GetBasicResultAt(6, 34, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "d", "RIPEMD160"),
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CA5350HMACRIPEMD160InMethodDeclaration()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
using System.Security.Cryptography;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            var md160 = new HMACRIPEMD160();
        }
    }
}",
                ExpectedDiagnostics =
                {
                    GetCSharpResultAt(10, 25, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "HMACRIPEMD160"),
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Private Shared Sub TestMethod()
			Dim md160 = New HMACRIPEMD160()
		End Sub
	End Class
End Namespace",
                ExpectedDiagnostics =
                {
                    GetBasicResultAt(6, 16, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "HMACRIPEMD160"),
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CA5350HMACRIPEMD160InGetDeclaration()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
using System.Security.Cryptography;
namespace TestNamespace
{
   class TestClass1
    {
        public HMACRIPEMD160 GetHMARIPEMD160
        {
            get { return new HMACRIPEMD160(); }
        }
    }
}",
                ExpectedDiagnostics =
                {
                    GetCSharpResultAt(9, 26, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "get_GetHMARIPEMD160", "HMACRIPEMD160"),
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass1
		Public ReadOnly Property GetHMARIPEMD160() As HMACRIPEMD160
			Get
				Return New HMACRIPEMD160()
			End Get
		End Property
	End Class
End Namespace",
                ExpectedDiagnostics =
                {
                    GetBasicResultAt(7, 12, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "get_GetHMARIPEMD160", "HMACRIPEMD160"),
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CA5350HMACRIPEMD160InFieldDeclaration()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
using System.Security.Cryptography;
namespace TestNamespace
{
    class TestClass
    {
        HMACRIPEMD160 privateHMARIPEMD160 = new HMACRIPEMD160();
    }
}",
                ExpectedDiagnostics =
                {
                    GetCSharpResultAt(7, 45, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "privateHMARIPEMD160", "HMACRIPEMD160"),
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Private privateHMARIPEMD160 As New HMACRIPEMD160()
	End Class
End Namespace",
                ExpectedDiagnostics =
                {
                    GetBasicResultAt(5, 34, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "privateHMARIPEMD160", "HMACRIPEMD160"),
                },
            }.RunAsync();
        }
        //No VB
        [Fact]
        public async Task CA5350HMACRIPEMD160InLambdaExpression()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
using System.Security.Cryptography;
using System.Threading.Tasks;
namespace TestNamespace
{
    class TestClass
    {
        private async Task TestMethod()
        {
            await Task.Run(() => { new HMACRIPEMD160(); });
        }
    }
}",
                ExpectedDiagnostics =
                {
                    GetCSharpResultAt(10, 36, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "HMACRIPEMD160"),
                },
            }.RunAsync();
        }
        //No VB
        [Fact]
        public async Task CA5350HMACRIPEMD160InAnonymousMethodExpression()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
using System.Security.Cryptography;
namespace TestNamespace
{
    class TestClass
    {
        delegate void Del();
        Del d = delegate () { new HMACRIPEMD160(); };
    }
}",
                ExpectedDiagnostics =
                {
                    GetCSharpResultAt(8, 31, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "d", "HMACRIPEMD160"),
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CA5350CreateObjectFromRIPEMD160DerivedClass()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestState =
                {
                    Sources =
                    {
//Test0
                @"
using System.Security.Cryptography;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(byte[] inBytes)
        {
            var md160 = new MyRIPEMD160();
        }
    }
}",
//Test1
                @"
using System;
using System.Security.Cryptography;

namespace TestNamespace
{
    class MyRIPEMD160 : RIPEMD160
    {
        public override void Initialize()
        {
            throw new NotImplementedException();
        }

        protected override void HashCore(byte[] array, int ibStart, int cbSize)
        {
            throw new NotImplementedException();
        }

        protected override byte[] HashFinal()
        {
            throw new NotImplementedException();
        }
    }
}"
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(10, 25, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "RIPEMD160"),
                    }
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestState =
                {
                    Sources =
                    {
//Test0
                @"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Private Shared Sub TestMethod(inBytes As Byte())
			Dim md160 = New MyRIPEMD160()
		End Sub
	End Class
End Namespace",
//Test1
                @"
Imports System
Imports System.Security
Imports System.Security.Cryptography
Namespace TestNamespace
	Class MyRIPEMD160
		Inherits RIPEMD160
		Public Overrides Sub Initialize()
			Throw New NotImplementedException()
		End Sub

		Protected Overrides Sub HashCore(array As Byte(), ibStart As Integer, cbSize As Integer)
			Throw New NotImplementedException()
		End Sub

		Protected Overrides Function HashFinal() As Byte()
			Throw New NotImplementedException()
		End Function
	End Class
End Namespace"
                },
                    ExpectedDiagnostics =
                    {
                        GetBasicResultAt(6, 16, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "RIPEMD160"),
                    }
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CA5350CreateObjectFromRIPEMD160ManagedDerivedClass()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestState =
                {
                    Sources =
                    {
//Test0
                @"
using System.Security.Cryptography;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(byte[] inBytes)
        {
            var md160 = new MyRIPEMD160();
        }
    }
}",
//Test1
                @"
using System;
using System.Security.Cryptography;

namespace TestNamespace
{
    class MyRIPEMD160 : RIPEMD160Managed
    {
        public override void Initialize()
        {
            throw new NotImplementedException();
        }

        protected override void HashCore(byte[] array, int ibStart, int cbSize)
        {
            throw new NotImplementedException();
        }

        protected override byte[] HashFinal()
        {
            throw new NotImplementedException();
        }
    }
}"
                },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(10, 25, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "RIPEMD160"),
                    }
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestState =
                {
                    Sources =
                    {
//Test0
                @"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Private Shared Sub TestMethod(inBytes As Byte())
			Dim md160 = New MyRIPEMD160()
		End Sub
	End Class
End Namespace",
//Test1
                @"
Imports System
Imports System.Security.Cryptography
Namespace TestNamespace
	Class MyRIPEMD160
		Inherits RIPEMD160Managed
		Public Overrides Sub Initialize()
			Throw New NotImplementedException()
		End Sub

		Protected Overrides Sub HashCore(array As Byte(), ibStart As Integer, cbSize As Integer)
			Throw New NotImplementedException()
		End Sub

		Protected Overrides Function HashFinal() As Byte()
			Throw New NotImplementedException()
		End Function
	End Class
End Namespace
"
                },
                    ExpectedDiagnostics =
                    {
                        GetBasicResultAt(6, 16, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "RIPEMD160"),
                    }
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CA5350CreateObjectFromHMACRIPEMD160DerivedClass()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
using System.Security.Cryptography;

namespace TestNamespace
{
    class MyHMACRIPEMD160 : HMACRIPEMD160 {}

    class TestClass
    {
        private static void TestMethod()
        {
            var md160 = new MyHMACRIPEMD160();
        }
    }
}",
                ExpectedDiagnostics =
                {
                    GetCSharpResultAt(12, 25, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "HMACRIPEMD160"),
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class MyHMACRIPEMD160
		Inherits HMACRIPEMD160
	End Class

	Class TestClass
		Private Shared Sub TestMethod()
			Dim md160 = New MyHMACRIPEMD160()
		End Sub
	End Class
End Namespace",
                ExpectedDiagnostics =
                {
                    GetBasicResultAt(10, 16, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseWeakCryptographyRule, "TestMethod", "HMACRIPEMD160"),
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CA5356DSACreateSignatureInMethodDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(DSA dsa, byte[] inBytes)
        {
            var sig = dsa.CreateSignature(inBytes);
        }
    }
}",
                GetCSharpResultAt(10, 23, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "DSA"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography

Module TestClass
    Function TestMethod(ByVal bytes As Byte())
        Dim dsa As New DSACryptoServiceProvider
        Return dsa.CreateSignature(bytes)
    End Function
End Module",
                GetBasicResultAt(7, 16, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "DSA"));
        }

        [Fact]
        public async Task CA5356UseDSACreateSignatureInGetDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    DSA dsa1 = null;
    public byte[] MyProperty
    {
        get
        {
            byte[] inBytes = null;
            return dsa1.CreateSignature(inBytes);
        }
    }
}",
                GetCSharpResultAt(12, 20, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "get_MyProperty", "DSA"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography

Class TestClass
	Private dsa1 As DSA = Nothing
	Public ReadOnly Property MyProperty() As Byte()
		Get
			Dim inBytes As Byte() = Nothing
			Return dsa1.CreateSignature(inBytes)
		End Get
	End Property
End Class",
                GetBasicResultAt(9, 11, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "get_MyProperty", "DSA"));
        }

        [Fact]
        public async Task CA5356DSASignatureFormatterInMethodDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            var sf1 = new DSASignatureFormatter();
            var sf2 = new DSASignatureFormatter(new DSACryptoServiceProvider());
        }
    }
}",
                GetCSharpResultAt(10, 23, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "DSA"),
                GetCSharpResultAt(11, 23, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "DSA"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim sf1 = New DSASignatureFormatter()
            Dim sf2 = New DSASignatureFormatter(New DSACryptoServiceProvider())
        End Sub
    End Class
End Namespace",
                GetBasicResultAt(7, 23, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "DSA"),
                GetBasicResultAt(8, 23, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "DSA"));
        }

        [Fact]
        public async Task CA5356UseDSACreateSignatureFormatterInGetDeclaration()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

class TestClass
{
    DSA dsa1 = null;
    public DSASignatureFormatter MyProperty
    {
        get
        {
            DSASignatureFormatter inBytes = null;
            if (inBytes == null) { return new DSASignatureFormatter(); }
            else return new DSASignatureFormatter(new DSACryptoServiceProvider());
        }
    }
}",
                GetCSharpResultAt(12, 43, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "get_MyProperty", "DSA"),
                GetCSharpResultAt(13, 25, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "get_MyProperty", "DSA"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Class TestClass
	Private dsa1 As DSA = Nothing
	Public ReadOnly Property MyProperty() As DSASignatureFormatter
		Get
			Dim inBytes As DSASignatureFormatter = Nothing
			If inBytes Is Nothing Then
				Return New DSASignatureFormatter()
			Else
				Return New DSASignatureFormatter(New DSACryptoServiceProvider())
			End If
		End Get
	End Property
End Class",
                GetBasicResultAt(9, 12, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "get_MyProperty", "DSA"),
                GetBasicResultAt(11, 12, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "get_MyProperty", "DSA"));
        }

        [Fact]
        public async Task CA5356CreateSignatureFromDSADerivedClass()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                //Test0
                @"
using System.Security.Cryptography;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(byte[] inBytes)
        {
            var myDsa = new MyDsa();
            myDsa.CreateSignature(inBytes);
        }
    }
}",
                //Test1
                @"
using System;
using System.Security.Cryptography;

namespace TestNamespace
{
    class MyDsa : DSA
    {
        public override string KeyExchangeAlgorithm
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override string SignatureAlgorithm
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override byte[] CreateSignature(byte[] rgbHash)
        {
            throw new NotImplementedException();
        }

        public override DSAParameters ExportParameters(bool includePrivateParameters)
        {
            throw new NotImplementedException();
        }

        public override void ImportParameters(DSAParameters parameters)
        {
            throw new NotImplementedException();
        }

        public override bool VerifySignature(byte[] rgbHash, byte[] rgbSignature)
        {
            throw new NotImplementedException();
        }
    }
}"
                },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(11, 13, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "DSA"),
                    }
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
//Test0
                @"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Private Shared Sub TestMethod(inBytes As Byte())
			Dim myDsa = New MyDsa()
			myDsa.CreateSignature(inBytes)
		End Sub
	End Class
End Namespace",
//Test1
                @"
Imports System
Imports System.Security.Cryptography

Namespace TestNamespace
	Class MyDsa
		Inherits DSA
		Public Overrides ReadOnly Property KeyExchangeAlgorithm() As String
			Get
				Throw New NotImplementedException()
			End Get
		End Property

		Public Overrides ReadOnly Property SignatureAlgorithm() As String
			Get
				Throw New NotImplementedException()
			End Get
		End Property

		Public Overrides Function CreateSignature(rgbHash As Byte()) As Byte()
			Throw New NotImplementedException()
		End Function

		Public Overrides Function ExportParameters(includePrivateParameters As Boolean) As DSAParameters
			Throw New NotImplementedException()
		End Function

		Public Overrides Sub ImportParameters(parameters As DSAParameters)
			Throw New NotImplementedException()
		End Sub

		Public Overrides Function VerifySignature(rgbHash As Byte(), rgbSignature As Byte()) As Boolean
			Throw New NotImplementedException()
		End Function
	End Class
End Namespace"
                },
                    ExpectedDiagnostics =
                    {
                        GetBasicResultAt(7, 4, DoNotUseInsecureCryptographicAlgorithmsAnalyzer.DoNotUseBrokenCryptographyRule, "TestMethod", "DSA"),
                    }
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CA5357RijndaelManagedInMethodDeclarationShouldNotGenerateDiagnostics()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            var rc2 = new RijndaelManaged();
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography

Module TestClass
    Sub TestMethod()
        Dim rijndaelalg As New RijndaelManaged
    End Sub
End Module"
            );
        }

        [Fact]
        public async Task CA5357RijndaelManagedInGetDeclarationShouldNotGenerateDiagnostics()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
   class TestClass1
    {
        public RijndaelManaged GetRijndael
        {
            get { return new RijndaelManaged(); }
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass1
		Public ReadOnly Property GetRijndael() As RijndaelManaged
			Get
				Return New RijndaelManaged()
			End Get
		End Property
	End Class
End Namespace"
            );
        }

        [Fact]
        public async Task CA5357RijndaelManagedInFieldDeclarationShouldNotGenerateDiagnostics()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
    class TestClass
    {
        RijndaelManaged privateRijndael = new RijndaelManaged();
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Private privateRijndael As New RijndaelManaged()
	End Class
End Namespace"
            );
        }
        //No VB
        [Fact]
        public async Task CA5357RijndaelManagedInLambdaExpressionShouldNotGenerateDiagnostics()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
using System.Threading.Tasks;
namespace TestNamespace
{
    class TestClass
    {
        private async Task TestMethod()
        {
            await Task.Run(() => { new RijndaelManaged(); });
        }
    }
}"
            );
        }
        //No VB
        [Fact]
        public async Task CA5357RijndaelManagedInAnonymousMethodExpressionShouldNotGenerateDiagnostics()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Security.Cryptography;
namespace TestNamespace
{
    class TestClass
    {
        delegate void Del();
        Del d = delegate () { new RijndaelManaged(); };
    }
}"
            );
        }

        [Fact]
        public async Task CA5357CreateObjectFromRijndaelDerivedClassShouldNotGenerateDiagnostics()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
//Test0
                @"
using System.Security.Cryptography;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            var rc2 = new MyRijndael();
        }
    }
}",
//Test1
                @"
using System;
using System.Security.Cryptography;

namespace TestNamespace
{
    class MyRijndael : Rijndael
    {
        public override ICryptoTransform CreateDecryptor(byte[] rgbKey, byte[] rgbIV)
        {
            throw new NotImplementedException();
        }

        public override ICryptoTransform CreateEncryptor(byte[] rgbKey, byte[] rgbIV)
        {
            throw new NotImplementedException();
        }

        public override void GenerateIV()
        {
            throw new NotImplementedException();
        }

        public override void GenerateKey()
        {
            throw new NotImplementedException();
        }
    }
}"
                    }
                }
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
//Test0
                @"
Imports System.Security.Cryptography
Namespace TestNamespace
	Class TestClass
		Private Shared Sub TestMethod()
			Dim rc2 = New MyRijndael()
		End Sub
	End Class
End Namespace",
//Test1
                @"
Imports System
Imports System.Security.Cryptography
Namespace TestNamespace
	Class MyRijndael
		Inherits Rijndael
		Public Overrides Function CreateDecryptor(rgbKey As Byte(), rgbIV As Byte()) As ICryptoTransform
			Throw New NotImplementedException()
		End Function

		Public Overrides Function CreateEncryptor(rgbKey As Byte(), rgbIV As Byte()) As ICryptoTransform
			Throw New NotImplementedException()
		End Function

		Public Overrides Sub GenerateIV()
			Throw New NotImplementedException()
		End Sub

		Public Overrides Sub GenerateKey()
			Throw New NotImplementedException()
		End Sub
	End Class
End Namespace"
                    }
                }
            }.RunAsync();
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, DiagnosticDescriptor rule, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);

        private static DiagnosticResult GetBasicResultAt(int line, int column, DiagnosticDescriptor rule, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);
    }
}
