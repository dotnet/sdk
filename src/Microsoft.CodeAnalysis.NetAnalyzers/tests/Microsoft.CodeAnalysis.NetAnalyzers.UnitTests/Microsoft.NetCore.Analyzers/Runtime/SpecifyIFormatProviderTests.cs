// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.SpecifyIFormatProviderAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpSpecifyIFormatProviderFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.SpecifyIFormatProviderAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicSpecifyIFormatProviderFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class SpecifyIFormatProviderTests
    {
        [Fact]
        public async Task CA1305_StringReturningStringFormatOverloads_CSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;
using System.Threading;

public static class IFormatProviderStringTest
{
    public static string SpecifyIFormatProvider1()
    {
        return string.Format(""aaa {0}"", ""bbb"");
    }

    public static string SpecifyIFormatProvider2()
    {
        return string.Format(""aaa {0} {1}"", ""bbb"", ""ccc"");
    }

    public static string SpecifyIFormatProvider3()
    {
        return string.Format(""aaa {0} {1} {2}"", ""bbb"", ""ccc"", ""ddd"");
    }

    public static string SpecifyIFormatProvider4()
    {
        return string.Format(""aaa {0} {1} {2} {3}"", ""bbb"", ""ccc"", ""ddd"", """");
    }
}",
GetIFormatProviderAlternateStringRuleCSharpResultAt(10, 16, "string.Format(string, object)",
                                                            "IFormatProviderStringTest.SpecifyIFormatProvider1()",
                                                            "string.Format(IFormatProvider, string, params object[])"),
GetIFormatProviderAlternateStringRuleCSharpResultAt(15, 16, "string.Format(string, object, object)",
                                                            "IFormatProviderStringTest.SpecifyIFormatProvider2()",
                                                            "string.Format(IFormatProvider, string, params object[])"),
GetIFormatProviderAlternateStringRuleCSharpResultAt(20, 16, "string.Format(string, object, object, object)",
                                                            "IFormatProviderStringTest.SpecifyIFormatProvider3()",
                                                            "string.Format(IFormatProvider, string, params object[])"),
GetIFormatProviderAlternateStringRuleCSharpResultAt(25, 16, "string.Format(string, params object[])",
                                                            "IFormatProviderStringTest.SpecifyIFormatProvider4()",
                                                            "string.Format(IFormatProvider, string, params object[])"));
        }

        [Fact]
        public async Task CA1305_StringReturningUserMethodOverloads_CSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;
using System.Threading;

public static class IFormatProviderStringTest
{
    public static void SpecifyIFormatProvider()
    {
        IFormatProviderOverloads.LeadingIFormatProviderReturningString(""aaa"");
        IFormatProviderOverloads.TrailingIFormatProviderReturningString(""aaa"");
        IFormatProviderOverloads.UserDefinedParamsMatchMethodOverload(""aaa"");
    }
}

internal static class IFormatProviderOverloads
{
    public static string LeadingIFormatProviderReturningString(string format)
    {
        return LeadingIFormatProviderReturningString(CultureInfo.CurrentCulture, format);
    }

    public static string LeadingIFormatProviderReturningString(IFormatProvider provider, string format)
    {
        return string.Format(provider, format);
    }

    public static string TrailingIFormatProviderReturningString(string format)
    {
        return TrailingIFormatProviderReturningString(format, CultureInfo.CurrentCulture);
    }

    public static string TrailingIFormatProviderReturningString(string format, IFormatProvider provider)
    {
        return string.Format(provider, format);
    }

    public static string TrailingIFormatProviderReturningString(IFormatProvider provider, string format)
    {
        return string.Format(provider, format);
    }

    public static string UserDefinedParamsMatchMethodOverload(string format, params object[] objects)
    {
        return null;
    }

    public static string UserDefinedParamsMatchMethodOverload(IFormatProvider provider, string format, params object[] objs)
    {
        return null;
    }
}",
GetIFormatProviderAlternateStringRuleCSharpResultAt(10, 9, "IFormatProviderOverloads.LeadingIFormatProviderReturningString(string)",
                                                           "IFormatProviderStringTest.SpecifyIFormatProvider()",
                                                           "IFormatProviderOverloads.LeadingIFormatProviderReturningString(IFormatProvider, string)"),
GetIFormatProviderAlternateStringRuleCSharpResultAt(11, 9, "IFormatProviderOverloads.TrailingIFormatProviderReturningString(string)",
                                                           "IFormatProviderStringTest.SpecifyIFormatProvider()",
                                                           "IFormatProviderOverloads.TrailingIFormatProviderReturningString(string, IFormatProvider)"),
GetIFormatProviderAlternateStringRuleCSharpResultAt(12, 9, "IFormatProviderOverloads.UserDefinedParamsMatchMethodOverload(string, params object[])",
                                                           "IFormatProviderStringTest.SpecifyIFormatProvider()",
                                                           "IFormatProviderOverloads.UserDefinedParamsMatchMethodOverload(IFormatProvider, string, params object[])"));
        }

        [Fact]
        public async Task CA1305_StringReturningNoDiagnostics_CSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;
using System.Threading;

public static class IFormatProviderStringTest
{
    public static void SpecifyIFormatProvider6()
    {
        IFormatProviderOverloads.IFormatProviderAsDerivedTypeOverload(""aaa"");
    }

    public static void SpecifyIFormatProvider7()
    {
        IFormatProviderOverloads.UserDefinedParamsMismatchMethodOverload(""aaa"");
    }

    public static void SpecifyIFormatProvider8()
    {
        IFormatProviderOverloads.MethodOverloadWithMismatchRefKind(""aaa"");
    }
}

internal static class IFormatProviderOverloads
{
    public static string IFormatProviderAsDerivedTypeOverload(string format)
    {
        return null;
    }

    public static string IFormatProviderAsDerivedTypeOverload(DerivedClass provider, string format)
    {
        return null;
    }

    public static string UserDefinedParamsMismatchMethodOverload(string format)
    {
        return null;
    }

    public static string UserDefinedParamsMismatchMethodOverload(IFormatProvider provider, string format, params object[] objs)
    {
        return null;
    }

    public static string MethodOverloadWithMismatchRefKind(string format)
    {
        return null;
    }

    public static string MethodOverloadWithMismatchRefKind(IFormatProvider provider, ref string format)
    {
        return null;
    }

    public static string MethodOverloadWithMismatchRefKind(out IFormatProvider provider, string format)
    {
        provider = null;
        return null;
    }
}

public class DerivedClass : IFormatProvider
{
    public object GetFormat(Type formatType)
    {
        throw new NotImplementedException();
    }
}");
        }

        [Fact]
        public async Task CA1305_NonStringReturningStringFormatOverloads_CSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;

public static class IFormatProviderStringTest
{
    public static void TestMethod()
    {
        int x = Convert.ToInt32(""1"");
        long y = Convert.ToInt64(""1"");
        IFormatProviderOverloads.LeadingIFormatProvider(""1"");
        IFormatProviderOverloads.TrailingIFormatProvider(""1"");
    }
}

internal static class IFormatProviderOverloads
{
    public static void LeadingIFormatProvider(string format)
    {
        LeadingIFormatProvider(CultureInfo.CurrentCulture, format);
    }

    public static void LeadingIFormatProvider(IFormatProvider provider, string format)
    {
        Console.WriteLine(string.Format(provider, format));
    }

    public static void TrailingIFormatProvider(string format)
    {
        TrailingIFormatProvider(format, CultureInfo.CurrentCulture);
    }

    public static void TrailingIFormatProvider(string format, IFormatProvider provider)
    {
        Console.WriteLine(string.Format(provider, format));
    }
}",
GetIFormatProviderAlternateRuleCSharpResultAt(9, 17, "Convert.ToInt32(string)",
                                                     "IFormatProviderStringTest.TestMethod()",
                                                     "Convert.ToInt32(string, IFormatProvider)"),
GetIFormatProviderAlternateRuleCSharpResultAt(10, 18, "Convert.ToInt64(string)",
                                                      "IFormatProviderStringTest.TestMethod()",
                                                      "Convert.ToInt64(string, IFormatProvider)"),
GetIFormatProviderAlternateRuleCSharpResultAt(11, 9, "IFormatProviderOverloads.LeadingIFormatProvider(string)",
                                                     "IFormatProviderStringTest.TestMethod()",
                                                     "IFormatProviderOverloads.LeadingIFormatProvider(IFormatProvider, string)"),
GetIFormatProviderAlternateRuleCSharpResultAt(12, 9, "IFormatProviderOverloads.TrailingIFormatProvider(string)",
                                                     "IFormatProviderStringTest.TestMethod()",
                                                     "IFormatProviderOverloads.TrailingIFormatProvider(string, IFormatProvider)"));
        }

        [Fact]
        public async Task CA1305_NonStringReturningStringFormatOverloads_TargetMethodNoGenerics_CSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public static class IFormatProviderStringTest
{
    public static void TestMethod()
    {
        IFormatProviderOverloads.TargetMethodIsNonGeneric(""1"");
        IFormatProviderOverloads.TargetMethodIsGeneric<int>(""1""); // No Diagnostics because the target method can be generic
    }
}

internal static class IFormatProviderOverloads
{
    public static void TargetMethodIsNonGeneric(string format)
    {
    }

    public static void TargetMethodIsNonGeneric<T>(string format, IFormatProvider provider)
    {
    }

    public static void TargetMethodIsGeneric<T>(string format)
    {
    }

    public static void TargetMethodIsGeneric(string format, IFormatProvider provider)
    {
    }
}",
GetIFormatProviderAlternateRuleCSharpResultAt(8, 9, "IFormatProviderOverloads.TargetMethodIsNonGeneric(string)",
                                                    "IFormatProviderStringTest.TestMethod()",
                                                    "IFormatProviderOverloads.TargetMethodIsNonGeneric<T>(string, IFormatProvider)"));
        }

        [Fact]
        public async Task CA1305_StringReturningUICultureIFormatProvider_CSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;
using System.Threading;

public static class UICultureAsIFormatProviderReturningStringTest
{
    public static void TestMethod()
    {
        IFormatProviderOverloads.IFormatProviderReturningString(""1"", CultureInfo.CurrentUICulture);
        IFormatProviderOverloads.IFormatProviderReturningString(""1"", CultureInfo.InstalledUICulture);
        IFormatProviderOverloads.IFormatProviderReturningString(""1"", Thread.CurrentThread.CurrentUICulture);
        IFormatProviderOverloads.IFormatProviderReturningString(""1"", Thread.CurrentThread.CurrentUICulture, CultureInfo.InstalledUICulture);
    }
}

internal static class IFormatProviderOverloads
{
    public static string IFormatProviderReturningString(string format, IFormatProvider provider)
    {
        return null;
    }

    public static string IFormatProviderReturningString(string format, IFormatProvider provider, IFormatProvider provider2)
    {
        return null;
    }
}",
GetIFormatProviderAlternateStringRuleCSharpResultAt(10, 9, "IFormatProviderOverloads.IFormatProviderReturningString(string, IFormatProvider)",
                                                           "UICultureAsIFormatProviderReturningStringTest.TestMethod()",
                                                           "IFormatProviderOverloads.IFormatProviderReturningString(string, IFormatProvider, IFormatProvider)"),
GetIFormatProviderUICultureStringRuleCSharpResultAt(10, 9, "UICultureAsIFormatProviderReturningStringTest.TestMethod()",
                                                           "CultureInfo.CurrentUICulture",
                                                           "IFormatProviderOverloads.IFormatProviderReturningString(string, IFormatProvider)"),
GetIFormatProviderAlternateStringRuleCSharpResultAt(11, 9, "IFormatProviderOverloads.IFormatProviderReturningString(string, IFormatProvider)",
                                                           "UICultureAsIFormatProviderReturningStringTest.TestMethod()",
                                                           "IFormatProviderOverloads.IFormatProviderReturningString(string, IFormatProvider, IFormatProvider)"),
GetIFormatProviderUICultureStringRuleCSharpResultAt(11, 9, "UICultureAsIFormatProviderReturningStringTest.TestMethod()",
                                                           "CultureInfo.InstalledUICulture",
                                                           "IFormatProviderOverloads.IFormatProviderReturningString(string, IFormatProvider)"),
GetIFormatProviderAlternateStringRuleCSharpResultAt(12, 9, "IFormatProviderOverloads.IFormatProviderReturningString(string, IFormatProvider)",
                                                           "UICultureAsIFormatProviderReturningStringTest.TestMethod()",
                                                           "IFormatProviderOverloads.IFormatProviderReturningString(string, IFormatProvider, IFormatProvider)"),
GetIFormatProviderUICultureStringRuleCSharpResultAt(12, 9, "UICultureAsIFormatProviderReturningStringTest.TestMethod()",
                                                           "Thread.CurrentUICulture",
                                                           "IFormatProviderOverloads.IFormatProviderReturningString(string, IFormatProvider)"),
GetIFormatProviderUICultureStringRuleCSharpResultAt(13, 9, "UICultureAsIFormatProviderReturningStringTest.TestMethod()",
                                                           "Thread.CurrentUICulture",
                                                           "IFormatProviderOverloads.IFormatProviderReturningString(string, IFormatProvider, IFormatProvider)"),
GetIFormatProviderUICultureStringRuleCSharpResultAt(13, 9, "UICultureAsIFormatProviderReturningStringTest.TestMethod()",
                                                           "CultureInfo.InstalledUICulture",
                                                           "IFormatProviderOverloads.IFormatProviderReturningString(string, IFormatProvider, IFormatProvider)"));
        }

        [Fact]
        public async Task CA1305_NonStringReturningUICultureIFormatProvider_CSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;
using System.Threading;

public static class UICultureAsIFormatProviderReturningNonStringTest
{
    public static void TestMethod()
    {
        IFormatProviderOverloads.IFormatProviderReturningNonString(""1"", CultureInfo.CurrentUICulture);
        IFormatProviderOverloads.IFormatProviderReturningNonString(""1"", CultureInfo.InstalledUICulture);
        IFormatProviderOverloads.IFormatProviderReturningNonString(""1"", Thread.CurrentThread.CurrentUICulture);
        IFormatProviderOverloads.IFormatProviderReturningNonString(""1"", Thread.CurrentThread.CurrentUICulture, CultureInfo.InstalledUICulture);
    }
}

internal static class IFormatProviderOverloads
{
    public static void IFormatProviderReturningNonString(string format, IFormatProvider provider)
    {
    }

    public static void IFormatProviderReturningNonString(string format, IFormatProvider provider, IFormatProvider provider2)
    {
    }
}",
GetIFormatProviderAlternateRuleCSharpResultAt(10, 9, "IFormatProviderOverloads.IFormatProviderReturningNonString(string, IFormatProvider)",
                                                     "UICultureAsIFormatProviderReturningNonStringTest.TestMethod()",
                                                     "IFormatProviderOverloads.IFormatProviderReturningNonString(string, IFormatProvider, IFormatProvider)"),
GetIFormatProviderUICultureRuleCSharpResultAt(10, 9, "UICultureAsIFormatProviderReturningNonStringTest.TestMethod()",
                                                     "CultureInfo.CurrentUICulture",
                                                     "IFormatProviderOverloads.IFormatProviderReturningNonString(string, IFormatProvider)"),
GetIFormatProviderAlternateRuleCSharpResultAt(11, 9, "IFormatProviderOverloads.IFormatProviderReturningNonString(string, IFormatProvider)",
                                                     "UICultureAsIFormatProviderReturningNonStringTest.TestMethod()",
                                                     "IFormatProviderOverloads.IFormatProviderReturningNonString(string, IFormatProvider, IFormatProvider)"),
GetIFormatProviderUICultureRuleCSharpResultAt(11, 9, "UICultureAsIFormatProviderReturningNonStringTest.TestMethod()",
                                                     "CultureInfo.InstalledUICulture",
                                                     "IFormatProviderOverloads.IFormatProviderReturningNonString(string, IFormatProvider)"),
GetIFormatProviderAlternateRuleCSharpResultAt(12, 9, "IFormatProviderOverloads.IFormatProviderReturningNonString(string, IFormatProvider)",
                                                     "UICultureAsIFormatProviderReturningNonStringTest.TestMethod()",
                                                     "IFormatProviderOverloads.IFormatProviderReturningNonString(string, IFormatProvider, IFormatProvider)"),
GetIFormatProviderUICultureRuleCSharpResultAt(12, 9, "UICultureAsIFormatProviderReturningNonStringTest.TestMethod()",
                                                     "Thread.CurrentUICulture",
                                                     "IFormatProviderOverloads.IFormatProviderReturningNonString(string, IFormatProvider)"),
GetIFormatProviderUICultureRuleCSharpResultAt(13, 9, "UICultureAsIFormatProviderReturningNonStringTest.TestMethod()",
                                                     "Thread.CurrentUICulture",
                                                     "IFormatProviderOverloads.IFormatProviderReturningNonString(string, IFormatProvider, IFormatProvider)"),
GetIFormatProviderUICultureRuleCSharpResultAt(13, 9, "UICultureAsIFormatProviderReturningNonStringTest.TestMethod()",
                                                     "CultureInfo.InstalledUICulture",
                                                     "IFormatProviderOverloads.IFormatProviderReturningNonString(string, IFormatProvider, IFormatProvider)"));
        }

        [Fact]
        public async Task CA1305_AcceptNullForIFormatProvider_CSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;
using System.Threading;

public static class UICultureAsIFormatProviderReturningStringTest
{
    public static void TestMethod()
    {
        IFormatProviderOverloads.IFormatProviderReturningString(""1"", null);
    }
}

internal static class IFormatProviderOverloads
{
    public static string IFormatProviderReturningString(string format, IFormatProvider provider)
    {
        return null;
    }
}");
        }

        [Fact]
        public async Task CA1305_DoesNotRecommendObsoleteOverload_CSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;
using System.Threading;

public static class TestClass
{
    public static void TestMethod()
    {
        IFormatProviderOverloads.TrailingObsoleteIFormatProvider(""1"");
    }
}

internal static class IFormatProviderOverloads
{
    public static string TrailingObsoleteIFormatProvider(string format)
    {
        return null;
    }

    [Obsolete]
    public static string TrailingObsoleteIFormatProvider(string format, IFormatProvider provider)
    {
        return null;
    }
}");
        }

        [Fact]
        public async Task CA1305_RuleException_NoDiagnostics_CSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;
using System.Threading;

public static class IFormatProviderStringTest
{
    public static void TrailingThreadCurrentUICulture()
    {
        var s = new System.Resources.ResourceManager(null);
        Console.WriteLine(s.GetObject("""", Thread.CurrentThread.CurrentUICulture));
        Console.WriteLine(s.GetStream("""", Thread.CurrentThread.CurrentUICulture));
        Console.WriteLine(s.GetResourceSet(Thread.CurrentThread.CurrentUICulture, false, false));

        var activator = Activator.CreateInstance(null, System.Reflection.BindingFlags.CreateInstance, null, null, Thread.CurrentThread.CurrentUICulture);
        Console.WriteLine(activator);
    }
}");
        }

        [Fact]
        public async Task CA1305_StringReturningStringFormatOverloads_VisualBasicAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Globalization
Imports System.Threading

Public NotInheritable Class IFormatProviderStringTest
    Private Sub New()
    End Sub

    Public Shared Function SpecifyIFormatProvider1() As String
        Return String.Format(""aaa {0}"", ""bbb"")
    End Function

    Public Shared Function SpecifyIFormatProvider2() As String
        Return String.Format(""aaa {0} {1}"", ""bbb"", ""ccc"")
    End Function

    Public Shared Function SpecifyIFormatProvider3() As String
        Return String.Format(""aaa {0} {1} {2}"", ""bbb"", ""ccc"", ""ddd"")
    End Function

    Public Shared Function SpecifyIFormatProvider4() As String
        Return String.Format(""aaa {0} {1} {2} {3}"", ""bbb"", ""ccc"", ""ddd"", """")
    End Function
End Class",
GetIFormatProviderAlternateStringRuleBasicResultAt(11, 16, "String.Format(String, Object)",
                                                           "IFormatProviderStringTest.SpecifyIFormatProvider1()",
                                                           "String.Format(IFormatProvider, String, ParamArray Object())"),
GetIFormatProviderAlternateStringRuleBasicResultAt(15, 16, "String.Format(String, Object, Object)",
                                                           "IFormatProviderStringTest.SpecifyIFormatProvider2()",
                                                           "String.Format(IFormatProvider, String, ParamArray Object())"),
GetIFormatProviderAlternateStringRuleBasicResultAt(19, 16, "String.Format(String, Object, Object, Object)",
                                                           "IFormatProviderStringTest.SpecifyIFormatProvider3()",
                                                           "String.Format(IFormatProvider, String, ParamArray Object())"),
GetIFormatProviderAlternateStringRuleBasicResultAt(23, 16, "String.Format(String, ParamArray Object())",
                                                           "IFormatProviderStringTest.SpecifyIFormatProvider4()",
                                                           "String.Format(IFormatProvider, String, ParamArray Object())"));
        }

        [Fact]
        public async Task CA1305_StringReturningUserMethodOverloads_VisualBasicAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Globalization
Imports System.Threading

Public NotInheritable Class IFormatProviderStringTest
    Private Sub New()
    End Sub
    Public Shared Sub SpecifyIFormatProvider()
        IFormatProviderOverloads.LeadingIFormatProviderReturningString(""aaa"")
        IFormatProviderOverloads.TrailingIFormatProviderReturningString(""aaa"")
        IFormatProviderOverloads.UserDefinedParamsMatchMethodOverload(""aaa"")
    End Sub
End Class

Friend NotInheritable Class IFormatProviderOverloads
    Private Sub New()
    End Sub
    Public Shared Function LeadingIFormatProviderReturningString(format As String) As String
        Return LeadingIFormatProviderReturningString(CultureInfo.CurrentCulture, format)
    End Function

    Public Shared Function LeadingIFormatProviderReturningString(provider As IFormatProvider, format As String) As String
        Return String.Format(provider, format)
    End Function

    Public Shared Function TrailingIFormatProviderReturningString(format As String) As String
        Return TrailingIFormatProviderReturningString(format, CultureInfo.CurrentCulture)
    End Function

    Public Shared Function TrailingIFormatProviderReturningString(format As String, provider As IFormatProvider) As String
        Return String.Format(provider, format)
    End Function

    Public Shared Function TrailingIFormatProviderReturningString(provider As IFormatProvider, format As String) As String
        Return String.Format(provider, format)
    End Function

    Public Shared Function UserDefinedParamsMatchMethodOverload(format As String, ParamArray objects As Object()) As String
        Return Nothing
    End Function

    Public Shared Function UserDefinedParamsMatchMethodOverload(provider As IFormatProvider, format As String, ParamArray objs As Object()) As String
        Return Nothing
    End Function
End Class",
 GetIFormatProviderAlternateStringRuleBasicResultAt(10, 9, "IFormatProviderOverloads.LeadingIFormatProviderReturningString(String)",
                                                           "IFormatProviderStringTest.SpecifyIFormatProvider()",
                                                           "IFormatProviderOverloads.LeadingIFormatProviderReturningString(IFormatProvider, String)"),
 GetIFormatProviderAlternateStringRuleBasicResultAt(11, 9, "IFormatProviderOverloads.TrailingIFormatProviderReturningString(String)",
                                                           "IFormatProviderStringTest.SpecifyIFormatProvider()",
                                                           "IFormatProviderOverloads.TrailingIFormatProviderReturningString(String, IFormatProvider)"),
 GetIFormatProviderAlternateStringRuleBasicResultAt(12, 9, "IFormatProviderOverloads.UserDefinedParamsMatchMethodOverload(String, ParamArray Object())",
                                                           "IFormatProviderStringTest.SpecifyIFormatProvider()",
                                                           "IFormatProviderOverloads.UserDefinedParamsMatchMethodOverload(IFormatProvider, String, ParamArray Object())"));
        }

        [Fact]
        public async Task CA1305_StringReturningNoDiagnostics_VisualBasicAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Globalization
Imports System.Threading

Public NotInheritable Class IFormatProviderStringTest
    Private Sub New()
    End Sub
    Public Shared Sub SpecifyIFormatProvider6()
        IFormatProviderOverloads.IFormatProviderAsDerivedTypeOverload(""aaa"")
    End Sub

    Public Shared Sub SpecifyIFormatProvider7()
        IFormatProviderOverloads.UserDefinedParamsMismatchMethodOverload(""aaa"")
    End Sub
End Class

Friend NotInheritable Class IFormatProviderOverloads
    Private Sub New()
    End Sub

    Public Shared Function IFormatProviderAsDerivedTypeOverload(format As String) As String
        Return Nothing
    End Function

    Public Shared Function IFormatProviderAsDerivedTypeOverload(provider As DerivedClass, format As String) As String
        Return Nothing
    End Function

    Public Shared Function UserDefinedParamsMismatchMethodOverload(format As String) As String
        Return Nothing
    End Function

    Public Shared Function UserDefinedParamsMismatchMethodOverload(provider As IFormatProvider, format As String, ParamArray objs As Object()) As String
        Return Nothing
    End Function
End Class

Public Class DerivedClass
    Implements IFormatProvider

    Public Function GetFormat(formatType As Type) As Object Implements IFormatProvider.GetFormat
        Throw New NotImplementedException()
    End Function
End Class");
        }

        [Fact]
        public async Task CA1305_NonStringReturningStringFormatOverloads_VisualBasicAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Globalization
Imports System.Threading

Public NotInheritable Class IFormatProviderStringTest
    Private Sub New()
    End Sub
    Public Shared Sub TestMethod()
        Dim x As Integer = Convert.ToInt32(""1"")
        Dim y As Long = Convert.ToInt64(""1"")
        IFormatProviderOverloads.LeadingIFormatProvider(""1"")
        IFormatProviderOverloads.TrailingIFormatProvider(""1"")
    End Sub
End Class

Friend NotInheritable Class IFormatProviderOverloads
    Private Sub New()
    End Sub
    Public Shared Sub LeadingIFormatProvider(format As String)
        LeadingIFormatProvider(CultureInfo.CurrentCulture, format)
    End Sub

    Public Shared Sub LeadingIFormatProvider(provider As IFormatProvider, format As String)
        Console.WriteLine(String.Format(provider, format))
    End Sub

    Public Shared Sub TrailingIFormatProvider(format As String)
        TrailingIFormatProvider(format, CultureInfo.CurrentCulture)
    End Sub

    Public Shared Sub TrailingIFormatProvider(format As String, provider As IFormatProvider)
        Console.WriteLine(String.Format(provider, format))
    End Sub
End Class",
 GetIFormatProviderAlternateRuleBasicResultAt(10, 28, "Convert.ToInt32(String)",
                                                      "IFormatProviderStringTest.TestMethod()",
                                                      "Convert.ToInt32(String, IFormatProvider)"),
 GetIFormatProviderAlternateRuleBasicResultAt(11, 25, "Convert.ToInt64(String)",
                                                      "IFormatProviderStringTest.TestMethod()",
                                                      "Convert.ToInt64(String, IFormatProvider)"),
 GetIFormatProviderAlternateRuleBasicResultAt(12, 9, "IFormatProviderOverloads.LeadingIFormatProvider(String)",
                                                     "IFormatProviderStringTest.TestMethod()",
                                                     "IFormatProviderOverloads.LeadingIFormatProvider(IFormatProvider, String)"),
 GetIFormatProviderAlternateRuleBasicResultAt(13, 9, "IFormatProviderOverloads.TrailingIFormatProvider(String)",
                                                     "IFormatProviderStringTest.TestMethod()",
                                                     "IFormatProviderOverloads.TrailingIFormatProvider(String, IFormatProvider)"));
        }

        [Fact]
        public async Task CA1305_StringReturningUICultureIFormatProvider_VisualBasicAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Globalization
