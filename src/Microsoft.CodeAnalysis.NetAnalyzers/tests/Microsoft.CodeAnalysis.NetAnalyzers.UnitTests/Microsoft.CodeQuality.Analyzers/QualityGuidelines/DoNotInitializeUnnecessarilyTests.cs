// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.CSharp.Analyzers.QualityGuidelines.CSharpDoNotInitializeUnnecessarilyAnalyzer,
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.CSharpDoNotInitializeUnnecessarilyFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.VisualBasic.Analyzers.QualityGuidelines.BasicDoNotInitializeUnnecessarilyAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.UnitTests
{
    public class DoNotInitializeUnnecessarilyTests
    {
        [Fact]
        public async Task NoDiagnostics()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    private const int SomeConst = 0;

    private static object SyncObjStatic = new object();
    private static int? NullableIntStatic = 0;
    private static string EmptyStringValueStatic = """";
    private static string StringValueStatic1 = ""hello"", StringValueStatic2 = ""world"";
    private static char charValueStatic = '\r';
    private static bool s_boolValueStatic = true;
    public static byte ByteValueStatic = 1;
    protected internal static sbyte SByteValueStatic = -1;
    protected static int Int16ValueStatic = -1;
    private protected static uint UInt16ValueStatic = 1;
    public static int Int32ValueStatic = -1;
    internal static uint UInt32ValueStatic = 1;
    public static long Int64ValueStatic = long.MaxValue;
    public static ulong UInt64ValueStatic = ulong.MaxValue;
    static float s_someFloatStatic = 1.0f;
    static readonly double s_someDoubleStatic = 1.0;
    public static System.DateTime CurrentTimeStatic = System.DateTime.Now;

    private object SyncObjInstance = new object();
    private int? NullableIntInstance = 0;
    private string EmptyStringValueInstance = """";
    private string StringValueInstance = ""hello"";
    private char charValueInstance= '\r';
    private bool _boolValueInstance = true;
    public byte ByteValueInstance = 1;
    protected internal sbyte SByteValueInstance = -1;
    protected int Int16ValueInstance = -1;
    private protected uint UInt16ValueInstance = 1;
    public int Int32ValueInstance = -1;
    internal uint UInt32ValueInstance = 1;
    public long Int64ValueInstance = long.MaxValue;
    public ulong UInt64ValueInstance = ulong.MaxValue;
    float _someFloatInstance = 1.0f;
    readonly double _someDoubleInstance = 1.0;
    public System.DateTime CurrentTimeInstance = System.DateTime.Now;

    public System.DayOfWeek Week1 = (System.DayOfWeek)1;
    public System.DayOfWeek Week2 = System.DayOfWeek.Sunday;

    public int SomeIntProp { get; } = 42;
    public System.ValueTuple<int, int> SomeTuple = new System.ValueTuple<int, int>() { Item1 = 42, Item2 = 84 };

    public static readonly object BoxedInt32Default = default(int);
    public static readonly object BoxedInt32Value = 0;
    public static int? NullableInt32Default = default(int);
    public static int? NullableInt32Value = 0;
}");
        }

        [Fact]
        public async Task NoDiagnostics_VB()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Private Const SomeConst As Integer = 0
    Private Shared SyncObjStatic As Object = New Object()
    Private Shared NullableIntStatic As Integer? = 0
    Private Shared EmptyStringValueStatic As String = """"
    Private Shared StringValueStatic1 As String = ""hello"", StringValueStatic2 As String = ""world""
    Private Shared s_boolValueStatic As Boolean = True
    Public Shared ByteValueStatic As Byte = 1
    Protected Friend Shared SByteValueStatic As SByte = -1
    Protected Shared Int16ValueStatic As Integer = -1
    Private Protected Shared UInt16ValueStatic As UInteger = 1
    Public Shared Int32ValueStatic As Integer = -1
    Friend Shared UInt32ValueStatic As UInteger = 1
    Public Shared Int64ValueStatic As Long = Long.MaxValue
    Public Shared UInt64ValueStatic As ULong = ULong.MaxValue
    Shared s_someFloatStatic As Single = 1.0F
    Shared ReadOnly s_someDoubleStatic As Double = 1.0
    Public Shared CurrentTimeStatic As System.DateTime = System.DateTime.Now

    Private SyncObjInstance As Object = New Object()
    Private NullableIntInstance As Integer ? = 0
    Private EmptyStringValueInstance As String = """"
    Private StringValueInstance As String = ""hello""
    Private _boolValueInstance As Boolean = True
    Public ByteValueInstance As Byte = 1
    Protected Friend SByteValueInstance As SByte = -1
    Protected Int16ValueInstance As Integer = -1
    Private Protected UInt16ValueInstance As UInteger = 1
    Public Int32ValueInstance As Integer = -1
    Friend UInt32ValueInstance As UInteger = 1
    Public Int64ValueInstance As Long = Long.MaxValue
    Public UInt64ValueInstance As ULong = ULong.MaxValue
    Private _someFloatInstance As Single = 1.0F
    ReadOnly _someDoubleInstance As Double = 1.0
    Public CurrentTimeInstance As System.DateTime = System.DateTime.Now
    Public Week1 As System.DayOfWeek = CType(1, System.DayOfWeek)
    Public Week2 As System.DayOfWeek = System.DayOfWeek.Sunday
    Public ReadOnly Property SomeIntProp As Integer = 42