Imports System.Threading

Public NotInheritable Class UICultureAsIFormatProviderReturningStringTest
    Private Sub New()
    End Sub
    Public Shared Sub TestMethod()
        IFormatProviderOverloads.IFormatProviderReturningString(""1"", CultureInfo.CurrentUICulture)
        IFormatProviderOverloads.IFormatProviderReturningString(""1"", CultureInfo.InstalledUICulture)
        IFormatProviderOverloads.IFormatProviderReturningString(""1"", Thread.CurrentThread.CurrentUICulture)
        IFormatProviderOverloads.IFormatProviderReturningString(""1"", Thread.CurrentThread.CurrentUICulture, CultureInfo.InstalledUICulture)
    End Sub
End Class

Friend NotInheritable Class IFormatProviderOverloads
    Private Sub New()
    End Sub
    Public Shared Function IFormatProviderReturningString(format As String, provider As IFormatProvider) As String
        Return Nothing
    End Function

    Public Shared Function IFormatProviderReturningString(format As String, provider As IFormatProvider, provider2 As IFormatProvider) As String
        Return Nothing
    End Function
End Class",
 GetIFormatProviderAlternateStringRuleBasicResultAt(10, 9, "IFormatProviderOverloads.IFormatProviderReturningString(String, IFormatProvider)",
                                                           "UICultureAsIFormatProviderReturningStringTest.TestMethod()",
                                                           "IFormatProviderOverloads.IFormatProviderReturningString(String, IFormatProvider, IFormatProvider)"),
 GetIFormatProviderUICultureStringRuleBasicResultAt(10, 9, "UICultureAsIFormatProviderReturningStringTest.TestMethod()",
                                                           "CultureInfo.CurrentUICulture",
                                                           "IFormatProviderOverloads.IFormatProviderReturningString(String, IFormatProvider)"),
 GetIFormatProviderAlternateStringRuleBasicResultAt(11, 9, "IFormatProviderOverloads.IFormatProviderReturningString(String, IFormatProvider)",
                                                           "UICultureAsIFormatProviderReturningStringTest.TestMethod()",
                                                           "IFormatProviderOverloads.IFormatProviderReturningString(String, IFormatProvider, IFormatProvider)"),
 GetIFormatProviderUICultureStringRuleBasicResultAt(11, 9, "UICultureAsIFormatProviderReturningStringTest.TestMethod()",
                                                           "CultureInfo.InstalledUICulture",
                                                           "IFormatProviderOverloads.IFormatProviderReturningString(String, IFormatProvider)"),
 GetIFormatProviderAlternateStringRuleBasicResultAt(12, 9, "IFormatProviderOverloads.IFormatProviderReturningString(String, IFormatProvider)",
                                                           "UICultureAsIFormatProviderReturningStringTest.TestMethod()",
                                                           "IFormatProviderOverloads.IFormatProviderReturningString(String, IFormatProvider, IFormatProvider)"),
 GetIFormatProviderUICultureStringRuleBasicResultAt(12, 9, "UICultureAsIFormatProviderReturningStringTest.TestMethod()",
                                                           "Thread.CurrentUICulture",
                                                           "IFormatProviderOverloads.IFormatProviderReturningString(String, IFormatProvider)"),
 GetIFormatProviderUICultureStringRuleBasicResultAt(13, 9, "UICultureAsIFormatProviderReturningStringTest.TestMethod()",
                                                           "Thread.CurrentUICulture",
                                                           "IFormatProviderOverloads.IFormatProviderReturningString(String, IFormatProvider, IFormatProvider)"),
 GetIFormatProviderUICultureStringRuleBasicResultAt(13, 9, "UICultureAsIFormatProviderReturningStringTest.TestMethod()",
                                                           "CultureInfo.InstalledUICulture",
                                                           "IFormatProviderOverloads.IFormatProviderReturningString(String, IFormatProvider, IFormatProvider)"));
        }

        [Fact]
        public async Task CA1305_NonStringReturningUICultureIFormatProvider_VisualBasicAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Globalization