End Class
");
        }

        [Fact]
        public async Task NoDiagnostics_Nullable()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;

public class C
{
    private static object s_obj1 = null!;
    [AllowNull] private object _obj2 = null;
}

public class G<T>
{
    private T _value1 = default!;
    [AllowNull] private T _value2 = default;
}

namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property, Inherited = false)]
    internal sealed class AllowNullAttribute : Attribute { }
}
",
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp8,
            }.RunAsync();
        }

        [Fact]
        public async Task Diagnostics_InitializerRemoved()
        {
            await new VerifyCS.Test()
            {
                TestCode = @"
public class C
{
    private static object SyncObjStatic [|= null|];
    private static int? NullableIntStatic [|= null|];
    private static string StringValueStatic1 [|= null|], StringValueStatic2 [|= null|];
    private static char charValueStatic[|= '\0'|], anotherCharValueStatic = '\n';
    private static bool s_boolValueStatic [|= false|];
    public static byte ByteValueStatic [|= 0|];
    protected internal static sbyte SByteValueStatic     [|= 0|];
    protected static int Int16ValueStatic [|= 0|];
    private protected static uint UInt16ValueStatic [|= 0|];
    public static int Int32ValueStatic [|= 0|];
    internal static uint UInt32ValueStatic [|= 0|];
    public static long Int64ValueStatic [|= 0|];
    public static ulong UInt64ValueStatic [|= 0|];
    static float s_someFloatStatic [|= 0f|];
    static readonly double s_someDoubleStatic [|= 0|];
    public static System.DateTime CurrentTimeStatic [|= default|];

    private object SyncObjInstance[|=null|];
    private int? NullableIntInstance [|= null|];
    private string StringValueInstance [|= null|];
    private char charValueInstance[|= '\0'|];
    private bool _boolValueInstance [|= false|];
    public byte ByteValueInstance [|= 0|];
    protected internal sbyte SByteValueInstance [|= 0|];
    protected int Int16ValueInstance [|= 0|];
    private protected uint UInt16ValueInstance [|= 0|]; // some comment
    public int Int32ValueInstance [|= 0|];
    internal uint UInt32ValueInstance /*some comment*/[|= 0|];
    public long Int64ValueInstance/*some comment*/ [|= 0|];
    public ulong UInt64ValueInstance [|= 0|];
    float _someFloatInstance [|= 0f|];
    readonly double _someDoubleInstance [|= 0|];
    public System.DateTime    CurrentTimeInstance      [|= default|];
    public System.TimeSpan ts [|= new System.TimeSpan()|];

    public System.DayOfWeek Week1 [|= 0|];
    public System.DayOfWeek Week2 [|= (System.DayOfWeek)0|];

    public int SomeIntProp { get; } [|= 0|];
    public string SomeStringProp { get; set; } [|= null|];
}",
                FixedCode = @"
public class C
{
    private static object SyncObjStatic;
    private static int? NullableIntStatic;
    private static string StringValueStatic1, StringValueStatic2;
    private static char charValueStatic, anotherCharValueStatic = '\n';
    private static bool s_boolValueStatic;
    public static byte ByteValueStatic;
    protected internal static sbyte SByteValueStatic;
    protected static int Int16ValueStatic;
    private protected static uint UInt16ValueStatic;
    public static int Int32ValueStatic;
    internal static uint UInt32ValueStatic;
    public static long Int64ValueStatic;
    public static ulong UInt64ValueStatic;
    static float s_someFloatStatic;
    static readonly double s_someDoubleStatic;
    public static System.DateTime CurrentTimeStatic;

    private object SyncObjInstance;
    private int? NullableIntInstance;
    private string StringValueInstance;
    private char charValueInstance;
    private bool _boolValueInstance;
    public byte ByteValueInstance;
    protected internal sbyte SByteValueInstance;
    protected int Int16ValueInstance;
    private protected uint UInt16ValueInstance; // some comment
    public int Int32ValueInstance;
    internal uint UInt32ValueInstance /*some comment*/;
    public long Int64ValueInstance/*some comment*/ ;
    public ulong UInt64ValueInstance;
    float _someFloatInstance;
    readonly double _someDoubleInstance;
    public System.DateTime    CurrentTimeInstance;
    public System.TimeSpan ts;

    public System.DayOfWeek Week1;
    public System.DayOfWeek Week2;

    public int SomeIntProp { get; }
    public string SomeStringProp { get; set; }
}",
                NumberOfFixAllIterations = 2
            }.RunAsync();
        }

        [Fact]
        public async Task Diagnostics_VB()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Private Shared SyncObjStatic As Object [|= Nothing|]
    Private SomeInt As System.Int32 [|= 0|]
End Class
");
        }
    }
}