Imports System.Threading

Public NotInheritable Class UICultureAsIFormatProviderReturningNonStringTest
    Private Sub New()
    End Sub
    Public Shared Sub TestMethod()
        IFormatProviderOverloads.IFormatProviderReturningNonString(""1"", CultureInfo.CurrentUICulture)
        IFormatProviderOverloads.IFormatProviderReturningNonString(""1"", CultureInfo.InstalledUICulture)
        IFormatProviderOverloads.IFormatProviderReturningNonString(""1"", Thread.CurrentThread.CurrentUICulture)
        IFormatProviderOverloads.IFormatProviderReturningNonString(""1"", Thread.CurrentThread.CurrentUICulture, CultureInfo.InstalledUICulture)
    End Sub
End Class

Friend NotInheritable Class IFormatProviderOverloads
    Private Sub New()
    End Sub
    Public Shared Sub IFormatProviderReturningNonString(format As String, provider As IFormatProvider)
    End Sub

    Public Shared Sub IFormatProviderReturningNonString(format As String, provider As IFormatProvider, provider2 As IFormatProvider)
    End Sub
End Class",
 GetIFormatProviderAlternateRuleBasicResultAt(10, 9, "IFormatProviderOverloads.IFormatProviderReturningNonString(String, IFormatProvider)",
                                                     "UICultureAsIFormatProviderReturningNonStringTest.TestMethod()",
                                                     "IFormatProviderOverloads.IFormatProviderReturningNonString(String, IFormatProvider, IFormatProvider)"),
 GetIFormatProviderUICultureRuleBasicResultAt(10, 9, "UICultureAsIFormatProviderReturningNonStringTest.TestMethod()",
                                                     "CultureInfo.CurrentUICulture",
                                                     "IFormatProviderOverloads.IFormatProviderReturningNonString(String, IFormatProvider)"),
 GetIFormatProviderAlternateRuleBasicResultAt(11, 9, "IFormatProviderOverloads.IFormatProviderReturningNonString(String, IFormatProvider)",
                                                     "UICultureAsIFormatProviderReturningNonStringTest.TestMethod()",
                                                     "IFormatProviderOverloads.IFormatProviderReturningNonString(String, IFormatProvider, IFormatProvider)"),
 GetIFormatProviderUICultureRuleBasicResultAt(11, 9, "UICultureAsIFormatProviderReturningNonStringTest.TestMethod()",
                                                     "CultureInfo.InstalledUICulture",
                                                     "IFormatProviderOverloads.IFormatProviderReturningNonString(String, IFormatProvider)"),
 GetIFormatProviderAlternateRuleBasicResultAt(12, 9, "IFormatProviderOverloads.IFormatProviderReturningNonString(String, IFormatProvider)",
                                                     "UICultureAsIFormatProviderReturningNonStringTest.TestMethod()",
                                                     "IFormatProviderOverloads.IFormatProviderReturningNonString(String, IFormatProvider, IFormatProvider)"),
 GetIFormatProviderUICultureRuleBasicResultAt(12, 9, "UICultureAsIFormatProviderReturningNonStringTest.TestMethod()",
                                                     "Thread.CurrentUICulture",
                                                     "IFormatProviderOverloads.IFormatProviderReturningNonString(String, IFormatProvider)"),
 GetIFormatProviderUICultureRuleBasicResultAt(13, 9, "UICultureAsIFormatProviderReturningNonStringTest.TestMethod()",
                                                     "Thread.CurrentUICulture",
                                                     "IFormatProviderOverloads.IFormatProviderReturningNonString(String, IFormatProvider, IFormatProvider)"),
 GetIFormatProviderUICultureRuleBasicResultAt(13, 9, "UICultureAsIFormatProviderReturningNonStringTest.TestMethod()",
                                                     "CultureInfo.InstalledUICulture",
                                                     "IFormatProviderOverloads.IFormatProviderReturningNonString(String, IFormatProvider, IFormatProvider)"));
        }

        [Fact]
        public async Task CA1305_NonStringReturningComputerInfoInstalledUICultureIFormatProvider_VisualBasicAsync()
        {
            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
Imports System
Imports System.Globalization
Imports System.Threading
Imports Microsoft.VisualBasic.Devices

Public NotInheritable Class UICultureAsIFormatProviderReturningNonStringTest
    Private Sub New()
    End Sub
    Public Shared Sub TestMethod()
        Dim computerInfo As New Microsoft.VisualBasic.Devices.ComputerInfo()
        IFormatProviderOverloads.IFormatProviderReturningNonString(""1"", computerInfo.InstalledUICulture)
    End Sub
End Class

Friend NotInheritable Class IFormatProviderOverloads
    Private Sub New()
    End Sub
    Public Shared Sub IFormatProviderReturningNonString(format As String, provider As IFormatProvider)
    End Sub
End Class",
                ExpectedDiagnostics =
                {
                    GetIFormatProviderUICultureRuleBasicResultAt(12, 9, "UICultureAsIFormatProviderReturningNonStringTest.TestMethod()",
                                                    "ComputerInfo.InstalledUICulture",
                                                    "IFormatProviderOverloads.IFormatProviderReturningNonString(String, IFormatProvider)"),
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CA1305_RuleException_NoDiagnostics_VisualBasicAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Globalization
Imports System.Threading

Public NotInheritable Class IFormatProviderStringTest
    Private Sub New()
    End Sub
    Public Shared Sub TrailingThreadCurrentUICulture()
        Dim s = New System.Resources.ResourceManager(Nothing)
        Console.WriteLine(s.GetObject("""", Thread.CurrentThread.CurrentUICulture))
        Console.WriteLine(s.GetStream("""", Thread.CurrentThread.CurrentUICulture))
        Console.WriteLine(s.GetResourceSet(Thread.CurrentThread.CurrentUICulture, False, False))

        Dim activator__1 = Activator.CreateInstance(Nothing, System.Reflection.BindingFlags.CreateInstance, Nothing, Nothing, Thread.CurrentThread.CurrentUICulture)
        Console.WriteLine(activator__1)
    End Sub
End Class");
        }

        [Fact]
        [WorkItem(2394, "https://github.com/dotnet/roslyn-analyzers/issues/2394")]
        public async Task CA1305_BoolToString_NoDiagnosticsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class SomeClass
{
    public string SomeMethod(bool b1, System.Boolean b2)
    {
        return b1.ToString() + b2.ToString();
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class SomeClass
    Public Function SomeMethod(ByVal b As Boolean) As String
        Return b.ToString()
    End Function
End Class
");
        }

        [Fact]
        [WorkItem(2394, "https://github.com/dotnet/roslyn-analyzers/issues/2394")]
        public async Task CA1305_CharToString_NoDiagnosticsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class SomeClass
{
    public string SomeMethod(char c1, System.Char c2)
    {
        return c1.ToString() + c2.ToString();
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class SomeClass
    Public Function SomeMethod(ByVal c As Char) As String
        Return c.ToString()
    End Function
End Class
");
        }

        [Fact]
        [WorkItem(2394, "https://github.com/dotnet/roslyn-analyzers/issues/2394")]
        public async Task CA1305_StringToString_NoDiagnosticsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class SomeClass
{
    public string SomeMethod(string s1, System.String s2)
    {
        return s1.ToString() + s2.ToString();
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class SomeClass
    Public Function SomeMethod(ByVal s As String) As String
        Return s.ToString()
    End Function
End Class
");
        }

        [Fact]
        [WorkItem(3378, "https://github.com/dotnet/roslyn-analyzers/issues/3378")]
        public async Task CA1305_GuidToString_NoDiagnosticsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
public class SomeClass
{
    public string SomeMethod(Guid g)
    {
        return g.ToString() + g.ToString(""D"");
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Public Class SomeClass
    Public Function SomeMethod(ByVal g As Guid) As String
        Return g.ToString() + g.ToString(""D"")
    End Function
End Class
");
        }

        [Fact]
        [WorkItem(5999, "https://github.com/dotnet/roslyn-analyzers/issues/5999")]
        public async Task CA1305_GuidParse_NoDiagnosticsAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net70,
                TestCode = @"
namespace Test
{
    using System;
    public class SomeClass
    {
        public Guid SomeMethod(string s)
        {
            return Guid.Parse(s);
        }
    }
}",
            }.RunAsync();
        }

        [Fact]
        public async Task CA1305_NullableInvariantTypes_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
public class SomeClass
{
    private char? _char;
    private bool? _bool;
    private Guid? _guid;

    public string SomeMethod()
    {
        return _char.ToString() + _bool.ToString() + _guid.ToString();
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Public Class SomeClass
    Private _char As Char?
    Private _bool As Boolean?
    Private _guid As Guid?

    Public Function SomeMethod() As String
        Return _char.ToString() & _bool.ToString() & _guid.ToString()
    End Function
End Class");
        }

        [Theory, WorkItem(3507, "https://github.com/dotnet/roslyn-analyzers/issues/3507")]
        [InlineData("DateTime")]
        [InlineData("DateTimeOffset")]
        public async Task CA1305_DateTimeOrDateTimeOffsetInvariantSpecifiers_NoDiagnosticAsync(string type)
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
using System;
public class C
{{
    public string M({type} d)
    {{
        return d.ToString(""o"") +
            d.ToString(""O"") +
            d.ToString(""r"") +
            d.ToString(""R"") +
            d.ToString(""s"") +
            d.ToString(""u"");
    }}
}}");
        }

        [Theory, WorkItem(3507, "https://github.com/dotnet/roslyn-analyzers/issues/3507")]
        [InlineData("DateTime")]
        [InlineData("DateTimeOffset")]
        public async Task CA1305_DateTimeOrDateTimeOffsetVariantSpecifiers_DiagnosticAsync(string type)
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
using System;
public class C
{{
    public string M({type} d)
    {{
        return {{|#0:d.ToString(""d"")|}} +
            {{|#1:d.ToString(""t"")|}} +
            {{|#2:d.ToString(""hh"")|}};
    }}
}}",
                GetIFormatProviderAlternateStringRuleCSharpResultAt(0, $"{type}.ToString(string)", $"C.M({type})", $"{type}.ToString(string, IFormatProvider)"),
                GetIFormatProviderAlternateStringRuleCSharpResultAt(1, $"{type}.ToString(string)", $"C.M({type})", $"{type}.ToString(string, IFormatProvider)"),
                GetIFormatProviderAlternateStringRuleCSharpResultAt(2, $"{type}.ToString(string)", $"C.M({type})", $"{type}.ToString(string, IFormatProvider)"));
        }

        [Fact, WorkItem(3507, "https://github.com/dotnet/roslyn-analyzers/issues/3507")]
        public async Task CA1305_TimeSpanInvariantSpecifiers_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public string M(System.TimeSpan t)
    {
        return t.ToString(""c"");
    }
}");
        }

        [Fact, WorkItem(3507, "https://github.com/dotnet/roslyn-analyzers/issues/3507")]
        public async Task CA1305_TimeSpanVariantSpecifiers_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public string M(System.TimeSpan t)
    {
        return {|#0:t.ToString(""g"")|} +
            {|#1:t.ToString(""hh:mm:ss"")|};
    }
}",
                GetIFormatProviderAlternateStringRuleCSharpResultAt(0, "TimeSpan.ToString(string)", "C.M(TimeSpan)", "TimeSpan.ToString(string, IFormatProvider)"),
                GetIFormatProviderAlternateStringRuleCSharpResultAt(1, "TimeSpan.ToString(string)", "C.M(TimeSpan)", "TimeSpan.ToString(string, IFormatProvider)"));
        }

        [Theory, WorkItem(5372, "https://github.com/dotnet/roslyn-analyzers/issues/5372")]
        // Diagnostics
        [InlineData("")]
        // No diagnostics
        [InlineData("dotnet_code_quality.CA1305.excluded_symbol_names = M1")]
        [InlineData("dotnet_code_quality.CA1305.excluded_symbol_names = M:NS.C.M1(System.String)")]
        [InlineData("dotnet_code_quality.CA1305.excluded_symbol_names = T:NS.C")]
        [InlineData("dotnet_code_quality.CA1305.excluded_symbol_names = N:NS")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = M1")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = M:NS.C.M1(System.String)")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = T:NS.C")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = N:NS")]
        [InlineData("dotnet_code_quality.CA1305.excluded_symbol_names = M*")]
        public async Task CA1305_ExcludedSymbolsOption_NoOverloads(string editorConfigText)
        {
            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;

namespace NS
{
    public class C
    {
        public void M1(string s) {}

        public void M()
        {
            M1(""aaa""); // No warning here.
        }
    }
}",
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
"), },
                },
            };

            await csharpTest.RunAsync();

            var vbTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Imports System

Namespace NS
    Public Class C
        Public Sub M1(ByVal s As String)
        End Sub

        Public Sub M()
            M1(""aaa"")
        End Sub
    End Class
End Namespace",
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
"), },
                },
            };

            await vbTest.RunAsync();
        }

        [Theory, WorkItem(5372, "https://github.com/dotnet/roslyn-analyzers/issues/5372")]
        // Diagnostics
        [InlineData("")]
        // No diagnostics
        [InlineData("dotnet_code_quality.CA1305.excluded_symbol_names = M1|M2")]
        [InlineData("dotnet_code_quality.CA1305.excluded_symbol_names = M:NS.C.M1(System.String)|M:NS.C.M2(System.String)")]
        [InlineData("dotnet_code_quality.CA1305.excluded_symbol_names = T:NS.C")]
        [InlineData("dotnet_code_quality.CA1305.excluded_symbol_names = N:NS")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = M1|M2")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = M:NS.C.M1(System.String)|M:NS.C.M2(System.String)")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = T:NS.C")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = N:NS")]
        [InlineData("dotnet_code_quality.CA1305.excluded_symbol_names = M*")]
        public async Task CA1305_ExcludedSymbolsOption_IFormatProviderPositions(string editorConfigText)
        {
            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;

namespace NS
{
    public class C
    {
        public void M1(string s) {}
        public void M1(string s, IFormatProvider provider) {}

        public void M2(string s) {}
        public void M2(IFormatProvider provider, string s) {}

        public void M3(string s1) {}
        public void M3(string s1, IFormatProvider provider, string s2) {}

        public void M4(string s1) {}
        public void M4(string s1, string s2, string s3) {}

        public void M()
        {
            M1(""aaa""); // Warning here.
            M2(""aaa""); // Warning here.
            M3(""aaa""); // No warning here.
            M4(""aaa""); // No warning here.
        }
    }
}",
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
"), },
                },
            };

            if (editorConfigText.Length == 0)
            {
                csharpTest.ExpectedDiagnostics.Add(GetIFormatProviderAlternateStringRuleCSharpResultAt(22, 13, "C.M1(string)", "C.M()", "C.M1(string, IFormatProvider)"));
                csharpTest.ExpectedDiagnostics.Add(GetIFormatProviderAlternateStringRuleCSharpResultAt(23, 13, "C.M2(string)", "C.M()", "C.M2(IFormatProvider, string)"));
            }

            await csharpTest.RunAsync();

            var vbTest = new VerifyVB.Test
            {
                TestState =
                            {
                                Sources =
                                {
                                    @"
Imports System

Namespace NS
    Public Class C
        Public Sub M1(ByVal s As String)
        End Sub

        Public Sub M1(ByVal s As String, ByVal provider As IFormatProvider)
        End Sub

        Public Sub M2(ByVal s As String)
        End Sub

        Public Sub M2(ByVal provider As IFormatProvider, ByVal s As String)
        End Sub

        Public Sub M3(ByVal s As String)
        End Sub

        Public Sub M3(ByVal s1 As String, ByVal provider As IFormatProvider, ByVal s2 As String)
        End Sub

        Public Sub M4(ByVal s1 As String)
        End Sub

        Public Sub M4(ByVal s1 As String, ByVal s2 As String, ByVal s3 As String)
        End Sub

        Public Sub M()
            M1(""aaa"") ' Warning here.
            M2(""aaa"") ' Warning here.
            M3(""aaa"") ' No warning here.
            M4(""aaa"") ' No warning here.
        End Sub
    End Class
End Namespace",
                },
                AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
"), },
                },
            };

            if (editorConfigText.Length == 0)
            {
                vbTest.ExpectedDiagnostics.Add(GetIFormatProviderAlternateStringRuleBasicResultAt(31, 13, "C.M1(String)", "C.M()", "C.M1(String, IFormatProvider)"));
                vbTest.ExpectedDiagnostics.Add(GetIFormatProviderAlternateStringRuleBasicResultAt(32, 13, "C.M2(String)", "C.M()", "C.M2(IFormatProvider, String)"));
            }

            await vbTest.RunAsync();
        }

        [Theory, WorkItem(5372, "https://github.com/dotnet/roslyn-analyzers/issues/5372")]
        // Diagnostics
        [InlineData("")]
        // No diagnostics
        [InlineData("dotnet_code_quality.CA1305.excluded_symbol_names = M1|M2|M3")]
        [InlineData("dotnet_code_quality.CA1305.excluded_symbol_names = M:NS.C.M1(System.String)|M:NS.C.M2(System.String)|M:NS.C.M3(System.String)")]
        [InlineData("dotnet_code_quality.CA1305.excluded_symbol_names = T:NS.C")]
        [InlineData("dotnet_code_quality.CA1305.excluded_symbol_names = N:NS")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = M1|M2|M3")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = M:NS.C.M1(System.String)|M:NS.C.M2(System.String)|M:NS.C.M3(System.String)")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = T:NS.C")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = N:NS")]
        [InlineData("dotnet_code_quality.CA1305.excluded_symbol_names = M*")]
        public async Task CA1305_ExcludedSymbolsOption_StringReturnType(string editorConfigText)
        {
            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;

namespace NS
{
    public class C
    {
        public string M1(string s) { return string.Empty; }
        public string M1(string s1, IFormatProvider provider) { return string.Empty; }

        public string M2(string s) { return string.Empty; }
        public void M2(IFormatProvider provider, string s) {}

        public void M3(string s) {}
        public string M3(string s1, IFormatProvider provider) { return string.Empty; }

        public string M4(string s) { return string.Empty; }
        public string M4(string s1, IFormatProvider provider, string s3) { return string.Empty; }

        public void M()
        {
            _ = M1(""aaa""); // Warning here.
            _ = M2(""aaa""); // Warning here.
            M3(""aaa"");     // Warning here.
            _ = M4(""aaa""); // No warning here.
        }
    }
}",
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
"), },
                },
            };

            if (editorConfigText.Length == 0)
            {
                csharpTest.ExpectedDiagnostics.Add(GetIFormatProviderAlternateStringRuleCSharpResultAt(22, 17, "C.M1(string)", "C.M()", "C.M1(string, IFormatProvider)"));
                csharpTest.ExpectedDiagnostics.Add(GetIFormatProviderAlternateStringRuleCSharpResultAt(23, 17, "C.M2(string)", "C.M()", "C.M2(IFormatProvider, string)"));
                csharpTest.ExpectedDiagnostics.Add(GetIFormatProviderAlternateStringRuleCSharpResultAt(24, 13, "C.M3(string)", "C.M()", "C.M3(string, IFormatProvider)"));
            }

            await csharpTest.RunAsync();

            var vbTest = new VerifyVB.Test
            {
                TestState =
                            {
                                Sources =
                                {
                                    @"
Imports System

Namespace NS
    Public Class C
        Public Function M1(ByVal s As String) As String
            Return String.Empty
        End Function

        Public Function M1(ByVal s As String, ByVal provider As IFormatProvider) as String
            Return String.Empty
        End Function

        Public Function M2(ByVal s As String) As String
            Return String.Empty
        End Function

        Public Sub M2(ByVal provider As IFormatProvider, ByVal s As String)
        End Sub

        Public Sub M3(ByVal s As String)
        End Sub

        Public Function M3(ByVal provider As IFormatProvider, ByVal s As String) as String
            Return String.Empty
        End Function

        Public Function M4(ByVal s As String) as String
            Return String.Empty
        End Function

        Public Function M4(ByVal s1 As String, ByVal provider As IFormatProvider, ByVal s3 As String) as String
            Return String.Empty
        End Function

        Public Sub M()
            M1(""aaa"") ' Warning here.
            M2(""aaa"") ' Warning here.
            M3(""aaa"") ' Warning here.
            M4(""aaa"") ' No warning here.
        End Sub
    End Class
End Namespace",
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
"), },
                },
            };

            if (editorConfigText.Length == 0)
            {
                vbTest.ExpectedDiagnostics.Add(GetIFormatProviderAlternateStringRuleBasicResultAt(37, 13, "C.M1(String)", "C.M()", "C.M1(String, IFormatProvider)"));
                vbTest.ExpectedDiagnostics.Add(GetIFormatProviderAlternateStringRuleBasicResultAt(38, 13, "C.M2(String)", "C.M()", "C.M2(IFormatProvider, String)"));
                vbTest.ExpectedDiagnostics.Add(GetIFormatProviderAlternateStringRuleBasicResultAt(39, 13, "C.M3(String)", "C.M()", "C.M3(IFormatProvider, String)"));
            }

            await vbTest.RunAsync();
        }

        [Theory, WorkItem(5372, "https://github.com/dotnet/roslyn-analyzers/issues/5372")]
        // Diagnostics
        [InlineData("")]
        // No diagnostics
        [InlineData("dotnet_code_quality.CA1305.excluded_symbol_names = Format")]
        [InlineData("dotnet_code_quality.CA1305.excluded_symbol_names = M:System.String.Format(System.String,System.Object)")]
        [InlineData("dotnet_code_quality.CA1305.excluded_symbol_names = T:System.String")]
        [InlineData("dotnet_code_quality.CA1305.excluded_symbol_names = N:System")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = Format")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = M:System.String.Format(System.String,System.Object)")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = T:System.String")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = N:System")]
        public async Task CA1305_ExcludedSymbolsOption_CultureInfo(string editorConfigText)
        {
            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;
using System.Globalization;

namespace NS
{
    public class C
    {
        public void M()
        {
            string name = ""Georgette"";
            string example1 = String.Format(""Hello {0}"", name);
            string example2 = String.Format(CultureInfo.CurrentCulture, ""Hello {0}"", name);
        }
    }
}",
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
"), },
                },
            };

            if (editorConfigText.Length == 0)
            {
                csharpTest.ExpectedDiagnostics.Add(GetIFormatProviderAlternateStringRuleCSharpResultAt(12, 31, "string.Format(string, object)", "C.M()", "string.Format(IFormatProvider, string, params object[])"));
            }

            await csharpTest.RunAsync();

            var vbTest = new VerifyVB.Test
            {
                TestState =
                            {
                                Sources =
                                {
                                    @"
Imports System
Imports System.Globalization

Namespace NS
    Public Class C
        Public Sub M()
            Dim name As String = ""Georgette""
            Dim example1 As String = String.Format(""Hello {0}"", name)
            Dim example2 As String = String.Format(CultureInfo.CurrentCulture, ""Hello {0}"", name)
        End Sub
    End Class
End Namespace",
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
"), },
                },
            };

            if (editorConfigText.Length == 0)
            {
                vbTest.ExpectedDiagnostics.Add(GetIFormatProviderAlternateStringRuleBasicResultAt(9, 38, "String.Format(String, Object)", "C.M()", "String.Format(IFormatProvider, String, ParamArray Object())"));
            }

            await vbTest.RunAsync();
        }

        [Fact]
        [WorkItem(5843, "https://github.com/dotnet/roslyn-analyzers/issues/5843")]
        public async Task IFormatProviderOptional()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
using System;
public class C
{
    public void M(ReadOnlySpan<char> span)
    {
        // IFormatProvider is last argument
        var d = {|#0:Decimal.Parse(span)|};

        // IFormatProvider is not last argument
        var date = {|#1:DateTime.Parse(span)|};

        // This 'Parse' call doesn't have overloads
        var c1 = {|#2:Parse("""")|};

        // Based on other tests, passing null seems acceptable so do not raise here
        var c2 = Parse("""", null);

        // An overload with IFormatProvider exists.
        var s = {|#3:MyMethod("""")|};
    }

    public C Parse(string s, IFormatProvider provider = null)
    {
        return null;
    }

    public string MyMethod(string s)
    {
        return null;
    }

    public string MyMethod(string s, IFormatProvider provider = null)
    {
        return null;
    }
}",
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(SpecifyIFormatProviderAnalyzer.IFormatProviderOptionalRule).WithLocation(0)
                        .WithArguments("decimal.Parse(ReadOnlySpan<char>, [NumberStyles], [IFormatProvider])"),
                    VerifyCS.Diagnostic(SpecifyIFormatProviderAnalyzer.IFormatProviderOptionalRule).WithLocation(1)
                        .WithArguments("DateTime.Parse(ReadOnlySpan<char>, [IFormatProvider], [DateTimeStyles])"),
                    VerifyCS.Diagnostic(SpecifyIFormatProviderAnalyzer.IFormatProviderOptionalRule).WithLocation(2)
                        .WithArguments("C.Parse(string, [IFormatProvider])"),
                    VerifyCS.Diagnostic(SpecifyIFormatProviderAnalyzer.IFormatProviderAlternateRule).WithLocation(3)
                        .WithArguments("C.MyMethod(string)", "C.M(ReadOnlySpan<char>)", "C.MyMethod(string, [IFormatProvider])"),
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
Imports System

Public Class C
    Public Sub M()
        Dim c1 = {|#0:Parse("""")|}
        Dim c2 = Parse("""", Nothing)
        Dim s = {|#1:MyMethod("""")|}
    End Sub

    Public Function Parse(ByVal s As String, ByVal Optional provider As IFormatProvider = Nothing) As C
        Return Nothing
    End Function

    Public Function MyMethod(ByVal s As String) As String
        Return Nothing
    End Function

    Public Function MyMethod(ByVal s As String, ByVal Optional provider As IFormatProvider = Nothing) As String
        Return Nothing
    End Function
End Class
",
                ExpectedDiagnostics =
                {
                    VerifyVB.Diagnostic(SpecifyIFormatProviderAnalyzer.IFormatProviderOptionalRule).WithLocation(0)
                        .WithArguments("C.Parse(String, [IFormatProvider])"),
                    VerifyVB.Diagnostic(SpecifyIFormatProviderAnalyzer.IFormatProviderAlternateRule).WithLocation(1)
                        .WithArguments("C.MyMethod(String)", "C.M()", "C.MyMethod(String, [IFormatProvider])"),
                },
            }.RunAsync();
        }

        [Theory, WorkItem(6586, "https://github.com/dotnet/roslyn-analyzers/issues/6586")]
        [InlineData("int")]
        [InlineData("uint")]
        [InlineData("long")]
        [InlineData("ulong")]
        [InlineData("short")]
        [InlineData("ushort")]
        [InlineData("double")]
        [InlineData("float")]
        [InlineData("decimal")]
        public Task FormatProviderForNullableValueTypes(string valueType)
        {
            var code = $@"
public class Test {{
    public void M({valueType}? x) {{
        var y = {{|#0:x.ToString()|}};
    }}
}}";

            return VerifyCS.VerifyAnalyzerAsync(code, new DiagnosticResult(SpecifyIFormatProviderAnalyzer.IFormatProviderOptionalRule).WithLocation(0).WithArguments($"{valueType}?.ToString()"));
        }

        [Theory, WorkItem(6746, "https://github.com/dotnet/roslyn-analyzers/issues/6586")]
        [InlineData("int")]
        [InlineData("uint")]
        [InlineData("long")]
        [InlineData("ulong")]
        [InlineData("short")]
        [InlineData("ushort")]
        [InlineData("double")]
        [InlineData("float")]
        [InlineData("decimal")]
        public Task FormatProviderForNullableValueTypesAlreadyProvided_NoDiagnostic(string valueType)
        {
            var code = $@"
using System.Globalization;

public class Test {{
    public void M({valueType}? x) {{
        var y = x?.ToString(CultureInfo.CurrentCulture);
    }}
}}";

            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Theory, WorkItem(6746, "https://github.com/dotnet/roslyn-analyzers/issues/6746")]
        [CombinatorialData]
        public Task FormatProviderForNullableValueTypes_NoDiagnostic(
            [CombinatorialValues("int", "uint", "long", "ulong", "short", "ushort", "double", "float", "decimal")] string valueType,
            [CombinatorialValues("GetHashCode", "GetValueOrDefault")] string methodName
        )
        {
            var code = $@"
public class Test {{
    public void M({valueType}? x) {{
        var y = x.{methodName}();
    }}
}}";

            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        private DiagnosticResult GetIFormatProviderAlternateStringRuleCSharpResultAt(int line, int column, string arg1, string arg2, string arg3) =>
#pragma warning disable RS0030 // Do not use banned APIs
            VerifyCS.Diagnostic(SpecifyIFormatProviderAnalyzer.IFormatProviderAlternateStringRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arg1, arg2, arg3);

        private DiagnosticResult GetIFormatProviderAlternateStringRuleCSharpResultAt(int markupKey, string arg1, string arg2, string arg3) =>
            VerifyCS.Diagnostic(SpecifyIFormatProviderAnalyzer.IFormatProviderAlternateStringRule)
                .WithLocation(markupKey)
                .WithArguments(arg1, arg2, arg3);

        private DiagnosticResult GetIFormatProviderAlternateRuleCSharpResultAt(int line, int column, string arg1, string arg2, string arg3) =>
#pragma warning disable RS0030 // Do not use banned APIs
            VerifyCS.Diagnostic(SpecifyIFormatProviderAnalyzer.IFormatProviderAlternateRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arg1, arg2, arg3);

        private DiagnosticResult GetIFormatProviderUICultureStringRuleCSharpResultAt(int line, int column, string arg1, string arg2, string arg3) =>
#pragma warning disable RS0030 // Do not use banned APIs
            VerifyCS.Diagnostic(SpecifyIFormatProviderAnalyzer.UICultureStringRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arg1, arg2, arg3);

        private DiagnosticResult GetIFormatProviderUICultureRuleCSharpResultAt(int line, int column, string arg1, string arg2, string arg3) =>
#pragma warning disable RS0030 // Do not use banned APIs
            VerifyCS.Diagnostic(SpecifyIFormatProviderAnalyzer.UICultureRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arg1, arg2, arg3);

        private DiagnosticResult GetIFormatProviderAlternateStringRuleBasicResultAt(int line, int column, string arg1, string arg2, string arg3) =>
#pragma warning disable RS0030 // Do not use banned APIs
            VerifyVB.Diagnostic(SpecifyIFormatProviderAnalyzer.IFormatProviderAlternateStringRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arg1, arg2, arg3);

        private DiagnosticResult GetIFormatProviderAlternateRuleBasicResultAt(int line, int column, string arg1, string arg2, string arg3) =>
#pragma warning disable RS0030 // Do not use banned APIs
            VerifyVB.Diagnostic(SpecifyIFormatProviderAnalyzer.IFormatProviderAlternateRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arg1, arg2, arg3);

        private DiagnosticResult GetIFormatProviderUICultureStringRuleBasicResultAt(int line, int column, string arg1, string arg2, string arg3) =>
#pragma warning disable RS0030 // Do not use banned APIs
            VerifyVB.Diagnostic(SpecifyIFormatProviderAnalyzer.UICultureStringRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arg1, arg2, arg3);

        private DiagnosticResult GetIFormatProviderUICultureRuleBasicResultAt(int line, int column, string arg1, string arg2, string arg3) =>
#pragma warning disable RS0030 // Do not use banned APIs
            VerifyVB.Diagnostic(SpecifyIFormatProviderAnalyzer.UICultureRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arg1, arg2, arg3);
    }
}
